# 面试尖塔 · Unity / 团结引擎版

本文件是本目录的长期上下文说明，供 Codely 及后续开发者在阅读代码前快速建立整体认识。修改工程前请先读本文件与 `InterviewSpireUnity/README.md`。

## 1. 项目概览

**面试尖塔（Interview Spire）** 是一款把「面试准备」包装成**卡牌构筑 Roguelike** 的单机游戏：玩家扮演候选人，用「自我介绍」「量化成果」「技术深挖」等卡牌对面试官造成「认可伤害」，依次通过 4 轮面试拿到 Offer。

本目录是**团结引擎（Tuanjie）/ Unity 移植版**，从上级目录 `C:\Users\Klein Loius\Desktop\游戏demo\源码` 中的 Python + Tkinter Demo 完整移植而来。原 Python 版的数据文件（`data.json`、`save.json`、`stats.json`）、`美术资源/` 与 `面试尖塔.exe` 位于上级目录，本目录只负责引擎版工程与其构建产物。

目录内同时存在**两份内容**：

| 路径 | 性质 | 说明 |
| --- | --- | --- |
| `InterviewSpireUnity/` | **源码工程（唯一真相源）** | 团结引擎工程，所有代码、资源、数据表都在这里。改功能只改这里。 |
| `InterviewSpire.exe`、`InterviewSpire_Data/`、`MonoBleedingEdge/`、`TuanjiePlayer.dll`、`GFSDK_Aftermath_Lib.x64.dll`、`TuanjieCrashHandler64.exe` | **构建产物** | 由工程 Build 出来的 Windows Player，可直接双击运行。不要手工编辑这些文件。 |

架构要点：**逻辑层与界面层分离**，`GameController` 只处理规则与状态，`GameUI` 用 UGUI 代码在运行时搭建全部界面（**没有任何预制体、没有场景手工摆放的 UI**）。

## 2. 目录结构

```
Unity版/
├─ CODELY.md                      本文件
├─ InterviewSpire.exe             Windows 构建产物（可直接运行）
├─ InterviewSpire_Data/           构建产物数据（level0、globalgamemanagers、资源包等）
├─ MonoBleedingEdge/              构建产物运行时（Mono）
├─ TuanjiePlayer.dll 等           构建产物依赖库
├─ .codely-cli/settings.json      Codely CLI 配置（模型 codely-basic；MCP：TJGenerators、AssetStore）
└─ InterviewSpireUnity/           ← 团结引擎工程，改动都发生在这里
   ├─ README.md                   功能清单与已知细节（部分路径信息已过时，见第 8 节）
   ├─ ProjectSettings/            引擎工程设置（ProjectVersion.txt 记录 2022.3.62t18）
   ├─ Packages/manifest.json      仅依赖 com.unity.ugui 与少量内置模块
   ├─ Logs/                       Editor.log / 运行日志
   ├─ UserSettings/
   └─ Assets/
      ├─ Scenes/Main.unity       首次打开时由 ProjectBootstrap 自动生成，被写入构建列表
      ├─ Scripts/
      │  ├─ Core/                GameController、GameState/Models、SaveSystem、DisplaySettings、StatsSystem、GameBootstrap
      │  ├─ Data/                GameDatabase（读取 game_data.json 与导入题库）
      │  ├─ UI/                  GameUI（运行时搭 UGUI，613 行）、CardDragHandler（拖拽出牌）
      │  └─ Editor/              ProjectBootstrap、ArtImporter、SmokeTest（均以 #if UNITY_EDITOR 包裹）
      ├─ StreamingAssets/
      │  └─ game_data.json       全部游戏数值与文本（卡牌/敌人/事件/题库/难度/权重）
      └─ Resources/Art/          运行时美术资源（约 90 张 PNG）
         ├─ Cards/   (57)  卡面
         ├─ Enemies/ (13)  4 位面试官 × 表情/立绘
         ├─ FX/      (10)  受击、契合、通关特效
         ├─ UI/      (8)   按钮、血条、面板底件
         ├─ Backgrounds/ (1)  全屏背景
         └─ Player/  (1)   候选人头像
```

## 3. 技术栈与版本

