# 超市收纳单模式项目清理设计

## 目标

将当前多玩法 Unity 工程裁剪为只包含“超市收纳”的单模式工程。物理删除其他玩法的源码、场景、资源、编辑器工具、Addressables 配置、生成 Bundle 和历史备份，同时保证超市收纳能够正常编译、启动、游玩、结算，并可从设置面板返回场景内难度选择主页。

## 成功标准

- Unity 项目无 C# 编译错误。
- Build Settings 只包含超市收纳运行所需场景；无用的过场场景在确认无引用后删除。
- 启动游戏后直接进入超市收纳场景内主页。
- “杂货店”和“超级市场”两个难度都能开始游戏。
- 拾取、搬运、货架放置、技能、扫地机器人、奖励与结算流程正常。
- 设置面板的“返回主页”回到场景内难度选择页，不加载已删除的 MainHome。
- 保留的场景、Prefab 和 ScriptableObject 中不存在由本次清理产生的 Missing Script 或缺失 GUID。
- Addressables 只保留超市收纳及其实际依赖组，并可完成构建。
- 代码和配置中不再引用 Tower、Fishing、DestroyCity、WormDodge、EscapeSnake 或 AnimalHospital 命名空间、场景名、模块枚举和资源组。

## 保留边界

### 必须保留

- `Assets/Game/Modes/SupermarketOrganize` 及其 `.meta`。
- `Assets/Game/Scripts/SupermarketOrganize` 及其直接依赖。
- 超市场景实际使用的通用基础设施：启动、存档、金币/钻石、设置、音频、VFX、玩家移动、交互、UI 路由和 Addressables 运行支持。
- `SupermarketOrganizeScene` 中实际引用的共享资产；如果资产名称带有 Fishing 等旧名称，但仍被超市场景使用，则先解除或替换引用后再决定是否删除。
- Unity 包、第三方插件和 ProjectSettings 中超市运行所需内容。

### 必须删除

- AnimalHospital、DestroyCity、Fishing、Tower、WormDodge、EscapeSnake、Glide 及其专属源码和资源。
- MainHome、多模式选择界面和其他玩法入口。
- 超市不使用的宠物、翅膀、拖尾、鱼竿、签到、转盘、在线奖励、钓鱼图鉴、跑步机、力量训练等共享功能。
- 其他模式的编辑器生成器、场景布置工具、调试面板和构建分支。
- 其他模式 Addressables Group、Schema、Label、Catalog 记录和远程 Bundle。
- `LBT-AdFrameWork_Backups` 中的历史备份内容。
- 删除旧功能后确认无引用的图片、音频、Prefab、材质、配置和资源目录。

## 架构调整

### 单模式产品身份

`GameProductMode`、`GameplayModule`、`GameProductSettings` 和 `GameplayModuleContext` 统一收敛为超市收纳单模式。运行时不再从场景名或多模式列表推导当前模块，也不保留其他模式兼容分支。产品配置资产只序列化一个启用的 `SupermarketOrganize` 条目。

### 启动和场景导航

游戏从 `SupermarketOrganizeScene` 直接启动。MainHome 及其导航上下文被删除。设置面板继续通过已实现的模式内 override 调用 `SupermarketOrganizeDirector.ShowHome()`。如果静态引用扫描和运行验证证明 `SceneTransition` 不再被调用，则一并删除过场场景与过场导航代码；否则只保留超市实际需要的最小实现。

### 配置注册

`GameBootstrapper`、`ConfigRegistry` 和 `GameModeConfigBinder` 只注册：

- `SupermarketOrganizeConfig`
- `SupermarketItemDatabase`
- `SupermarketMapDatabase`
- `SupermarketOrganizeProgressionConfig`
- `SupermarketOrganizeUpgradeUiConfig`
- `SupermarketOrganizeHomeConfig`
- 经引用审计确认仍需要的通用平衡、经济、音频和 VFX 配置

删除所有其他模式的强类型字段、延迟解析、场景刷新和编辑器自动绑定逻辑。

### 玩家和 HUD

`SupermarketOrganizePlayerSetup` 不再调用其他模式的 Suppress 方法。旧模式组件必须从保留的玩家 Prefab 和超市场景序列化数据中清除。通用 HUD 和 UI Router 只保留超市页面、设置、经济和实际可达的弹窗；删除爬塔、钓鱼、虫子、摧毁城市和动物医院分支。

