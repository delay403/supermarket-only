# Unity 操作指南：从零把「超市收纳」跑通

> 适用仓库：`D:\code\roblox_gamecenter`（Unity **2021.3.11f1c2**，单玩法 = 超市收纳）
> 目标：从空场景开始，在 Unity 编辑器里一步步搭出「启动页 → 难度选择 → 限时整理 → 结算 → 商店升级」的完整闭环，并通过项目自带的验收器。

---

## 0. 开工前

### 0.1 选一条路线

| 路线 | 做法 | 适合 | 工作量 |
| --- | --- | --- | --- |
| **A（推荐）** | 在现有工程里，**新建一个空场景**，从零搭场景、绑引用、调数值，代码与配置资产复用 | 要亲手做一遍、要交报告证据 | 2~3 天 |
| B | 真·新建 Unity 工程，把 `Assets` 里的代码/插件/配置整包搬过去 | 只想理解工程组成 | 半天（搬运）+ 路线 A |

**路线 B 的最小步骤**（只想复刻工程时）：
1. Unity Hub 新建 **3D (URP)** 工程，版本必须 2021.3.11f1c2。
2. Package Manager 确认装上：**Addressables 1.19.19、URP 12.1.7、UGUI、2D Sprite、Post Processing 3.4.0**（见 [manifest.json](Packages/manifest.json)）。
3. 把仓库里的 `Assets/Game`、`Assets/FGUIforUnity`、`Assets/Plugins/Demigiant`、`Assets/LBT-AdFrameWork`、`Assets/Plus`、`Assets/AddressableAssetsData` 拷进新工程（第三方库是**内嵌**在 Assets 里的，不需要额外下载）。
4. 拷 `ProjectSettings`（Layer/Tag、Graphics、Quality、Player）。
5. 丢弃自动生成的 SampleScene，按下面的阶段一重搭。

### 0.2 打开工程后的第一件事

1. Unity 打开工程，等 Library 导入结束。
2. **Console 必须 0 error** 再动手（有编译错误时所有菜单工具都不会出现）。
3. 确认菜单栏出现 `Game / Supermarket Organize / ...`（说明 `Assets/Game/Editor` 编译成功，28 个编辑器工具可用）。

### 0.3 全局铁律（记住这 5 条，少走一半弯路）