- **引擎**：团结引擎 **2022.3.62t18**（`ProjectSettings/ProjectVersion.txt`，`m_TuanjieEditorVersion: 1.11.1`）。Unity 2022.3 LTS 同版本线亦可尝试。
- **编辑器可执行文件**：`D:\Tuanjie Cowork\2022.3.62t18\Editor\Tuanjie.exe`；命令行 `tuanjie`（`D:\Tuanjie Cowork\hub\tuanjie.exe`）可用。
- **渲染 / UI**：内置渲染管线 + UGUI（`com.unity.ugui`）。**未使用 TextMeshPro**，中文字体通过 `Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16)` 获取，失败时回退 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`。
- **序列化**：全部使用 `UnityEngine.JsonUtility`（无 Newtonsoft；字段必须 public 且可序列化，不支持字典/多态）。
- **语言**：C#，脚本均在命名空间 `InterviewSpire`（编辑器脚本在 `InterviewSpire.Editor`）。
- **产品标识**：company `DefaultCompany`，product `InterviewSpireUnity`。

## 4. 构建与运行

### 4.1 编辑器内运行（主要开发方式）

1. 用团结引擎（或 Unity 2022.3）打开 `InterviewSpireUnity/` 目录。
2. 首次打开时 `ProjectBootstrap`（`[InitializeOnLoad]` + `EditorApplication.delayCall`）会自动：
   - 创建 `Assets/Scenes/Main.unity`（若不存在）；
   - 把该场景写入 `EditorBuildSettings.scenes`。
3. 直接按 **Play** 即可。即使打开的是空场景也能跑：`GameBootstrap` 用 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 自动生成 `GameController` + `GameUI` 两个对象，**不需要手工挂脚本或搭 UI**。

### 4.2 命令行冒烟测试

`Assets/Scripts/Editor/SmokeTest.cs` 提供 `InterviewSpire.Editor.SmokeTest.Run()`：不进入 Play 模式，直接 NewGame + 打一张牌，并输出手牌数、敌人血量、题目类型与结束后精力。用于快速回归核心逻辑：

```powershell
& "D:\Tuanjie Cowork\2022.3.62t18\Editor\Tuanjie.exe" `
  -batchmode -quit -nographics `
  -projectPath "C:\Users\Klein Loius\Desktop\游戏demo\Unity版\InterviewSpireUnity" `
  -executeMethod InterviewSpire.Editor.SmokeTest.Run `
  -logFile -
```

### 4.3 构建 Windows Player

工程内**没有** BuildPipeline 脚本，构建走编辑器 GUI：

1. 团结引擎 → `File > Build Settings`，确认 `Assets/Scenes/Main.unity` 已勾选；
2. 目标平台 `Windows / Standalone Windows 64-bit` → `Build`；
3. 输出目录选本目录（`Unity版/`），产物即 `InterviewSpire.exe` + `InterviewSpire_Data/`。

> TODO：如需 CI 化构建，可在 `Assets/Scripts/Editor/` 新增 `BuildScript.PerformBuild()` 并用 `-executeMethod` 调用；目前不存在。

### 4.4 其他编辑器菜单

- `Tools > Interview Spire > Reimport Art`：把 `Assets/Resources/Art/` 下全部 PNG 强制按 Sprite 重新导入（`ArtImporter` 是 `AssetPostprocessor`，只对路径含 `/Resources/Art/` 的贴图生效）。

## 5. 运行时数据与持久化

所有持久化路径都由 `Application.persistentDataPath` 决定，实测为：

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\InterviewSpireUnity\
```

| 文件 | 写入者 | 内容 |
| --- | --- | --- |
| `saves\*.json` | `SaveSystem` | 多存档：`<名称>_<yyyyMMdd_HHmmss>_<随机数>.json`，整份 `GameState` 序列化 |
| `题库.json` | `GameDatabase.SaveImported()` | 玩家在设置里粘贴导入的题库（`ImportedBank.groups`） |
| `display.json` | `DisplaySettings` | 窗口模式与分辨率（默认 1240×820 窗口化） |
| `stats.json` | `StatsSystem` | 跨局匿名汇总，最多保留最近 100 局（岗位/难度/胜负/轮次/伤害/卡牌数） |
| `Player.log`、`Player-prev.log` | 引擎 | 运行日志，排查启动问题先看这里 |

