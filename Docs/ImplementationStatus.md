# 实现状态与阶段交接

## 2026-10-19：独立编辑器退出入口

用户反馈 Mac 构建可以编辑和保存，当前以全屏运行。右侧配置滚动栏底部新增“退出编辑器”，与常用编辑操作分隔，始终复用确认弹窗；存在未保存编辑或未应用输入时明确提示丢失风险。取消保留草稿，确认后独立构建调用 Application.Quit，Unity Editor 中结束 Play Mode。试玩期间需先返回编辑。

验证：实际 Unity DLL 的相关 C# 独立编译通过，git diff --check 通过；新增按钮布局、确认取消和实际退出仍需重新构建后手动验证。用户现有关卡修改未纳入此步骤。

更新：2026-10-19

## 当前总览

阶段 7 首版流程已实现并通过 Unity Play Mode 场景集成检查，详见文末新增记录；独立平台构建仍待验证。用户已反馈关卡编辑器测试暂未发现问题、Mac 独立包可以编辑保存。此前阶段历史如下。

阶段 1 已通过；阶段 2、3 已实现，用户于 2026-10-17 确认测试过 JSON 保存，`Assets/StreamingAssets/Levels/002.json` 为用户新建关卡。该确认不扩大为重载、全部交互或视觉验收均通过。阶段 4 共用玩法、阶段 5 撤销／重置／输入／HUD 的代码和独立检查已完成；用户已确认阶段 5 可玩，菜单与手动清单未逐项确认；阶段 6 同场景试玩及有机细胞渲染已实现，15 项新增检查通过；新版 Play Mode 与 Shader 编译／视觉验收待确认。

项目本地根目录为 `/Users/aoi/UnityProjects/taptap2026-emergence`。所有后续新增 Scene 统一放在 `Assets/Scenes`，编辑场景为 `Assets/Scenes/LevelEditor.unity`。`OrganicGridDemo` 保留用于细胞 Shader 表现试验，不作为正式玩法规则。以下阶段 1～3 的验证历史按发生时记录，最新结论以本总览和各阶段补充为准。

## 阶段 1：关卡数据与读写

状态：阶段 1 已通过。用户于 2026-10-15 在 Unity 中执行菜单，确认输出 `Level data checks passed: 47`。后续阶段进度见当前总览。

### 文件与职责

| 文件（项目相对路径） | 职责 |
| --- | --- |
| `Assets/Game/Runtime/Data/Levels/LevelDefinition.cs` | 纯数据模板、稳定类型 ID，不依赖 Core、Gameplay 或 Newtonsoft.Json |
| `Assets/Game/Runtime/Core/IO/AtomicTextFile.cs` | 通用 UTF-8 文件读写；同目录临时文件加替换，失败不主动删除原文件 |
| `Assets/Game/Runtime/Gameplay/Levels/LevelJson.cs` | 严格 JSON 结构检查、序列化与深拷贝 |
| `Assets/Game/Runtime/Gameplay/Levels/LevelRepository.cs` | 桌面关卡目录、安全文件名、保存与加载 |
| `Assets/Game/Runtime/Gameplay/Levels/LevelValidator.cs` | 试玩校验与初始状态提示 |
| `Assets/Game/Runtime/Gameplay/Levels/LevelState.cs` | 独立运行状态、分身来源和快照深拷贝；尚无玩法操作 |
| `Assets/Game/Editor/LevelDataChecks.cs` | 可复现的数据检查菜单 |
| `Assets/StreamingAssets/Levels/level_001.json` | 合法读写示例，不是已验证的正式谜题 |
| `Docs/Tests/run-level-data-checks.sh` | 使用 Unity 附带 Mono 的独立编译与检查脚本 |

新增资源附有稳定 `.meta`。未修改现有 asmdef、DataLoader、GameManager、Demo 或场景。

### 公开入口

命名空间：模板为 `Game.Runtime.Data.Levels`，关卡服务为 `Game.Runtime.Gameplay.Levels`，通用读写为 `Game.Runtime.Core.IO`。

```csharp
// 开发者桌面环境，默认目录 Assets/StreamingAssets/Levels。
var repository = LevelRepository.DeveloperDefault();
LevelDefinition draft = repository.Load("level_001");
LevelValidationResult validation = LevelValidator.ValidateForPlay(draft);
if (validation.CanPlay)
{
    LevelDefinition initial = LevelJson.DeepCopy(draft);
    LevelState state = LevelState.FromDefinition(initial);
}
repository.Save(draft); // 允许目标未配置、缺少玩家等不完整草稿。
```

- `LevelJson.Serialize`／`Deserialize`：字符串入口；未知版本、类型、规则、比较符、字段或损坏结构抛出 `FormatException`。整数不接受字符串、小数、显式 null 或溢出。要求文件提供 schemaVersion=1。
- `LevelJson.DeepCopy`：复制所有模板子对象与列表，不与编辑草稿共享引用。
- `LevelRepository.Load/Save/PathFor`：按 ID 操作；非法 ID 抛 `ArgumentException`；文件系统错误按标准 I/O 异常向调用者报告。Load 检查文件名与内部 ID 一致。
- `LevelValidator.ValidateForPlay`：返回 `Errors`、`Warnings`、`CanPlay`。读取草稿不自动判试玩合法，调用方必须使用它；失败读取不会替换调用方的原草稿。
- `LevelState.FromDefinition`：仅接收通过试玩校验的模板，初始细胞均标记为非分身；`DeepCopy` 复制来源标记，`LivingCloneCount` 计算当前存活分身数。

UI 阶段应在 Load 成功后才替换当前草稿，捕获文件与格式异常并展示；不要在异常路径新建空草稿。草稿保存不运行试玩校验，但要求 ID、版本与 JSON 类型可编码。