1. **只有一个场景**：`SupermarketOrganizeScene`。加到 Build Settings 的独立启动场景会被验收器直接判死（[SupermarketOnlyProjectValidator.cs:109-116](Assets/Game/Editor/SupermarketOnlyProjectValidator.cs#L109-L116)）。
2. **货架的 `targetItemId` 必须和商品的 `itemId` 一字不差**——这是"摆完不结算"的头号原因。
3. **商品生成总数 = 货架槽位总数**，否则永远凑不齐胜利条件。
4. **能点菜单就别手搓**：UI、货架、生成区都有 `Ensure*` / `Setup*` 一键工具，手搓只会漏引用。
5. **改数值找 `.asset`，改行为找代码**。这个项目几乎全部手感都在 ScriptableObject 里。

---

## 总览：14 个阶段

| # | 阶段 | 主要操作 | 预计 |
| --- | --- | --- | --- |
| 1 | 场景与 Build Settings | 建/开场景，只加这一个场景 | 10 min |
| 2 | 启动器 | 放 `GameBootstrapper`，绑 4 个配置 | 15 min |
| 3 | 配置资产 | 7 个 SO 检查/新建/绑定 | 1 h |
| 4 | 玩家与相机 | 组件 + 2 个一键菜单 | 30 min |
| 5 | 难度与货架 | **最重**：建货架、`Setup Selected Container Zone`、绑难度 | 2 h |
| 6 | 商品生成区 | `Item Spawn Zone` + Spawner 绑定 | 40 min |
| 7 | 商品数据 | prefab + Excel 导入 + Database | 1 h |
| 8 | UI 与面板 | 5 个 `Ensure` 菜单 + 手绑字段 + 启动页 | 2 h |
| 9 | Director 装配 | `Ensure Director In Scene` + 7 个字段 | 30 min |
| 10 | 数值调参 | 金币/倒计时/成长/价格 | 1 h（可分多次） |
| 11 | 跑通一局 | 12 条验收清单 | 30 min |
| 12 | 一键验收 | 菜单 + 命令行 Validator | 15 min |
| 13 | Addressables | 命令行清理并构建 | 30 min |
| 14 | 出包与冒烟 | 切平台、构建、真机/WebGL | 1 h |

---

## 阶段 1：场景与 Build Settings

1. `File > Open Scene` 打开 `Assets/Game/Modes/SupermarketOrganize/Scenes/SupermarketOrganizeScene.unity`。
   - 从零的话：`File > New Scene > Basic (Built-in)`，**另存为**上面这个路径同名文件。
2. `File > Build Settings`：
   - 只勾选 `SupermarketOrganizeScene`，并确保它在第 0 位；
   - **不要**添加任何 Start/Transition 场景。
3. 保存工程（`Ctrl+S`）。

✅ 验收：Build Settings 里恰好 1 个场景。

---

## 阶段 2：放启动器 `GameBootstrapper`

1. Hierarchy 右键 → `Create Empty`，命名 `GameBootstrapper`。
2. `Add Component` → 搜 `GameBootstrapper`。
3. Inspector 四个配置槽（可留空走 fallback，但**建议全部手动绑死**，避免切场景丢配置）：

| 字段 | 绑什么 |
| --- | --- |
| `Balance Override` | `Assets/Game/Core/Resources/GameBalanceConfig.asset` |
| `Supermarket Organize Override` | `SupermarketOrganizeConfig.asset` |
| `Supermarket Items Override` | `SupermarketItemDatabase.asset` |
| `Supermarket Maps Override` | `SupermarketMapDatabase.asset` |

4. 可选：`Bootstrap Vfx Bindings` / `Bootstrap Sfx Library` / `Bootstrap Bgm Clip`（不做也不影响玩法，见 [GameBootstrapper.cs](Assets/Game/Scripts/Core/GameBootstrapper.cs#L23-L27)）。

✅ 验收：Play 后 Hierarchy 出现 `DontDestroyOnLoad > GameBootstrapper`，Console 没有 `SaveService` 相关报错。

---

## 阶段 3：配置资产（7 份 SO）

全部在 `Assets/Game/Modes/SupermarketOrganize/Content/Configs/SupermarketOrganize/`：

| 资产 | 作用 | 关键字段（要看的） |
| --- | --- | --- |
| `SupermarketOrganizeConfig` | 交互与表现总配置 | `raycastMaxDistance=8`、`coinsPerPlacedItem`、`coinsPerCompletedShelf`、`eyeHeight`、`scatterSeed`、物理落定参数 |
| `SupermarketOrganizeHomeConfig` | 难度入口 | `difficulties[]`：`id`（杂货店/超级市场）、`countdownSeconds`（240 / 360）、`staminaEnabled`（当前 0=关） |
| `SupermarketOrganizeProgressionConfig` | 成长曲线 | `*ByLevel`（效果数组，**长度 6，含 0 级**）、`*UpgradeCosts`（价格数组，**长度 5**） |
| `SupermarketOrganizeUpgradeUiConfig` | 商店 UI 文案 | 7 个条目：技能 4 + 属性 3，含 `displayNameOverride` / 图标 / `displayOrder` |
| `SupermarketItemDatabase` | 商品索引 | `items[]` |
| `SupermarketMapDatabase` + `SupermarketMapDef_Map1` | 地图与场景名 | `mapId`、`gameplaySceneName = SupermarketOrganizeScene` |
| `Item/*.asset` | 216 个商品定义 | `itemId` / `displayName` / `worldPrefab` |

**绑定方式（二选一）**：

- 推荐：场景里放一个空物体 → 加 `GameModeConfigBinder` → 把上面 6 个资产（含 Progression/UpgradeUi/Home）塞进去，运行时自动注册。
- 或者：塞进阶段 2 的 `GameBootstrapper`（只覆盖基础 4 项）。

✅ 验收：Play 时能生成商品、能打开商店，且 Console 无 `ConfigRegistry` 相关警告。

---

## 阶段 4：玩家与相机

### 4.1 Hierarchy 结构

```
Player            ← CharacterController + PlayerMotor + LegacyPlayerInput + PlayerRig
Main Camera       ← Camera + AudioListener
```

从零的话用 `3D Object > Capsule` 当 Player，删掉自带 Mesh Collider，加 `CharacterController`。

### 4.2 两个一键菜单（先后顺序固定）

1. `Game / Supermarket Organize / Fix Player For Supermarket Organize`
   → 自动补 7 个玩法组件：`SupermarketOrganizeStatsAggregator`、`PlayerSupermarketOrganizeController`、`SupermarketItemHighlightController`、`SupermarketCrosshairInteractor`、`SupermarketOrganizeSkillController`、`SupermarketOrganizeAttributeApplicator`、`SupermarketOrganizePlayerMotorBoost`，并启用 `PlayerRig`。
2. `Game / Supermarket Organize / Switch Main Camera To First Person`
   → 相机加 `SupermarketFirstPersonCamera`、禁用 `ThirdPersonCamera`、自动生成手提区锚点 `SupermarketHandleZoneAnchor`。

### 4.3 手绑检查（工具只保证"有组件"，不保证"指对了"）

| 组件 | 字段 | 应该指向 |
| --- | --- | --- |
| `SupermarketFirstPersonCamera` | `followTarget` / `input` | Player / Player 上的 `LegacyPlayerInput` |
| `SupermarketCrosshairInteractor` | `raycastCamera`、`itemMask`、`shelfMask` | Main Camera；商品 Layer、货架 Layer |
| `SupermarketOrganizeSkillController` | `playerTransform` | Player |

> **Layer 小技巧**：新建 `Item`、`Shelf` 两个 Layer，商品 prefab 设 `Item`，货架设 `Shelf`，然后把 `itemMask` / `shelfMask` 勾上。不设 Layer 也能跑，但准星会打到墙和地。

✅ 验收：Play 后鼠标转视角正常、WASD 能走、Console 无"未找到 PlayerMotor"。

---

## 阶段 5：难度与货架（最重的一步）

### 5.1 建难度根节点

```
Environment_Easy      ← 地面、墙、灯光、货架
Environment_Hard      ← 同上，布局更大
```

两个根节点**都要建**，但同一时刻只会显示一个。

### 5.2 把普通物体变成货架（核心操作）

1. 选中一个货架根物体（`Shelf_01`）。
2. `Game / Supermarket Organize / Setup Selected Container Zone`
   → 自动加 **`SupermarketContainerZone` + Trigger Collider + `GridOrigin` + `TipObject`**。
3. 在 Inspector 里填：

| 字段 | 含义 | 注意 |
| --- | --- | --- |
| `targetItemId` | 这个货架只收哪种商品 | 必须与商品的 `itemId` **完全一致** |
| `rows` / `columns` | 格子行列 | 槽位总数 = rows × columns |
| `cellSpacing` | 格子间距 | 摆出来不穿模即可 |
| `gridOrigin` / `tipObject` | 工具已建好 | 别删 |

4. 重复到该难度的货架全部建完。

### 5.3 绑定难度

1. 场景里放一个空物体加 `SupermarketOrganizeDifficultyManager`（全场只需要一个）。
2. 每个难度根节点上各加一个 `SupermarketOrganizeDifficultySetup`：

| 字段 | 填什么 |
| --- | --- |
| `difficultyId` | 必须等于 `HomeConfig.difficulties[i].id`：**杂货店 / 超级市场** |
| `mapRoots` | 该难度根节点（拖 `Environment_Easy`） |
| `autoCollectShelvesFromMapRoots` | ✅ 勾上（自动收集子物体里的货架） |
| `activeShelfRatio` | 调试期设 0.2（只启用 20% 货架）快速跑通，通了再改 1 |

3. 把两个 setup 拖进 Manager 的 `setups` 数组。
4. 批量操作可用：
   - `GameObject / Supermarket Organize / Add Selected Shelves To Difficulty Setup`（多选货架一次塞进去）
   - `Game / Supermarket Organize / Batch Assign Shelves To Difficulty`（窗口版）
   - `GameObject / Supermarket Organize / Sort And Rename Containers By Target Item Id`（按 itemId 排序改名）

✅ 验收：Play 进入难度后，只显示该难度的货架；HomePanel 上"货架：N 个 / 商品：M 个"数字正常。

---

## 阶段 6：商品生成区 + Spawner

1. 每个难度根节点下建生成区：`GameObject / Supermarket Organize / Item Spawn Zone`
   → 生成一个带 `SupermarketItemSpawnZone` 的物体，调 `localHalfExtents` 覆盖地面范围，`groundLocalY` 贴合地面高度。
2. 找到 Director 上的 **`SupermarketItemSpawner`**（阶段 9 会一起建出来），填：

| 字段 | 含义 |
| --- | --- |
| `scatterCenter` | 散落中心（场地中心地标） |
| `spawnZones` | 各难度的生成区数组 |
| `itemsRoot` | 空物体，商品实例都会挂它下面（方便一键清理） |
| `itemDatabase` / `config` | 对应两份资产 |

3. 记住生成规则：**每个商品生成多少，取决于"该 itemId 的货架总槽位数"**（`BuildSpawnCounts`），不是随机的。

✅ 验收：开始挑战后地面散落商品，数量与 HomePanel 显示的"商品：M 个"一致。

---

## 阶段 7：商品数据（prefab + Excel 导入）

1. 每个商品 prefab 必须有：**模型 + Collider（必需，否则准星打不到）+ `SupermarketItem`**；
   `SupermarketItem` 上填 `itemId`、`handleWidth`、`interactionCollider`；想有掉落物理再挂 `Rigidbody`。
2. 表格导入：`Game / Supermarket Organize / Import Item Sheet`（或 `Assets/Game/Supermarket Organize/Import Item Sheet` / `... From Excel`）
   → 按表头自动生成/更新 `Item/*.asset` 与 `SupermarketItemDatabase`。
3. 批量设置 prefab：选中一批 prefab → `Assets/Game/Supermarket Organize/Setup Selected Item Prefabs`。
4. 名字对齐：选中货架 → `GameObject / Supermarket Organize / Set Target Item Id From Object Name`（按物体名反推 itemId）。
5. 一致性检查：`Item` 目录的 `.asset` 数量应与 `SupermarketItemDatabase.items` 条目数一致（当前 216 vs 215，已知差异，见 [超市收纳项目说明.md](Docs/超市收纳项目说明.md) 第 11 节）。

✅ 验收：地面商品能被准星高亮并拾取；Console 无"没有匹配货架"警告。

---

## 阶段 8：UI 与面板

### 8.1 用 `Ensure` 菜单把框架搭出来（省几小时）

按顺序执行：

| 菜单 | 生成/补齐 |
| --- | --- |
| `Game / Supermarket Organize / Ensure Interaction HUD (Crosshair + Buttons)` | Canvas（若无）+ 准星、瞄准提示、货架反馈、交互按钮 |
| `Game / Supermarket Organize / Ensure Skill HUD + Upgrade Panel` | 技能栏（4 槽）+ 升级/商店面板 + 扫地机面板 + 主页 + 结算 + HUD 信息区 |
| `Game / Supermarket Organize / Ensure Result Panel` | 只补结算面板 |
| `Game / Supermarket Organize / Ensure Home Panel` | 只补难度选择主页 |
| `Game / Supermarket Organize / Rebuild Upgrade Panel (Left-Right Layout)` | 商店面板重建为左右布局 |

### 8.2 必须**手工**建的：启动页 `SupermarketOrganizeStartOverlay`

这是唯一没有一键工具的必需 UI，但验收器**强制要求**它存在且文案精确（[SupermarketOnlyProjectValidator.cs:145-179](Assets/Game/Editor/SupermarketOnlyProjectValidator.cs#L145-L179)）：

1. Canvas 下建面板 → 加 `SupermarketOrganizeStartOverlay`。
2. 绑定 6 个引用：`root`、`titleText`、`startButton`、`helpButton`、`helpPopupRoot`、`helpCloseButton`、`helpMaskButton`。
3. 文案要求（写死会被校验）：
   - 标题 = **超市整理**
   - 开始按钮 = **开始游戏**
   - 操作说明按钮 = **操作说明**
   - 操作说明弹窗**默认关闭**（`SetActive(false)`）

### 8.3 手绑清单（工具不会帮你绑业务字段）

| 面板 | 关键字段 |
| --- | --- |
| `SupermarketOrganizeHudSection` | `handleCountText`、`shelfProgressText`、`itemProgressText`、**`countdownText`**、`upgradeButton`、`sweepRobotButton`、`crosshairUi`、`shelfFeedback` |
| `SupermarketOrganizeInteractionHud` | `crosshairUi`、`aimHintUi`、`shelfFeedbackUi`、`interactButton`、`interactButtonLabel`、`switchItemButton`、`currentHeldItemNameText` |
| `SupermarketOrganizeHomePanel` | `homeConfig`、`difficultyButtons[]`（每个填 `entryId`）、`shopButton`、`coinText`、`root`、`detailPopup` |
| `SupermarketOrganizeResultPanel` | `titleText`、`messageText`、`shelfCountText`、`itemCountText`、`returnMapButton` |
| `SupermarketOrganizeUpgradePanel` | `uiConfig`、`skillGridContent`、`attributeGridContent`、`itemPrefab`、`detailUi`、`coinText` |
| `SupermarketOrganizeSkillHud` | `sectionRoot` + `skillSlots[4]`（顺序：扫描/磁铁/X射线/自动存货） |

### 8.4 设置面板（验收器重点检查）

- 场景层级里必须有 **`Canvas/Setting`**，且：
  - `Image.sprite` 非空（齿轮图）
  - `sizeDelta = (48, 48)`
  - `anchoredPosition = (42, -42)`
- 精灵图集：`Assets/Game/Core/Art/UI/Main.spriteatlas` 必须存在且**打包了** `Assets/Game/Core/Art/UI/Main` 目录。
- 设置面板 prefab 路径固定：`Assets/Game/Core/Art/UI/Prefabs/Panel/SettingPanel.prefab`，且 `mainPanel` / `closeButton` 有 sprite、两个滑条已绑、4 个静音/音量图标齐全。
- 坏了就用 `Tools / Supermarket / Repair Settings UI` 修。

✅ 验收：所有面板能 `SetActive` 打开；点击按钮无 `NullReferenceException`。

---

## 阶段 9：Director 装配与流程

1. `Game / Supermarket Organize / Ensure Director In Scene`
   → 创建 `SupermarketOrganizeDirector` + 自动挂 `SupermarketItemSpawner` + `SupermarketOrganizeIntroController`，并自动收集场景货架。
2. Inspector 手绑 7 个字段：

| 字段 | 指向 |
| --- | --- |
| `config` | `SupermarketOrganizeConfig` |
| `spawner` | 同物体上的 `SupermarketItemSpawner` |
| `introController` | 同物体上的 `SupermarketOrganizeIntroController` |
| `containerZones` | 场景全部货架（或让 `Auto Assign Container Zones To Director` 菜单填） |
| `difficultyManager` | 场景里的 `SupermarketOrganizeDifficultyManager` |
| `playerHandleAnchor` | 相机下的 `SupermarketHandleZoneAnchor`（`Switch Main Camera To First Person` 会自动建） |
| `useHomePageBeforeGameplay` | ✅ 勾上（先出启动页 → 再进难度选择） |

3. 漏了货架可再执行 `Game / Supermarket Organize / Auto Assign Container Zones To Director`。

✅ 验收：Play 流程为 **启动页 → 开始游戏 → 难度选择主页 → 选难度 → 入场动画 → Running（倒计时开始）**。

---

## 阶段 10：数值与手感调参（只改 `.asset`）

| 要调的东西 | 去哪改 |
| --- | --- |
| 单件商品金币 | `SupermarketOrganizeConfig.coinsPerPlacedItem`（当前 3，设计稿要求 1） |
| 货架完成金币 | `SupermarketOrganizeConfig.coinsPerCompletedShelf`（当前 30） |
| 限时秒数 | `SupermarketOrganizeHomeConfig.difficulties[i].countdownSeconds`（240 / 360） |
| 难度显示名/预览图 | 同上，`id` 同时被 `DifficultySetup.difficultyId` 引用，**改名要两边一起改** |
| 5 级技能效果/冷却/价格 | `SupermarketOrganizeProgressionConfig`（注意数组长度：效果 6 个含 0 级、价格 5 个） |
| 商店条目名称/图标/排序 | `SupermarketOrganizeUpgradeUiConfig` |
| 体力开关 | `SupermarketOrganizeHomeConfig.staminaEnabled` |
| 第一人称视角高度 | `SupermarketOrganizeConfig.eyeHeight` |
| 散落随机种子 | `SupermarketOrganizeConfig.scatterSeed`（固定种子方便复现） |

> 当前 `ProgressionConfig` 里几组数组**长度不齐**（如 `pickupRangeByLevel` 只有 5 项、`scannerCooldownByLevel` 只有 5 项），按"0~5 共 6 级"统一补齐，否则商店满级显示会错位。

**试玩调参法**（设计稿要求）：每档难度跑 3 局、记录通关用时，再回头改 `countdownSeconds`，只调配置不动代码。

---

## 阶段 11：跑通一局的 12 条检查清单

1. [ ] 启动页标题「超市整理」、按钮「开始游戏」「操作说明」、说明弹窗默认关闭
2. [ ] 点开始 → 进入难度选择，两个难度卡片显示货架/商品数
3. [ ] 选「杂货店」→ 入场动画 → 倒计时出现（04:00）
4. [ ] 鼠标被锁定（`Cursor.lockState = Locked`）、ESC 能打开设置
5. [ ] 准星指向地面商品会高亮，点击能拾取到手提区
6. [ ] 手提已满时不能继续拾取
7. [ ] 对准错误货架提示「错误的货架」，正确货架显示绿色预览
8. [ ] 每放对一件 +金币；每个货架首次摆满额外 +金币
9. [ ] 打开设置时倒计时停住，关闭后继续
10. [ ] 全部摆完 → 结算「整理完成」；时间耗尽 → 结算「时间到」
11. [ ] 结算「返回」回到难度选择；商店（`shopButton`）能打开、能购买、金币不足有提示
12. [ ] 退出 Play 再进：金币/技能等级/最快用时都还在（存档键 `RobloxPata.SupermarketOrganize.PlayerSaveData.json.v1`）

任何一条失败，先看 Console 第一条红字，再查附录 B。

---

## 阶段 12：一键验收

### 12.1 编辑器内

`Game / Supermarket Organize / Validate Supermarket-Only Project`
→ 期望 Console 输出 `[SupermarketOnlyProjectValidator] PASS`。

它会检查：禁用路径/禁词 → Build Settings 场景 → Addressables 组 → 场景 Missing Script → 启动页引用与文案 → 设置 UI 图集与面板 → 旧存档 JSON 兼容。

### 12.2 命令行（相当于 CI）

```powershell
$U = 'D:\unity\2021.3.11f1c2\Editor\Unity.exe'
$P = 'D:\code\roblox_gamecenter'
# 1) 编译门禁
& $U -batchmode -nographics -quit -projectPath $P -logFile "$P\Logs\compile.log"
Select-String -Path "$P\Logs\compile.log" -Pattern 'error CS|Aborting batchmode'
# 2) 项目验收
& $U -batchmode -nographics -quit -projectPath $P `
  -executeMethod Game.Editor.SupermarketOnlyProjectValidator.ValidateBatch `
  -logFile "$P\Logs\validator.log"
```

> 前提：Unity 没被编辑器实例占用（同一工程不能同时开两个）、License 正常。命令返回 0 且日志里 `PASS` 才算过。

---

## 阶段 13：Addressables 清理与构建

1. `Window > Asset Management > Addressables > Groups`，确认只剩这些组：
   `Built In Data`、`Default Local Group`、`Main_Local`、`Remote_SupermarketOrganize`（出现其他 `Remote_*` 直接删组）。
2. 用命令行清理并构建（这个工具**没有菜单项**，只能命令行）：

```powershell
& $U -batchmode -nographics -quit -projectPath $P `
  -executeMethod Game.Editor.SupermarketOnlyAddressablesUtility.CleanAndBuildBatch `
  -logFile "$P\Logs\addressables.log"
```

3. 切平台（Android 或 WebGL）→ `Play Mode Script` 设 `Use Existing Build` → 再跑一次上面的构建。

✅ 验收：`ServerData/` 下没有旧玩法的 bundle；日志无 Addressables error。

---

## 阶段 14：出包与冒烟

```powershell
& $U -batchmode -nographics -quit -projectPath $P `
  -executeMethod Game.Editor.SupermarketOnlyAddressablesUtility.BuildDevelopmentPlayerBatch `
  -logFile "$P\Logs\player-build.log"
```

出包后重点看（这模式性能风险就在这几处）：**首帧商品生成、商品落地那 1~2 秒、快速甩视角、磁铁一次吸 10 件以上**。

---

## 附录 A：一键菜单速查

| 菜单 | 用途 |
| --- | --- |
| `Game/Supermarket Organize/Setup Selected Container Zone` | 选中物体变货架（加组件+GridOrigin+TipObject） |
| `Game/Supermarket Organize/Fix Player For Supermarket Organize` | 玩家补 7 个玩法组件 |
| `Game/Supermarket Organize/Switch Main Camera To First Person` | 相机转第一人称 |
| `Game/Supermarket Organize/Ensure Interaction HUD (Crosshair + Buttons)` | 准星/交互 UI |
| `Game/Supermarket Organize/Ensure Skill HUD + Upgrade Panel` | 技能栏+商店+主页+结算+HUD 一把梭 |
| `Game/Supermarket Organize/Ensure Result Panel` / `Ensure Home Panel` / `Ensure Sweep Robot Panel` | 单独补面板 |
| `Game/Supermarket Organize/Rebuild Upgrade Panel (Left-Right Layout)` | 商店面板重建 |
| `Game/Supermarket Organize/Ensure Director In Scene` | 建 Director + Spawner + Intro |
| `Game/Supermarket Organize/Auto Assign Container Zones To Director` | 货架批量塞进 Director |
| `Game/Supermarket Organize/Ensure Sweep Robot In Scene` / `Snap Sweep Robot To NavMesh` | 扫地机器人（首版可不做） |
| `Game/Supermarket Organize/Validate Supermarket-Only Project` | 全项目验收 |
| `Game/Supermarket Organize/Wire SFX To Game Bootstrapper` | 音效表接线 |
| `Game/Supermarket Organize/Import Item Sheet`（或 `...From Excel`） | 商品表格导入 |
| `Game/Supermarket Organize/Batch Setup Item Prefabs` | 批量设置商品 prefab |
| `Game/Supermarket Organize/Batch Assign Shelves To Difficulty` | 批量货架→难度 |
| `Game/Supermarket Organize/Batch Rename` / `Batch Material Duplicate` | 批量改名/材质去重 |
| `GameObject/Supermarket Organize/Item Spawn Zone` | 建商品生成区 |
| `GameObject/Supermarket Organize/Add Selected Shelves To Difficulty Setup` | 多选货架塞难度 |
| `GameObject/Supermarket Organize/Sort And Rename Containers By Target Item Id` | 按 itemId 排序改名 |
| `GameObject/Supermarket Organize/Set Target Item Id From Object Name` | 按名字反推 itemId |
| `GameObject/Supermarket Organize/Clean Container Children` | 清理货架里多余子物体 |
| `Game/Tools/Batch Rename` / `Batch Material Duplicate` / `Batch Assign UI Font` | 通用资源批处理 |
| `Tools/Supermarket/Repair Settings UI` | 修复设置面板 |
| `Tools/资源工具/批量修复 UI 字体` | 中文字体批量修复 |
| `Game/URP/Strip PostProcessData From Project Renderers` | 构建前裁后处理数据 |
| `Game/Generate/Create Supermarket Organize Configs` | 一键重建配置资产 |

---

## 附录 B：故障排查表

| 症状 | 先查什么 |
| --- | --- |
| 准星打不到商品 | 商品有没有 Collider；商品在不在 `itemMask` 的 Layer；距离是否 > 8m |
| 商品捡不起来 | `SupermarketItem.itemId` 为空；商品是否注册到 Service；手提区是否已满 |
| 提示「错误的货架」但看着没错 | 货架 `targetItemId` 与商品 `itemId` 有空格/大小写/全角差异 |
| 全摆完不结算 | 地面剩余、手提剩余、满货架数、已放置数四个条件；生成总数与槽位总数是否一致 |
| 倒计时不走 | Director 状态是否在 `Running`；`useHomePageBeforeGameplay` 是否卡在主页 |
| 打开设置后时间还在跑 | 暂停逻辑是否只改了 `Time.timeScale` 但计时用了 `unscaledDeltaTime`（当前 Service 用 unscaled，需在暂停时显式停） |
| 商店点不开 | `shopButton` 是否绑定；是否只在 `Lobby` 允许打开；`coinText` 是否绑 |
| 难度切换后货架重叠 | 两个 `DifficultySetup.mapRoots` 是否指到同一个根节点；`activeShelfRatio` 是否 1 |
| 扫地机器人不动 | NavMesh 是否烘焙；Agent 是否吸附到 NavMesh；货物是否有可达路径 |
| 重启后金币归零 | 存档键是否被改；`SaveService.Save()` 是否在 ApplicationPause/Quit 前触发 |
| 验收器报 `Missing script` | 场景里有删除过脚本的组件，用 Inspector 的 Missing 提示逐个 Remove |

---

## 附录 C：建议留存的报告素材（对应设计文档 §17）

1. **Hierarchy 截图**：Player / Main Camera / Canvas（各面板）/ Environment_Easy+Hard / Director。
2. **Inspector 截图**：Director 7 个字段、HUD `countdownText`、HomePanel `shopButton`、一个货架的 `targetItemId`、一个商品的 `itemId`。
3. **配置截图**：HomeConfig 的 `countdownSeconds`、ProgressionConfig 的 5 级数组。
4. **录屏 1**：启动页 → 操作说明 → 开始 → 难度选择 → 一局完整整理 → 结算 → 商店购买。
5. **录屏 2**：时间耗尽结算（不算通关）+ 完整通关（算通关）对比。
6. **试玩数据表**：每档难度 3 局的用时/完成度/金币，以及据此调整 `countdownSeconds` 的记录。
7. **验收截图**：Validator `PASS` 的 Console。
8. **Git 提交记录**：分阶段提交，便于展示"自己完成的部分"。

---

## 一句话收尾

**场景搭建、Inspector 绑定、配置调参、试玩验收**这四件事必须你亲手在 Unity 里做（也正是报告要的证据）；**代码补全、批处理编译、验收、Addressables 构建**可以交给脚本或 AI。先把阶段 1~9 用手搭出"能玩"，再用阶段 10~14 打磨"能交"。
