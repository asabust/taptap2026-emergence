# 格命 · CellRevolt

一个以细胞演化为核心的回合制 2D 解谜游戏，项目仓库为 `taptap2026-emergence`，现已结项。

玩家操纵一个不会因演化死亡的特殊细胞，通过移动、创建分身和主动推进迭代，改变周围细胞的生存状态，打开通路并完成关卡目标。移动与迭代彼此独立，可以先观察、调整站位，再决定何时推进演化。

本 README 记录结项时的项目结构和接续开发入口。项目包含标题与新游戏／继续游戏、人体章节选关、剧情与教程、章节档案、音量设置，以及可保存 JSON 并直接试玩的关卡编辑器。

## 快速运行

1. 克隆仓库，在 Unity Hub 中添加仓库根目录。
2. 使用 **Unity 2022.3.62f3** 打开，等待 Package Manager 依赖恢复及资源导入完成。
3. 打开 `Assets/Scenes/Persistent.unity`，点击 **Play**。
4. 在标题选择新游戏或继续游戏，经人体总览、章节关卡列表进入游戏。

已有存档时，新游戏会先确认，再重置通关、章节档案解锁和对话／教程阅读记录；音量保留。继续游戏返回选关，**不恢复退出前的局内棋盘**。

仅测试玩法时，可直接运行 `Assets/Scenes/Gameplay.unity`，由 `LevelPlayScreen` 的 `levelId` 指定关卡；这类测试及编辑器试玩均不写入正式通关进度。

### 环境与主要依赖

| 项目 | 版本／说明 |
| --- | --- |
| Unity | 2022.3.62f3，以 `ProjectSettings/ProjectVersion.txt` 为准 |
| 渲染 | URP 14.0.12，2D Renderer，自定义细胞 Shader |
| 输入 | Input System 1.14.2；项目 Active Input Handling 为 Both |
| UI | UGUI、TextMeshPro 3.0.7 |
| JSON | Unity Newtonsoft.Json 包 3.2.2 |
| 其他 | Cinemachine 2.10.6；ExcelDataReader 等 DLL 位于 `Assets/Plugins` |

完整包依赖见 [Packages/manifest.json](Packages/manifest.json)。第一次打开需能下载 Unity 包；无需手动创建场景或重新生成现有 UI。

## 操作与核心规则

| 输入 | 行为 |
| --- | --- |
| 方向键／WASD | 四向移动一格，长按不连续移动 |
| Space | 主动推进一次迭代 |
| B | 进入分身放置模式，再点击合法空格 |
| N | 回收分身：额度为 1 时直接回收已有分身；额度大于 1 时进入点选模式 |
| Z | 撤销最近一次成功的迭代、分身创建或分身回收 |
| R | 重置本关，清空撤销历史 |
| Esc | 取消选格、关闭规则，或切换暂停，依当前界面状态决定 |

正式界面也提供迭代、分身和撤销按钮；回收使用 N。键盘操作前先点击 Game 窗口取得焦点。

- 玩家四向移动；演化按周围八格计算邻居。玩家与分身都贡献邻居数，玩家本体不因死亡规则消失。
- 普通细胞的孤独死亡（邻居少于 2）、拥挤死亡（邻居多于 3）由关卡分别启用。
- 开启 `localBirth` 时，仅玩家八邻域内的有效空地在恰有 3 个存活邻居时诞生普通细胞。关闭时不繁殖；不是全棋盘康威生命游戏。
- 一次迭代统一读取旧状态，再同步应用出生与死亡。墙、缺失地块和棋盘边界不贡献邻居，边界不循环。
- 分身只能放在玩家八邻域内、经四向地形连通的空地。`cloneLimit` 限制同时存活的分身数量；死亡或回收释放名额，初始／繁殖的普通细胞不占名额。
- 目标包括玩家到达指定位置、生命细胞覆盖指定位置、生命细胞数量比较；多个目标必须同时满足。目标数量统计包含分身，不包含玩家。
- 移动、创建和回收不增加迭代数。达到迭代上限后仍能进行这些操作；再次尝试迭代才触发超限失败。
- 撤销最多保留最近 **100 次有效操作**，恢复对应操作前的棋盘、玩家位置、轮数和分身状态；移动不单独记录，但会随快照恢复。R 始终恢复本次关卡初始状态。