### 数据合同

- 规则 ID：`lonelyDeath`、`crowdedDeath`。数组中列出即开启；空数组表示都关闭，允许试玩并提示。阈值固定；暂兼容规则条目的空 `parameters: {}`，非空参数报告错误。
- 未填写 `cloneLimit` 默认 0，未填写 `iterationLimit` 默认 1,000。非法值不被静默改成默认值，在试玩校验中报告。
- 玩家只在初始 `cells` 中保存一次，`type` 为 `player`；其他初始细胞为 `normal`。分身来源仅属于运行状态，不作为本阶段初始关卡类型。
- 目标只支持 `actorAtPosition`、`livingCellCount`、`livingCellsCoverPositions`，组合仅 `all`，数量比较仅严格 `>`／`<`。来源为分身的运行细胞后续同样计入目标，玩家不计数量。
- 运行状态尚未执行移动、迭代、分身、目标会话或历史管理，不能将这些列为已实现。
- 文件 ID 为 1～64 位英文字母／数字／下划线／连字符，首位字母或数字，并排除 Windows 保留设备名。显示名称可用中文。
- 试玩校验包括尺寸 1～30、坐标／地形合法性、重复坐标、唯一玩家、至少一个完整目标、目标位置、比较符、规则重复与已知类型。分身上限不能超过有效地面数减玩家占位；初始普通细胞之后可死亡，不从总容量永久扣除。
- 初始已满足全部目标与死亡规则均关闭属于提示，不阻止试玩。可达性与谜题质量不在本阶段自动判断。

### 已执行验证

运行 `bash Docs/Tests/run-level-data-checks.sh`：**47 项通过**。

脚本分别编译新增 Data、Core 读写、Gameplay、Editor 检查模块，使用实际 Newtonsoft.Json DLL，并编译 UNITY_EDITOR 菜单及 Unity 默认路径分支。随后通过 Mono 执行数据检查；这不等于 Unity 对整个项目的编译。

检查覆盖保存回读和覆盖替换、默认值、损坏 JSON、重复属性、未知版本／字段／规则、整数类型／溢出、非法地形与目标、空目标、数量严格比较及排除玩家、警告、文件名约束、失败保存保留旧文件、草稿保存与加载、深拷贝和分身来源隔离。检查使用系统临时目录，不修改实际关卡文件。

### Unity 验证历史与使用方法

Unity 2022.3.62f3 批处理启动尝试停在 LicensingClient 连接，未运行检查菜单，已停止该次启动。日志：`/private/tmp/level-data-checks/unity-validation.log`。当时未进行 Unity Play Mode、场景或 WebGL 测试。随后用户确认 Unity 菜单的 47 项数据检查通过。

在 Unity 中打开项目，等待导入与编译后，执行 **Tools → Levels → Run Data Checks**。成功时 Console 显示 `Level data checks passed: 47`；如项目编译错误，应先解决错误再执行。本阶段没有新增 Scene 或 UI。

WebGL／Android 的 StreamingAssets 读取需要后续平台适配；`DeveloperDefault` 是开发者桌面文件仓库，不用于这些平台直接文件读写。正式游戏索引与批量关卡列表也属于后续流程阶段。

### 下一阶段输入

阶段 2 使用 `LevelDefinition` 作为草稿数据，`LevelJson.DeepCopy` 复制草稿，`LevelRepository` 读写；`LevelState` 留给玩法／试玩。先复查 Unity 编译和上述菜单，再实现棋盘绘制、坐标拾取与单格编辑，不增加新的自动繁殖规则。


## 阶段 2：棋盘显示与基础编辑

状态：代码、独立编译与逻辑检查完成；完整 Unity Play Mode 视觉与点击验收尚未记录；阶段 3 已实现，见下文。

### 新增模块与接口

- `Runtime/Gameplay/LevelEditor/LevelEditorSession.cs`：唯一内存草稿；`Draft`、`Changed`、`New`、`ReplaceDraft`、`Resize`、`TryPaint`。无 Unity 场景依赖，ReplaceDraft 和 Resize 通过深拷贝接入阶段 1。
- `LevelBrush`：Floor、Wall、Normal、Player、EraseCell、EraseTerrain、ActorGoal、CellGoal、EraseGoals。UI 点击调用 TryPaint，返回成功／拒绝原因。
- `LevelEditorSession.TryPickNormalized`：左下为零，右／上边界排除，映射到整数格；BoardView 先用 RectTransformUtility 从指针屏幕坐标转换到棋盘局部坐标。
- `LevelEditorBoardView.cs`：UI 方格临时配色和 P/C/G/T 标记；只有整张棋盘接收指针，子格和文字不消耗 raycast。宽高比固定为网格列／行比例，FitInParent 保持方格比例。
- `LevelEditorScreen.cs`：运行时 Canvas、画笔、尺寸、新建、提示；使用项目中文 TMP 字体与 InputSystemUIInputModule，输入框和按钮不会穿透到棋盘。
- `Editor/LevelEditorSceneSetup.cs`：Tools → Levels → Create Level Editor Scene，目标 `Assets/Scenes/LevelEditor.unity`；由用户执行创建，不手写场景 YAML，不覆盖已有场景，脏场景先走 Unity 保存提示。
- `Editor/LevelEditorChecks.cs`：Tools → Levels → Run Editor Checks。

新建默认 10×10 全地面，两个死亡规则默认打开，分身额度 0；仍是草稿，不自动放玩家或通关目标。目标画笔是基础拾取入口：角色目标写入首个角色位置条件，细胞目标写入首个覆盖列表；多条件选择和字段面板由阶段 3 接入。