静态数据只读文件是 `Assets/StreamingAssets/game_data.json`，规模（已核对）：

- 卡牌 **57** 张（starter 5 / common 28 / uncommon 15 / rare 9），起手牌组 10 张；
- 难度 **3** 档：轻松 / 标准 / 硬核；
- 面试官 **4** 轮：简历追问官 → 技术面试官 → 压力面主管 → 终面合伙人；
- 事件 **5** 个；题型组 **6** 类；岗位题库 **4** 个方向（软件开发 / 市场营销 / 管培生 / 产品设计）；
- 另有 `intent_scale`（逐轮敌方强度系数）、`job_weights`（岗位题型权重）、`status_info`（状态文案）。

## 6. 代码架构

### 6.1 逻辑层（Assets/Scripts/Core）

- **`GameController.cs`**（约 533 行）：唯一的战斗规则实现，`MonoBehaviour` 单例（`GameController.Instance`）。
  - 对外事件：`Changed`（状态变更，UI 重绘）、`Effect(fx 文件名, 目标 0 玩家/1 面试官/-1 全屏)`、`NeedReward`、`NeedEvent`、`GameOver(bool won)`。
  - 生命周期：`NewGame()` → `StartBattle(index)` → `StartPlayerTurn(first)`；回合 phase 语义 `0 玩家 / 1 敌方 / 2 奖励 / 3 结束`。
  - 卡牌效果由 `game_data.json` 里的 `ops` 数组驱动（`dmg` / `dmg_if` / `block` / `block_if` / `draw` / `heal` / `energy` / `cleanse` / `discount`），条件由 `CondMet()` 解析（`low_hp`、`non_common`、`first_card`、`last_tag:X`、`cards>=N`）。
  - `def.ops` 为空的旧卡走 `LegacyEffect(key, upgraded)` 硬编码效果表——**新增卡牌应写 `ops`，不要再扩展 `LegacyEffect`**。
  - 关键规则：题意契合（卡牌 `tag == 当前题 type`）伤害 ×1.5；通用伤害牌每回合首张 +2；硬核起手塞一张 uncommon；技术牌在 `tech_chain` 面试官处触发追问链（下一次攻击 +4）；负面状态（紧张 `vulnerable` / 自我怀疑 `selfDoubt` / 语塞 `energyPenalty`）**只覆盖玩家下一整回合**，在面试官下一次行动时结算或清除。
  - `EnemyMood`（沉思 / 愤怒 / 得意）由出牌是否契合与面试官是否攻击决定。
- **`Models.cs`**：`RuntimeCard`、`EnemyRuntime`、`QuestionRuntime`、`LogEntry`、`GameState`、`SaveRecord`。`GameState` 就是存档格式，改动字段需考虑旧存档兼容。
- **`SaveSystem.cs`**：命名存档、列表按时间倒序、`DeleteForRun(runId)` 清本局存档（通关/失败/重开时调用）。
- **`DisplaySettings.cs`**：`Screen.SetResolution` 封装，**编辑器内不真正改分辨率**（`if (!Application.isEditor)`），只落盘。
- **`StatsSystem.cs`**：只记录匿名数值，明确不保存玩家自由文本。
- **`GameBootstrap.cs`**：保证任意场景 Play 都能起游戏。

### 6.2 数据层（Assets/Scripts/Data）

`GameDatabase`：静态类，`Load()` 读取 `StreamingAssets/game_data.json`（`Data` 单例缓存），`LoadImported()/SaveImported()` 读写 `persistentDataPath/题库.json`；`Card(key)` 按 key 查卡。对应的 DTO（`CardData`、`OpData`、`EnemyData`、`JobQuestionData` 等）也定义在本文件顶部。

### 6.3 界面层（Assets/Scripts/UI）