## 内容与数据入口

当前正式目录共 **5 章、18 关**：

| 章节 | 内容 | 关卡 ID |
| --- | --- | --- |
| 1 | 新手教程 | `001`、`002` |
| 2 | 基础死亡规则 | `101`–`104` |
| 3 | 分裂细胞 | `201`–`204` |
| 4 | 区域繁殖与额外目标 | `301`–`304` |
| 5 | 综合考验 | `401`–`404` |

| 内容 | 修改入口 |
| --- | --- |
| 地形、细胞、规则、额度、通关目标 | `Assets/StreamingAssets/Levels/<ID>.json` |
| 正式关卡目录与章节配置 | `Assets/Resources/Campaign/Catalog.asset` |
| 剧情、教程、档案及章节／关卡绑定源表 | `Docs/GameData/Database.xlsx` |
| 导出的叙事数据 | `Assets/Resources/Campaign/Narrative.json` |
| 标题、选关、HUD、对话、弹窗布局 | `Assets/Resources/UI/*.prefab` |
| 立绘／档案插图 | `Assets/Resources/UI/Portraits`、`ArchiveImages` |
| 棋盘渲染 | `Assets/Resources/LevelPlay/OrganicLevelBoard.shader` |
| 玩法与选关美术引用 | `Assets/Resources/LevelPlay/Presentation.asset`、`Assets/Resources/Campaign/Art.asset` |
| 音频映射与基础音量 | `Assets/GameData/AudioInfoListSO.asset` |

关卡 ID 是文件、目录、剧情和存档之间的稳定关联键。保留 `001`／`002` 的前导零；新增 JSON 不会自动加入正式选关。

### 制作或修改关卡

1. 打开 `Assets/Scenes/LevelEditor.unity` 并运行。
2. 加载已有关卡或新建草稿，使用画笔编辑地形、墙、玩家、细胞及目标。
3. 配置三个演化开关、分身额度、迭代上限和目标，应用后执行关卡校验。
4. 点击“试玩当前草稿”，在同一场景使用正式玩法会话；返回编辑保留原草稿，试玩结果不写回草稿。
5. 保存到 `Assets/StreamingAssets/Levels/<ID>.json`；复制关卡请使用另存为。退出 Play 前保存，否则内存草稿会丢失。
6. 需要发布到正式流程时，同步源表中的章节成员、顺序及剧情绑定，导出并更新 Catalog，再从正式入口验证。

JSON 使用 `schemaVersion: 1`，主要字段为 `grid`、`tiles`、`cells`、`rules`、`cloneLimit`、`iterationLimit` 和 `winConditions`。棋盘尺寸为 1～30，坐标原点在左下。保存允许不完整草稿，进入试玩则要求通过玩法校验。字段定义见 [LevelDefinition.cs](Assets/Game/Runtime/Data/Levels/LevelDefinition.cs)，校验见 [LevelValidator.cs](Assets/Game/Runtime/Gameplay/Levels/LevelValidator.cs)。

### 更新剧情与 UI

修改源表后，在仓库根目录执行（Python 3，脚本只使用标准库）：

```bash
python3 Docs/Tools/import-ui-database.py Docs/GameData/Database.xlsx
```

该命令更新 `Narrative.json`，不覆盖关卡棋盘 JSON。随后在 Unity 执行 **Tools → UI → Create Editable UI** 同步 Catalog 并创建缺失的 Prefab；已有 Prefab 不覆盖。不要使用旧的 `Tools → Export Database To JSON` 导入这份源表。