改变尺寸会整体替换 Draft 对象，后续 UI 不能缓存旧列表引用，应订阅 Changed 后重新读取 Session.Draft。Draft 的外部字段编辑完成后刷新通知机制在阶段 3 统一；当前 UI 仅通过 Session 修改。缩小时保留被裁空的目标条件（坐标置空／列表清空），交给试玩校验提示，而非静默删除通关要求。

### 验证结果

- `bash Docs/Tests/run-level-data-checks.sh`：47 项读写检查、33 项编辑逻辑检查全部通过。
- `python3 Docs/Tests/check-level-editor-compilation.py`：新 Runtime UI、Input System、TMP 与 Editor 场景工具使用本机实际 Unity DLL 编译通过。
- 33 项逻辑检查覆盖画笔互斥、唯一玩家、分层清除、重复目标、内部空洞、5→6／6→5／6→4 以及混合轴缩放、裁切不恢复、无效尺寸与失败替换保持原草稿、点击边界和 30×30 网格。
- 中文字体字符表已检查，当前界面源码中的汉字均存在于所选字体图集中。
- 自动化连接 Unity 后未显示新菜单，截图不可用，未运行新场景或验证可视点击。不能将独立 C# 编译视为 Unity 内场景验收；下一步按快速说明复查。

操作与手动验收见 [LevelEditorQuickStart.md](/Users/aoi/UnityProjects/taptap2026-emergence/Docs/LevelEditorQuickStart.md)。阶段 2 当时没有保存 UI；现已由阶段 3 接入保存、仓库、规则与额度、条件参数面板。退出 Play 仍会丢失未保存草稿，正式谜题需待玩法验证。


## 阶段 3：编辑器配置与文件操作

状态：代码、独立 C# 编译和配置服务检查完成。2026-10-17 用户确认已测试保存 JSON，并新建 `002.json`；重载及其余新版界面点击与视觉验收未逐项确认。阶段 4 已实现，见下文。

### 文件与接口

- `LevelEditorDocument`：衔接 Session 与 Repository，管理 SavedId／IsDirty。Load 成功且草稿可编辑后才替换当前数据；Save／SaveAs 先写文件，成功后更新当前文件身份。提供 New、Load、Save(id,name,saveAs)、Dispose。
- `LevelRepository.ListIds()`：返回目录内合法 JSON 文件 ID，排序；目录不存在返回空，不为列文件创建目录。
- `LevelEditorSession`：增加 TryConfigure、SelectedGoalIndex、SelectGoal、AddGoal、RemoveSelectedGoal、TrySetCountGoal、ClearSelectedGoalPositions 与 EraseSelectedGoal 画笔。位置画笔优先写入所选同类型条件，多目标不再都写入第一个列表。New 清空选择，Load 默认选第一个目标。
- `LevelEditorScreen.Configuration.cs`：partial UI，包含顶部文件栏、右侧可滚动配置区、已有文件列表、目标编辑、校验结果与丢弃／覆盖确认弹窗。共用主 Screen 与场景入口，无需重新创建 Scene。
- `LevelConfigurationChecks.cs`：Tools → Levels → Run Configuration Checks，预期 34 项通过。

### 已实现行为

- 保存允许不完整草稿，成功显示实际文件路径及校验错误数量。非法 ID、数字、未应用尺寸输入不触发隐式裁切或静默修正。
- 修改现有关卡 ID 后须另存为；另存为新 ID 不覆盖原关，也拒绝覆盖已存在的目的文件。新草稿首次保存到已存在 ID 时先确认。
- 新建／加载有未保存数据或未应用输入时弹出确认；失败加载不清空草稿。保存、加载及应用配置后刷新相关显示。
- 两种死亡规则、分身与迭代上限、严格数量比较、角色终点与多位置覆盖均可编辑；全部目标同时满足，不提供 any。
- 目标坐标必须点击 floor；删除单个格子、清空本目标坐标、删除整个条件分开处理。
- 数据校验显示全部错误与警告，初始满足和两规则关闭不会禁止保存。
- 此阶段只负责编辑与持久化，没有试玩按钮。分身／迭代逻辑已在阶段 4 新增，现由阶段 5 接入独立 Gameplay HUD；编辑器内试玩仍留给阶段 6。

### 已执行验证

- 47 项读写检查、33 项编辑检查、34 项新增配置检查全部通过。
- Runtime UI、TMP/Input System 与 Editor 菜单的实际 DLL 独立编译通过，无编译警告。
- 新增检查覆盖默认值、非法配置不改数据、所选目标隔离、空目标草稿、保存回读、同 ID 覆盖、另存保留原文件、拒绝已有目的文件、改 ID 必须另存、失败读写保留草稿与文件、默认目标选择和源文件列表。
- 2026-10-16 交付时连接的 Unity 正运行旧阶段 2 的 Play Mode，棋盘有用户尚未保存的内容。为避免丢失该内存草稿，未退出 Play 或触发重载；没有把旧界面截图当作新版视觉验证。
- 用户新增的字体资源、TitleScene 与 LevelEditor 场景改动原样保留；未提交或推送此次代码。

使用与手动验收见 [LevelEditorQuickStart.md](/Users/aoi/UnityProjects/taptap2026-emergence/Docs/LevelEditorQuickStart.md)。用户已确认保存可用并授权进入阶段 4；重新加载及其他手动验收仍以该说明中的清单补验。


## 阶段 4：共用玩法逻辑

状态：2026-10-17 代码、独立 C# 编译和 63 项玩法检查完成；未在 Unity 内执行新增菜单，未进行 Play Mode、场景或 WebGL 验证。阶段 5 已实现，见下文。