- **`GameUI.cs`**（约 613 行）：`Start()` 里加载字体 → 确保 `EventSystem` → `Build()` 纯代码搭 HUD → 订阅 `GameController` 事件 → `ShowStart()` 显示开局设置弹窗（此时隐藏 HUD）。所有面板/按钮/输入框都是运行时 `new GameObject(...)` 生成。
  - 弹窗：开局、保存、读档、轮末奖励、轮间事件、结算、帮助、三步新手引导、设置（特效开关 / 动效强度 50-75-100% / 窗口模式 / 4 档分辨率 / 导入题库 / 清除题库）、统计。
  - 快捷键：`1`–`9` 打出手牌、`Enter`/`Space` 结束回合（仅在无弹窗且处于玩家回合时响应）。
  - 美术加载统一走 `Spr(path)`：**自动去掉 `.png` 后缀**，因为 `Resources.Load` 的路径不带扩展名。
- **`CardDragHandler.cs`**：手牌拖到 `dropZone`（面试官区）出牌，未命中则回原位；与点击出牌并存。

### 6.4 编辑器层（Assets/Scripts/Editor，全部 `#if UNITY_EDITOR`）

- `ProjectBootstrap`：自动建场景并写入构建列表。
- `ArtImporter`：`AssetPostprocessor`，把 `Resources/Art/` 下 PNG 统一设为 Sprite（Single、PPU 100、无 mipmap、Uncompressed），另带 `Tools/Interview Spire/Reimport Art` 菜单。
- `SmokeTest`：命令行冒烟测试入口（见 4.2）。

## 7. 开发约定

- **改动只发生在 `InterviewSpireUnity/`**；根目录的 exe 与 `InterviewSpire_Data/` 是产物，改了源码要重新 Build 才生效。
- **数值与文案一律改 `game_data.json`**，不要把平衡数值硬编码进 C#；代码里已有的 `LegacyEffect` 属于历史遗留。
- **不加预制体、不加场景依赖**：UI 继续用 `GameUI` 的代码式搭建（`Place` / `PanelAt` / `StretchAt` / `ButtonAt` / `Label` / `Modal` 等辅助方法），保持「打开工程按 Play 就能跑」。
- **美术资源**：PNG 放入 `Assets/Resources/Art/<分类>/`，代码里用不带扩展名的相对路径（如 `Spr("Cards/xxx@v1")`）；新增目录无需改导入设置。
- **中文注释、中文 UI 文案**：现有代码注释与日志均为中文，保持风格一致；标识符用英文。
- **序列化约束**：新增存档字段用 public 简单类型/`List<T>`，避免字典与接口；`GameState` 变更需考虑对 `saves/*.json` 的向后兼容（`SaveSystem.Read` 对 `s->enemy == null` 有兜底判断）。
- **编辑期专用逻辑**必须包在 `#if UNITY_EDITOR` 中，否则打包会失败。
- **命名空间**统一 `InterviewSpire`（编辑器 `InterviewSpire.Editor`）；单例访问用 `GameController.Instance`。
- 修改后至少跑一次 4.2 的 `SmokeTest.Run`，并在编辑器里 Play 验证界面与动画未回归。

## 8. 已知坑与待修正项

- **README 中的持久化路径已过时**：README 写 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\InterviewSpire\display.json`，实际 `productName` 为 `InterviewSpireUnity`，真实目录是 `...\DefaultCompany\InterviewSpireUnity\`。以本文件第 5 节为准。
- **README 与代码对「帮助弹窗」的描述不一致**：`ShowHelp()` 文案仍写着「Unity 版 MVP 使用点击操作」，但拖拽（`CardDragHandler`）与快捷键均已实现。
- `GameUI.ShowReward()` 的候选池是 `OrderBy(c => c.key).Take(3)`——固定顺序、恒定三张，不是随机抽奖，属于待改进点。
- `GameController.ExhaustPile()` 直接返回 `State.discard`，即 `exhaust` 卡并未真正移出牌局。
- `Screen.fullScreen` 在编辑器内不会真正切换（`DisplaySettings.Apply` 有意跳过），验证窗口行为需用构建产物。
- 存档只能在玩家回合写入；退出时由 `GameUI.Exit()` 自动存档。强制关闭进程不会自动存档。
- `Logs/Editor.log` 与 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\InterviewSpireUnity\Player.log` 是排查启动/构建失败的第一手材料。

## Codely Added Memories
- 当前项目默认使用中文与用户交流；除非用户明确要求其他语言，否则请用中文回答。

---