UI 布局在停止 Play 后通过 Prefab Mode 修改。保留 `CampaignPanelView`／`GameplayHUDView` 已绑定的组件引用；小关按钮由模板复制，修改模板和布局组件即可。具体方法见 [UI 编辑与交接](Docs/UIEditingQuickStart.md)。

## 代码结构与接续开发

```text
Assets/
  Game/
    Runtime/
      Core/           单例、事件、场景切换、文件 IO、通用 UI 与 Excel 读取
      Data/           关卡数据结构、章节目录、叙事与表现配置
      Gameplay/
        Levels/       规则会话、状态、校验、JSON 与关卡仓库
        LevelPlay/    输入与 HUD 协调、棋盘视图及表现
        LevelEditor/  草稿编辑、配置、保存和同场景试玩
        Campaign/     正式流程、章节、进度、对话、档案与设置
    Editor/           场景／UI 配置工具与检查菜单
  Scenes/             正式入口、选关、玩法与关卡编辑器场景
  Resources/          随包加载的 Prefab、配置、叙事 JSON 与 Shader
  StreamingAssets/    随包关卡 JSON
  Arts/               图片、字体等美术资源
  CellFusionDemo/     细胞融合原型
  OrganicGridDemo/    有机网格表现原型
Docs/                 设计、交接、源表、导入工具及检查脚本
```

| 要继续做什么 | 优先阅读 |
| --- | --- |
| 改演化、移动、分身或撤销规则 | `Levels/LevelPlaySession.cs`、`LevelState.cs`、`LevelPlayController.cs` |
| 改关卡格式、校验或文件读写 | `Data/Levels/LevelDefinition.cs`、`Levels/LevelValidator.cs`、`LevelJson.cs`、`LevelRepository.cs` |
| 改输入、按钮与棋盘反馈 | `LevelPlay/LevelPlayScreen*.cs`、`GameplayHUDView.cs`、`OrganicLevelBoardView.cs` |
| 改正式流程、解锁、存档 | `Campaign/CampaignRuntime.cs`、`CampaignProgress.cs` |
| 改对话、档案、设置与章节 UI | `Campaign/CampaignInterface.cs`、`ChapterScanView.cs` |
| 扩展关卡编辑器 | `LevelEditor/LevelEditorSession.cs`、`LevelEditorDocument.cs`、`LevelEditorScreen*.cs` |

上表中的代码路径相对 `Assets/Game/Runtime/Gameplay`，标有 `Data/` 的路径相对 `Assets/Game/Runtime`。

`LevelPlaySession` 持有玩法真值，正式游戏和编辑器试玩共用它；视图只负责呈现状态，膜融合不代表逻辑细胞合并。修改规则应从会话层入手，并同步对应检查。

Core 与 Data 有独立 asmdef，Gameplay 使用 `Assembly-CSharp`，Editor 工具使用 `Assembly-CSharp-Editor`。引用方向为 Gameplay → Core／Data、Core → Data，避免让 Core 反向依赖具体玩法。两个 Demo 用于原型参考，正式规则以共用会话实现为准。

### 存档

使用 Unity `PlayerPrefs`，不保存局内状态和撤销历史：

| Key | 内容 |
| --- | --- |
| `Campaign.Progress.v1` | 已完成关卡 ID，用于推导关卡／章节解锁 |
| `Campaign.Narrative.v1` | 对话、教程与档案的阅读记录 |
| `MusicVolume`、`SFXVolume` | 音乐／音效音量 |

更改公司名或产品名可能改变 PlayerPrefs 的存储位置，导致旧进度看起来消失；当前产品名为 `CellRevolt`。正式 WebGL 入口使用 UnityWebRequest 读取随包关卡；桌面关卡仓库使用文件 IO，关卡编辑器的保存依赖可写的桌面目录。

## 验证与构建

在仓库根目录执行：

```bash
bash Docs/Tests/run-level-data-checks.sh
python3 Docs/Tests/check-level-editor-compilation.py
```