### 文件、接口与依赖

- `Assets/Game/Runtime/Gameplay/Levels/LevelPlaySession.cs`：无 Unity 场景依赖的共用规则会话，命名空间 `Game.Runtime.Gameplay.Levels`。依赖已有 LevelDefinition、LevelValidator、LevelJson、LevelState，不修改 Data/Core 分层。
- `LevelPlayStatus`：Playing／Won／Failed；`LevelInteractionMode`：Move／PlaceClone／RecycleClone。
- `new LevelPlaySession(definition)`：校验后深拷贝关卡；非法或空目标草稿抛出 ArgumentException；构造时检查初始目标，可能直接 Won。
- 操作入口：`TryMove(dx,dy,out message)`、`TryBeginClonePlacement(out message)`、`TryCreateClone(x,y,out message)`、`TryBeginRecycling(out message)`、`TryRecycleClone(x,y,out message)`、`TryIterate(out message)`，返回成功与英文原因／结果文本。HUD 中文呈现留给阶段 5。
- `CancelSelection()`：取消分身／回收选择；`CanPlaceCloneAt(x,y)`：给后续高亮提供合法性查询，不要求先进入模式。
- `State`、`Definition` 每次返回深拷贝，外部不可直接修改会话真值；只需计数时用 `LivingCloneCount`、`Iteration`、`CloneLimit`、`IterationLimit`，避免重复复制。
- `Assets/Game/Editor/LevelPlayChecks.cs`：菜单 **Tools → Levels → Run Play Checks**，预期 `Level play checks passed: 63`；两个新 C# 文件均附 `.meta`。
- `Docs/Tests/run-level-data-checks.sh` 与 `check-level-editor-compilation.py` 已接入新会话与检查。

### 已实现行为

- 移动只接受四向一格；墙、无地块、边界、存活细胞均阻挡；不留下普通细胞，不增加迭代轮数。
- 每轮从完整旧状态统计八邻居，再一起删除死亡细胞；两个死亡开关独立生效，无自动繁殖。玩家计入邻居且永不因死亡规则消失；边界不循环。
- 分身需要先进入放置模式，玩家八邻域合法空地可放置。禁用、满额、没有合法邻格分别拒绝；非法选择保留模式，合法创建一个后回到 Move。
- 回收需要先进入回收模式，允许全棋盘选择存活分身，拒绝初始普通细胞。回收与死亡即时释放并发名额，存活分身保留来源标记。
- 放置和回收模式期间拒绝移动与迭代；CancelSelection 回到 Move，不修改棋盘。
- 构造及每次有效移动／创建／回收／迭代后检查当前全部目标，不累计历史达成标记。数量与覆盖包含分身、排除玩家；数量仅严格 >／<。
- 到达迭代上限仍 Playing，允许移动／分身／回收并通关；再次尝试迭代才 Failed，不改变布局、轮数或额度。Won／Failed 后所有棋盘操作拒绝，不直接打开 UI。
- 本阶段未接入键盘、按钮、棋盘渲染、撤销、重置或试玩；没有新增 Scene，没有更改 Shader Demo 或用户的 `002.json`。

### 实际执行的验证

- `bash Docs/Tests/run-level-data-checks.sh`：47 项数据、33 项编辑、34 项配置、63 项玩法检查全部通过，共 177 项。
- `python3 Docs/Tests/check-level-editor-compilation.py`：使用本机 Unity 2022.3.62f3 实际 DLL 编译相关 Data、Core 读写、Runtime 编辑器／玩法及 Editor 菜单／场景工具通过。
- 玩法检查包含旧状态同步死亡、玩家补足第二／第四邻居、规则独立关闭、无繁殖、不循环、非法动作不改变棋盘、模式锁定、分身来源／名额、全局回收、三类目标和严格比较、同一状态全部成立、初始通关、最后一轮胜利及超限失败、会话与草稿／返回副本隔离。
- 上述独立检查不代表 Unity 对整个项目编译或 Play Mode 验收；本次没有操作运行中的 Unity，也未覆盖用户未保存的内存草稿。

### Unity 中如何使用与下一阶段入口

本阶段没有可玩的场景 UI。在 Unity 等待编译后执行 **Tools → Levels → Run Play Checks** 即可复核规则；详细代码用法和手动确认项见 [LevelPlayQuickStart.md](/Users/aoi/UnityProjects/taptap2026-emergence/Docs/LevelPlayQuickStart.md)。

阶段 5 应继续使用同一个 LevelPlaySession，在会话内集中协调快照、撤销、重置与初始副本，新增受控恢复入口；不要让 UI 修改 State 副本。只为成功迭代保存迭代前快照，最近 100 份；失败超限、移动、分身、回收均不新增记录。Z 恢复来源、玩家、轮数并丢弃此后的变化；R 从初始关卡恢复并清空历史。恢复后清除选择模式、重新检查目标，清除失败状态。

阶段 5 再接入 Input System 单帧单次确定操作和 HUD：分身／回收的开始点击与棋盘选格必须分开处理；暂停、弹窗等进入时 CancelSelection 并阻断输入。新增 Scene 一律放 `Assets/Scenes`。阶段 6 才接编辑器内试玩和正式首个测试谜题，不将 002 自动认定为已验证的谜题。


## 阶段 5：撤销、重置与试玩 HUD

状态：2026-10-17 代码、44 项新增历史／输入检查及相关模块独立编译完成。用户已确认阶段 5 可以玩。现已进入阶段 6（见下文），新版同场景试玩和 Shader 的视觉验收尚未完成。

### 文件、公开接口与依赖

