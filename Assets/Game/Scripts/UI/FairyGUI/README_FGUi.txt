FairyGUI (FGUI) v5.x + 本工程 UI 框架对接说明
========================================

目标：业务与数据仍走 Service + ViewModel；FGUI 只负责展示与输入，通过同一套入口打开界面。

一、不要改动的核心
-----------------
- UiCoordinator / UiRouter（Assets/Game/Scripts/UI/Framework/）
- *ScreenViewModel（Assets/Game/Scripts/UI/ViewModels/）
- SaveService、各类 *Service

二、推荐接入方式（双栈短期共存）
-----------------
1) 导入 FairyGUI-unity v5.2 插件后，在首场景创建 Stage（按官方文档：UIPanel 或代码里 GRoot.inst）。
2) 为每个 UiPanelId 写一个「FGUI 中介」类，例如 FguiResultMediator：
   - 实现 IUiPanelController（或与 UGUI 面板二选一注册）
   - Open(UiOpenPayload p) 内：从包里 CreateObject 出 GComponent，把 p.Settlement 绑定到控件
   - Close()：从 GRoot 移除并 Dispose（按官方生命周期）
3) 场景里只保留「一个」某 PanelId 的 IUiPanelController 实现：
   - 要么 UGUI 的 PetPanel，要么 FguiPetMediator，不要两个同时 Register 同一 UiPanelId。

三、与 ViewModel 的绑定
-----------------
- 结算：SettlementViewModel.ToBodyText() 可直接赋给 GTextField.text；正式项目建议拆字段绑定（高度、距离、金币等）。
- 宠物列表：PetScreenViewModel.Rows 用于虚拟列表（GList）数据源。
- 商店：CosmeticShopScreenViewModel.Rows + Kind。

四、资源加载
-----------------
- 小包：UIPackage.AddPackage(Resources.Load<TextAsset>("xxx")) 等（以官方 API 为准）。
- 大包：Addressables 异步加载 bytes 再 AddPackage；Open 前确保包已就绪。

五、输入 / TMP / WebGL
-----------------
- 见 FairyGUI-unity Release Notes：新输入系统宏 FAIRYGUI_INPUT_SYSTEM、WebGL 文本宏 FAIRYGUI_WEBGL_TEXT_INPUT、TMP SoftClip 等。
- 升级插件时固定 Unity + FGUI 版本组合，先在分支验证再合并。

六、调试
-----------------
- 仍可用 Game/Debug/Debug Window 改存档数据；UI 层用 UiRouter.OpenXxx() 打开界面验证绑定。

七、FGUI 工作台（编辑器）
-----------------
- 菜单位置：FGUI for Unity / 工作台（脚本与 USS 在 Assets/FGUIforUnity/Editor/Workflow/）。
- 默认发布导入目录：Assets/FGUIforUnity/Resources/FGUIPublish（可在设置里改）。