脚本默认使用 macOS Unity Hub 安装路径 `/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents`，可通过 `UNITY_CONTENTS` 环境变量指定其他安装位置。运行前先在 Unity 打开项目完成导入，使 `Library/PackageCache` 和 `Library/ScriptAssemblies` 中的依赖就绪。这两份脚本按 macOS 路径编写，其他系统需调整路径或使用 Unity 内检查菜单。

本次 README 整理时实际执行结果：**315 项逻辑检查通过**，相关 Editor／Player 分支 C# 独立编译通过。逻辑检查覆盖数据、编辑、配置、玩法、历史／输入、试玩、进度和棋盘坐标；不等于全部正式关卡可解性验收，也不替代 Shader、Play Mode 或平台构建验证。

Unity 内可使用 **Tools → Levels → Run … Checks** 复核各模块，并使用 **Check Organic Shader** 检查当前图形后端。UI／音频检查位于 **Tools → UI → Validate …**。正式流程集成菜单为 **Run Campaign Integration Checks (Play Mode)**；但其脚本仍查找旧 UI 对象名，需要先适配当前 Prefab，不能直接作为本版正式流程验收依据。详见 [流程说明](Docs/CampaignQuickStart.md)。

### 正式游戏打包

1. 安装目标平台的 Unity Build Support。
2. 执行 **Tools → Levels → Use Game Build Scenes**，确认 Build Settings 场景顺序：
   `Persistent → TitleScene → LevelSelect → Gameplay`。
3. 在 **File → Build Settings** 选择目标平台并构建。项目包含桌面与 WebGL 适配；各平台仍需分别验证。
4. 验证标题、继续／新游戏、章节解锁、对话、胜败、音频和存档重启；WebGL 还需验证 HTTP 环境下的关卡加载、浏览器持久化和 Shader。

单独打包策划编辑器可执行 **Use Editor Build Scenes**，构建列表仅包含 `LevelEditor`；之后打包正式游戏前切回正式列表。WebGL 输出应通过本地或远程 HTTP 服务访问。

## 后续维护注意事项

- 先从正式入口跑一次完整流程，再针对需要修改的模块阅读代码与检查；不要把直接打开 Gameplay 的测试视作正式存档验证。
- 新增／改名关卡要同时检查 JSON 文件名与内部 ID、Catalog、源表绑定及旧存档兼容性。
- 修改剧情源表后记得提交导出的 `Narrative.json` 及需要同步的 Catalog；修改 Unity 资源时一起保留 `.meta`。
- 全章节通关、不同画幅、30×30 棋盘性能及各平台实际构建需按发布目标复核。本次文档工作没有重新执行 Unity Play Mode 或平台打包。
- 仓库内未提供统一的开源许可证；复用代码、美术、字体、音频或第三方插件前，请确认相应授权。

## 更多文档

| 文档 | 用途 |
| --- | --- |
| [UIEditingQuickStart.md](Docs/UIEditingQuickStart.md) | UI Prefab、源表、剧情、音频及后期功能更新 |
| [LevelEditorQuickStart.md](Docs/LevelEditorQuickStart.md) | 画笔、保存、配置与试玩操作 |
| [CampaignQuickStart.md](Docs/CampaignQuickStart.md) | 正式流程最初接入与配置说明 |
| [LevelPlayQuickStart.md](Docs/LevelPlayQuickStart.md) | 共用玩法、同场景试玩与原型验证步骤 |
| [GameDesignDocument.md](Docs/GameDesignDocument.md) | 玩法设计与数据约定 |
| [ImplementationStatus.md](Docs/ImplementationStatus.md) | 分阶段实现与历史验证记录 |
| [DevelopmentPlan.md](Docs/DevelopmentPlan.md) | 项目开发阶段规划 |

当前使用指南已按结项仓库核对。早期开发／UI 规划保存在 [Docs/Archive](Docs/Archive/README.md)，实现日志中的历史通过数与待办只代表当时状态。测试样例 `stage6_probe.json` 位于 `Docs/Tests/Fixtures`。