- `Runtime/Gameplay/Levels/LevelPlaySession.cs`：新增 `HistoryCapacity = 100`、`HistoryCount`、`CanUndo`、`TryUndo(out message)`、`Reset()`。独立初始状态、实时状态与历史均深拷贝隔离；每次成功迭代才保留其前快照。达到容量移除最早项。
- `Runtime/Gameplay/Levels/LevelPlayController.cs`：无 Unity 依赖的输入仲裁。`LevelPlayRequest` 含 Command／X／Y；`ProcessFrame(frame, requests, out message)` 每帧最多处理一个请求，包括失败请求，不会继续执行第二个。Session 和 Paused 为只读对外入口。
- `Runtime/Gameplay/LevelPlay/LevelPlayScreen.cs`：Input System 键盘、按钮、模式、计数、目标、暂停／胜利／失败覆盖层及中文结果提示。`levelId` 默认 002，`uiFont` 指定字体，`loadOnStart` 默认 true。`Initialize(LevelDefinition)` 先创建合法新会话再替换，可供阶段 6 使用未保存的深拷贝；失败不会替换原会话。
- `Runtime/Gameplay/LevelPlay/LevelPlayButton.cs`：忽略 UI Submit，避免空格重复触发上次聚焦按钮；空间键只走共用输入入口。
- `Runtime/Gameplay/LevelEditor/LevelEditorBoardView.cs`：新增 `RenderPlay(definition, session, font)`，复用棋盘构建、比例适配与坐标拾取。P 玩家、C 初始普通细胞、D 分身，放置／回收合法位置高亮，G／T 保留目标标记。未改变原有编辑 Render 路径。
- `Editor/LevelPlaySceneSetup.cs`：菜单 **Tools → Levels → Create Gameplay Scene**，生成 `Assets/Scenes/Gameplay.unity`；已有同名场景拒绝覆盖，脏场景先提示保存，Play 中拒绝生成。优先使用现有动态中文 TMP 字体，不修改用户字体资源。由用户执行菜单，不手写 YAML。
- `Editor/LevelHistoryChecks.cs`：菜单 **Tools → Levels → Run History Checks**，预期 `Level history checks passed: 44`。
- 两个独立验证脚本均接入新代码，全部新增脚本和目录附 `.meta`。未修改 Data／Core 程序集、002.json、Shader Demo、TitleScene 或用户资源。

### 已实现行为与输入合同

- Z 恢复迭代前的完整棋盘、玩家、来源与轮数，同时丢弃该轮后的移动、分身和回收；不提供重做。无历史时拒绝且不改变状态。
- R 恢复独立初始状态、清空历史与失败、返回 Move。撤销／重置后重新检查当前目标；初始已满足目标时 Reset 仍为 Won。
- 超限失败不添加历史，Won／Failed 可以通过 Z／R 恢复；恢复成功后 HUD 隐藏对应覆盖层。无历史时撤销按钮禁用，Z 可给出历史起点提示。
- 键盘只读 wasPressedThisFrame，长按不连发。UI 和键盘请求在 LateUpdate 汇总，每帧按固定优先级：Reset → Undo → Cancel → Pause → Clone → Recycle → Iterate → 上 → 右 → 下 → 左 → Select。同优先级保留首个请求，其余丢弃；最高优先级操作失败也不继续执行后续请求。
- 分身／回收选择中只接受选格、取消或暂停；R／Z 与其他玩法按钮须先取消选择。开始模式与选格同帧时只开始模式，不创建／回收。非法位置保留模式；成功一次后返回 Move。
- Esc 在选择中取消，在移动中暂停，在暂停中继续。暂停会取消未完成选择；暂停期间只接受取消／继续，不执行 R／Z 或棋盘操作。胜利／失败时只接收 Z／R。
- 按钮显示目标、轮数、分身数、历史数；禁用、满额、无合法邻格、无分身等使用明确中文反馈。右侧 HUD 可滚动；棋盘保持等比例，覆盖层拦截结算／暂停时棋盘点击。
- Screen 失焦或禁用清空请求并取消选择；销毁时解除棋盘事件并清理自建 Canvas／EventSystem。不会修改已有 EventSystem 或正式进度，不保存游玩状态到 JSON。

### 实际完成验证

- `bash Docs/Tests/run-level-data-checks.sh`：47 数据 + 33 编辑 + 34 配置 + 63 玩法 + 44 历史／输入 = **221 项通过**。
- `python3 Docs/Tests/check-level-editor-compilation.py`：本机 Unity 2022.3.62f3 实际 DLL 独立编译相关规则、编辑器 UI、试玩 HUD、Input System、TMP、Editor 检查和两个场景构建工具通过。
- 新检查覆盖第 0 轮不撤销、非法操作不留历史、A／B 分身与玩家回退、来源恢复、死亡分身恢复、连续多轮与分支、初始副本隔离、101 轮容量边界和玩家位置、超限失败后 Z／R、胜利后撤销、初始胜利重置、输入优先级、同帧重复派发、失败请求不穿透、模式点击分离、选择／暂停锁定与恢复。
- 没有在 Unity 内执行菜单、生成场景或进行 Play Mode；不将独立编译和纯逻辑检查记作视觉验收。桌面文件仓库仍不适用于 WebGL 直接读取，此阶段不做平台适配。

### Unity 操作与下一阶段交接

见 [LevelPlayQuickStart.md](/Users/aoi/UnityProjects/taptap2026-emergence/Docs/LevelPlayQuickStart.md)：先执行 History Checks，再执行 Create Gameplay Scene，默认读取用户的 002；其 cloneLimit=0，分身测试须另存一张有分身额度的草稿，勿覆盖原关。