### 存档和经济

保留现有超市存档字段和用户设置，删除其他模式的存档数据类、迁移逻辑、模式命名空间切换和奖励路由。若旧存档包含其他模式字段，清理后的反序列化应忽略多余 JSON 字段，不再维护或写回这些字段。超市已有存档键保持稳定，避免玩家进度被无意重置。

### Addressables 和生成内容

保留 `Remote_SupermarketOrganize`、必要的 Local Group、Built In Data 和 Default Local Group。删除其他模式的 Group、Schema 和 Label，并更新 `AddressableAssetSettings.asset` 中的 GUID 列表。删除旧平台 Bundle 与 Catalog 后重新构建 Android 和 WebGL Addressables，确保 Catalog 不再列出已删除模式。

## 删除顺序

1. 建立删除前清单，记录保留场景、Prefab、SO 的 GUID 依赖和当前编译基线。
2. 将产品身份、启动器、配置注册、场景导航、HUD、存档和编辑器工具改为超市单模式。
3. 清理超市场景及保留 Prefab 中的旧模式组件和序列化引用。
4. 编译验证，确认保留代码不再引用其他模式类型。
5. 删除其他模式源码及对应 `.meta`，再次编译。
6. 删除其他模式场景、资源、MainHome 和无用共享功能，扫描 Missing Script 与缺失 GUID。
7. 删除其他 Addressables 组、Schema、Bundle、Catalog 和备份目录，重新构建 Addressables。
8. 执行最终编译、构建和运行冒烟测试。

这个顺序保证每次物理删除前，引用方已经被重构或清除，避免一次性产生难以定位的大量错误。

## 风险与处理

### 隐式 Unity GUID 引用

场景和 Prefab 可能引用代码搜索不到的资产。删除前使用 `.meta` GUID 反向扫描保留范围；删除后扫描空 GUID、Missing Script 和 Addressables 悬空项。所有保留场景必须由 Unity 重新导入和保存。

### 共享资产名称误导

部分超市场景绑定了名称包含 Fishing 的 VFX。文件名不是删除依据，实际 GUID 引用才是依据。只有在解除引用或找到超市替代资源后才删除。

### 已存在的陈旧引用

超市场景当前有部分配置 GUID 与现有资源 `.meta` 不一致。清理时将这些引用修复为有效资源或明确清空并使用 Resources 回退，避免把历史问题误判为本次删除导致的问题。

### 编辑器代码编译

即使运行时不调用，`Assets/Game/Editor` 中的其他模式强类型引用也会导致编辑器程序集编译失败。编辑器工具与运行时代码同步裁剪，模式专属工具直接删除，通用工具改为超市单模式。

### 未跟踪的嵌套目录

仓库根目录当前存在未跟踪的 `supermarket/` 嵌套 Git 目录。它不属于 Unity `Assets` 依赖图，本次不把它当作可删除的旧模式内容，也不修改其内部仓库。

## 验证方案

### 静态检查

- 搜索已删除模式命名空间、类型、场景名和 Addressables Label，预期在保留源码和配置中为零。
- 使用删除资产的 GUID 反向扫描保留场景、Prefab 和 SO，预期为零。
- 检查 Build Settings、Product Settings 和 Addressables Settings 只包含超市路径。

### Unity 批处理检查

- 用项目指定 Unity 版本 `2021.3.11f1c2` 执行无界面导入和脚本编译，日志中不得出现 `CS` 编译错误。
- 执行 Addressables 内容构建，确认只生成超市和必要共享 Bundle。
- 执行目标平台 Player Build 或项目现有构建审计，确保预处理器不会重新添加旧模式。

### 运行冒烟测试

- 启动直接进入超市场景内主页。
- 分别进入两个难度并完成一次基础拾取和放置。
- 打开设置并返回场景内主页，再次进入难度。
- 触发结算并返回主页。
- 检查 Console 无 Missing Script、Missing Reference、场景加载或 Addressables 错误。

## 非目标

- 不重做超市玩法、美术或数值。
- 不迁移到新的 Unity 工程或升级 Unity 版本。
- 不修改未跟踪的根目录 `supermarket/` 嵌套仓库。
- 不为已删除模式保留兼容 API、空壳类或占位资源。