阶段 6 使用同一个 LevelPlayScreen／LevelPlaySession，设置 loadOnStart=false，通过 Initialize 传入校验后的草稿副本。编辑草稿、试玩初始数据、运行状态和历史保持隔离；关闭试玩销毁其 Screen／生成对象，恢复原编辑草稿及视图，不能从磁盘重载替代。需要额外协调编辑界面的键盘焦点／EventSystem、绘制锁定、重复进出清理；本阶段未新增编辑器试玩按钮，也未制作带通关操作序列的正式测试谜题。


## 阶段 6：同场景编辑／试玩与 Organic 细胞表现

状态：2026-10-17 用户确认阶段 5 可玩，并明确要求编辑与试玩在同一个场景，正式 Gameplay 复原 OrganicGridDemo 的 Shader 表现。代码和 15 项新增逻辑检查完成，相关 C# 独立编译通过；未执行新版 Unity Play Mode 或 Shader 编译菜单，不能视作视觉验收通过。原 Demo 文件保持不动。

### 文件与公开入口

- `Runtime/Gameplay/LevelEditor/LevelEditorScreen.Playtest.cs`：新增 partial。左侧“试玩当前草稿”校验并复制当前内存数据，不保存 JSON、不加载或切换 Scene。规则／数量输入采用已有应用逻辑；未应用尺寸须先明确调整。缺少玩家、目标、合法 ID 等继续编辑并显示原因。
- 试玩期间停用编辑 Canvas，保留编辑会话、画笔、目标选择、输入和滚动位置；新建子对象上的 LevelPlayScreen，设置 loadOnStart=false、showReturnToEditor=true，再 Initialize(candidate)。共用已有 EventSystem，清除输入框焦点。
- LevelPlayScreen 新增 `showReturnToEditor` 与 `ExitRequested`。HUD 的“结束试玩 · 返回编辑”在移动、选择、暂停、胜利和失败时都可使用；立即停用输入和试玩对象，再延迟销毁，恢复编辑 Canvas。不从磁盘重载、不将运行状态写回草稿，不更改 SavedId／正式进度。
- `Runtime/Gameplay/LevelPlay/OrganicLevelBoardView.cs`：共用棋盘渲染与坐标拾取。LevelEditor 内试玩、独立 Gameplay 均使用它；编辑态仍保留易辨识的方格画笔显示。
- `Assets/Resources/LevelPlay/OrganicLevelBoard.shader`：从原 Demo 的 smooth-min 融合、淡色半透明膜观感、粉白边缘、光晕和深色细胞核做一般化适配。不依赖 Demo 的规则脚本，不引入自动繁殖。
- 用两张线性 Point／Clamp RGBA32 状态纹理传入地形、目标、来源、存活和高亮；片元采样附近 5×5 格，替代固定 7×7／49 项数组，支持 1～30 尺寸及不规则地形，不按全棋盘逐细胞遍历每个像素。
- 玩家粉色，普通细胞浅蓝，分身深蓝；金环为角色目标、紫环为覆盖目标、黄框为合法选择。移动过渡 0.2 秒并保留收缩尾部，细胞变化 0.55 秒；新分身膜从旧邻居平均位置长出，核稍后出现，死亡核与膜一起收缩。
- 动画期间屏蔽普通移动、迭代和选择，仍允许取消、暂停、撤销、重置及返回编辑。暂停冻结表现，撤销／重置直接显示恢复后的准确状态；胜败覆盖层等待动画结束。第一帧进入试玩不会处理同帧遗留按键。
- `Editor/LevelPlaytestChecks.cs`：**Tools → Levels → Run Playtest Checks**（预期 15 项）；另有 **Tools → Levels → Check Organic Shader**，在实际 Unity 图形环境检查 Shader 支持、首个 Pass 和编译错误。
- 新增 `Assets/StreamingAssets/Levels/stage6_probe.json`，不修改用户 002。可在编辑器中加载后直接试玩；操作序列见下方表及快速说明。所有新资产附 `.meta`。

### 测试关卡与可复现操作序列

4×2，不规则地形；玩家 `(0,1)`，初始普通细胞 `(0,0)`、`(2,1)`；上排贯通，下排仅 `(0,0)`、`(1,0)` 有地面。两死亡规则开启，分身额度 1、迭代上限 2。要求玩家到达 `(3,1)` 且普通生命细胞覆盖 `(1,0)`。

| 操作后 | 迭代轮数 | 存活分身 | 关键状态 |
| --- | --- | --- | --- |
| 初始 | 0 | 0 | `(2,1)` 存活细胞阻挡通道，覆盖目标为空 |
| 分身按钮 → 点 `(1,0)` | 0 | 1 | 玩家保持 `(0,1)`；初始普通细胞仍有两个 |
| Space 迭代 | 1 | 1 | `(0,0)` 及分身 `(1,0)` 保留，`(2,1)` 因孤独死亡；共 2 个非玩家细胞 |
| 右 → 右 → 右 | 1 | 1 | 玩家到 `(3,1)`，分身仍覆盖 `(1,0)`，全部目标同时满足 |
| Z（可选验收） | 0 | 1 | 恢复迭代前玩家 `(0,1)`、两个初始普通细胞和分身 |
| R（可选验收） | 0 | 0 | 恢复本次试玩起点，普通细胞两个，历史清空 |

站在 `(1,1)` 再迭代会使 `(2,1)` 因玩家和分身提供两个邻居而保留，验证站位会影响通道。未迭代不能穿过存活阻挡；没有分身不能满足覆盖目标。该关验证最小操作循环，不代表正式关卡质量或批量内容完成。

### 实际验证与未完成项

- `bash Docs/Tests/run-level-data-checks.sh`：47 数据 + 33 编辑 + 34 配置 + 63 玩法 + 44 历史／输入 + 15 试玩／关卡 = **236 项通过**。
- `python3 Docs/Tests/check-level-editor-compilation.py`：相关模块包含新的 partial、Organic 渲染脚本、HUD 和 Shader 菜单工具，使用实际 Unity DLL 独立 C# 编译通过。它不编译 GPU Shader。
- 新检查使用提交的 JSON，验证可通关操作序列、错误站位、迭代前阻挡、Z／R、运行与原始草稿／候选／文件身份隔离、反复创建会话及非法草稿不破坏编辑数据。
- 静态字体字符表检查：新增试玩与 HUD 界面的汉字均存在于现有静态中文字体中；未修改字体资产。
- UI 连接 Unity 超时，未直接操作其运行状态、菜单或当前场景；未执行 Shader 编译检查、截图或 Play Mode。需按快速说明核对膜融合／拖尾／收缩、首次进入、反复进出与对象／回调清理、30×30 性能和坐标。未进行 PC/WebGL 构建与平台验证。

### 后续交接

先在既有 LevelEditor 场景 Play，加载 stage6_probe 或编辑未保存草稿，点击“试玩当前草稿”，完成后“结束试玩 · 返回编辑”，确认草稿和场景均保持。已有 Gameplay 场景无需重建，运行时自动使用同一 Organic 视图。入口与手动清单见 [LevelPlayQuickStart.md](/Users/aoi/UnityProjects/taptap2026-emergence/Docs/LevelPlayQuickStart.md)。

阶段 7 的标题／人体选关／结算／存档尚未接入，产品选项仍需按 DevelopmentPlan 在该阶段前定案。当前 Organic 为 Demo 表现的一般化实现，GPU 实际编译和与原 Demo 的视觉对照须在 Unity 确认后再标记通过。


### 阶段 6 补充：B（Birth）分身快捷键

2026-10-17：按用户要求新增 B 键，使用 Input System 的 bKey.wasPressedThisFrame，将 Clone 请求交给原 LevelPlayController，与分身按钮共用入口、限制与同帧优先级。按一次进入分身放置模式，点击合法空格创建一个；长按不连发。编辑器内试玩和 Gameplay 均生效，选择／暂停／胜败及输入框焦点限制沿用既有逻辑。HUD 按钮与操作说明已同步更新。


### Git 交接约定

2026-10-17 用户要求每一步完成后进行 Git 提交。后续按步骤验证并提交，提交范围限于对应功能与文档，不混入用户其他在途修改。本次将此前已完成的阶段 4～6、场景目录／本地路径修正和 B 快捷键统一补交，不能追溯声称它们曾逐步提交。B 快捷键相关 C# 独立编译已通过。

2026-10-17 补充：用户要求美术资源单独提交。本次功能提交包含共用玩法、同场景试玩、Organic 渲染代码／Shader、测试和交接文档，以及生成的 Gameplay 测试场景；UI 图片、字体图集及标题界面美术调整另作一次提交。其余无关在途改动保留。


### 编辑器文件选择自动加载

2026-10-17：按用户要求，已有文件下拉选择后立即开始加载流程，移除“加载选中”按钮。选择默认项不加载；程序刷新列表用防重入标志及 SetValueWithoutNotify，不触发加载。先在独立临时会话验证文件可读取／编辑，损坏或删除时直接刷新并选回“请选择已保存的关卡”，不替换当前草稿。有未保存内容仍保留原确认流程，取消后回到默认项；确认后重读文件，处理弹窗期间文件被删除或损坏的情况。列表目录读取失败时显示默认项及错误。

验证：相关 UI／Editor 模块实际 Unity DLL 独立编译通过；既有 236 项逻辑检查全部通过。新增下拉事件与弹窗操作未在本次 Unity Play Mode 中执行；手动复核选择自动加载、取消未保存确认、选择后文件被删除／损坏时的默认项恢复。


## 阶段 7：正式标题、人体选关、结算与进度（2026-10-19）

用户确认：启动先到 Title，点击新游戏，之后进入人体选关、局部预览、明确点击进入关卡。开场剧情可省略，本阶段只留插入点。按配置顺序解锁，通关回选关并高亮下一关，不自动进入；末关显示全部完成。保存通关 ID 与音量，不保存局内状态。

### 实现与入口

- Data/Campaign/CampaignCatalog：ScriptableObject 展示目录，按列表顺序控制关卡顺序，按稳定 ID 关联 JSON；包含人体底图、归一化按钮位置、局部预览图与描述。配置实例 Assets/Resources/Campaign/Catalog.asset。
- Gameplay/Campaign/CampaignProgress：无场景依赖的进度逻辑，严格版本／JSON 验证、顺序解锁、重复完成幂等、末关和稳定 ID 重排／暂时移除处理。
- CampaignRuntime：启动注册场景回调；绑定现有 Title 的 NewGame／Setting／ExitGame 按钮；正式关卡读取、胜利订阅、进度 PlayerPrefs 保存和音量设置。仅正式入口给 LevelPlayScreen 设置 formalGame 并订阅，编辑器和直接 Gameplay 不写进度。
- LevelSelectionScreen／CampaignUI：人体图节点、锁定／可进入／完成状态、局部预览、进入与返回、下一关高亮和全部完成提示。缺少局部彩图时暂用裁切着色图。
- LevelPlayScreen：正式模式的暂停／失败面板，重试与返回选关；胜利成立时记录进度，动画结束后返回选关。
- GameManager 的新游戏／标题方法接入正式协调器；GamePhase 末尾追加 LevelSelect，保留旧序列化值。
- 修复实际 Play Mode 发现的 AudioManager 未配置 BGM 时空引用；TransitionManager 用 finally 释放切换状态和遮罩，防止回调异常卡死。切换期间的后续请求等待上次切换完成。
- Editor/CampaignSceneSetup 菜单创建／配置关卡目录及 LevelSelect 场景，已有资源不覆盖；提供正式游戏／策划编辑器两种构建场景列表入口。本机已在 Unity 执行生成，未手写 Scene YAML。

当前展示目录为 stage6_probe → 001 → 002，属于流程验证配置，未声明策划关卡可解性和正式内容排序已验收。Title 美术保持原样；开场对话未制作。“新游戏”目前保留既有完成进度，尚无清档入口。

### 已执行验证

- 既有 236 项 + 新增 29 项进度逻辑检查 = **265 项通过**。
- 使用 Unity 自带 Roslyn 和实际 Unity DLL，Data、相关 Core、全部关卡编辑／玩法／正式流程模块及 Editor 工具独立编译通过；运行时 UNITY_EDITOR 和 Player 两个分支均编译通过。
- Unity 2022.3.62f3 实际编译并执行 Configure Stage 7 Game Flow，生成场景及配置。
- 实际点击验证：Persistent 启动 → Title → 新游戏 → 人体标记 → 局部预览 → Gameplay；暂停面板与暂停重试；失败面板显示撤销／重试／返回选关，点击撤销恢复至第 1 轮并关闭失败覆盖层；标题音量设置界面打开与关闭。
- 实际通关操作期间发现音频空引用及切换标志卡住，已修复；随后 Unity 内执行 Run Campaign Integration Checks (Play Mode) 输出 PASS，覆盖正式场景加载、会话初始化、通关返回、下一关解锁／高亮、已保存进度重新读取。
- 停止并重新进入 Play 后观察 probe 已完成、001 已解锁、002 仍锁定。测试留下 probe 的本机完成记录，未清除任何已有进度。
- Build Settings 中核对正式四场景顺序，最终结束测试，停留 Persistent 非 Play 状态。

### 验证限制与下一步

独立 Mac／Windows／WebGL 构建尚未执行；WebGL 已新增请求读取分支，但浏览器请求、存档持久化和平台 Shader 仍待实包验证。末关 UI 代码边界通过纯逻辑检查，未实际逐关通关全部示例关卡。正式局部彩图、完整 HUD 美术、剧情对话、音效内容仍未完成。操作与配置说明见 Docs/CampaignQuickStart.md。

## 正式 Gameplay 美术界面与流动地块（2026-10-19）

用户要求按提供的浅色 UI 参考图替换正式游戏的测试 HUD：地块有独立形状、轻微边缘流动、渐变填充和棋盘整体阴影；右下演化为最大按钮，另有分身、撤销和感叹号规则说明。

### 修改与入口

- 新增 Data/Presentation/LevelPlayArt 和 Resources/LevelPlay/Presentation.asset，引用现有背景、演化、撤销、stop 与规则 PNG；未改原图。
- LevelPlayScreen 改为 partial，新 Presentation 文件构建正式 Gameplay 界面：左上暂停、顶部关卡进度标记与轮数／额度、左下名称／目标、右下美术操作区。分身使用程序绘制的双细胞标记；保留次级回收入口，重置／返回置于暂停和失败覆盖层。
- 感叹号打开可滚动规则说明，显示当前关死亡规则、玩法说明和完整目标；取消未完成选格，拦截键盘／棋盘输入，冻结表现，Esc 或关闭恢复。
- OrganicLevelBoardView 的 styledTerrain 和 Shader 展示分支：圆角菱形距离场、蓝灰／淡紫渐变、统一形状阴影、约百分之一格幅度的慢速边缘扰动，细胞使用柔和蓝紫／粉白填充。空洞和棋盘外透明，不绘制矩形背景。地块流动在暂停／规则面板期间冻结。
- LevelBoardFrame 只负责实际地形包围盒与阴影留白的展示坐标映射；原关卡整数坐标不变。取景和选格共用一份 frame，避免裁切或加阴影后点击偏移。
- 独立 Gameplay 与正式流程使用新界面；showReturnToEditor 的编辑器内试玩保留原诊断 HUD 和棋盘样式。规则会话、撤销策略和进度隔离未更改。
- 新增 Configure Game Presentation、Capture Game Preview (Play Mode) 菜单；所有新增资源均带 meta。未新增或手写 Scene。

### 已执行验证

- 既有 265 项与新增 12 项取景／选格检查合计 **277 项通过**，包含偏移地形、墙体边界、全格中心映射、阴影留白、30×30 角落、单格、非法指针与原始关卡不变。
- 实际 Unity DLL 的相关 Editor／Player 两分支 C# 编译通过；Unity 中实际编译、运行新界面。
- 本机 Metal 的 Check Organic Shader 检查通过；新版 Shader 实际显示渐变、软阴影、透明空洞及柔化细胞。
- 实际点击 002 的大型演化图标，轮数从 0 到 1；点击撤销恢复 0；stop 打开暂停，点击重新开始能退出暂停并重置。
- 实际打开感叹号规则面板，Space 不推进演化，Esc 返回。新版地图取景之后，在 stage6_probe 点击双细胞分身图标，再点击紫色目标格，分身从 0/1 变为 1/1、目标覆盖从 0/1 变为 1/1，确认画面与逻辑映射一致。
- Unity 中重新运行 Campaign Integration Checks，实际正式加载、通关回选关、下一关解锁／高亮及存档回读输出 PASS。

待验证：Windows／WebGL 实包 Shader、不同窗口比例与 30×30 GPU 性能。本次只确认当前本机 Game 视图，正式美术效果仍可按用户反馈调整。
