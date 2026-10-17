# 实现状态与阶段交接

更新：2026-10-16

## 阶段 1：关卡数据与读写

状态：阶段 1 已通过。用户于 2026-10-15 在 Unity 中执行菜单，确认输出 `Level data checks passed: 47`。阶段 2 代码与逻辑检查完成，Play Mode 验证待执行。

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

状态：代码、独立编译与逻辑检查完成；Unity Play Mode 视觉与点击验证尚未完成。阶段 3 未开始。

### 新增模块与接口

- `Runtime/Gameplay/LevelEditor/LevelEditorSession.cs`：唯一内存草稿；`Draft`、`Changed`、`New`、`ReplaceDraft`、`Resize`、`TryPaint`。无 Unity 场景依赖，ReplaceDraft 和 Resize 通过深拷贝接入阶段 1。
- `LevelBrush`：Floor、Wall、Normal、Player、EraseCell、EraseTerrain、ActorGoal、CellGoal、EraseGoals。UI 点击调用 TryPaint，返回成功／拒绝原因。
- `LevelEditorSession.TryPickNormalized`：左下为零，右／上边界排除，映射到整数格；BoardView 先用 RectTransformUtility 从指针屏幕坐标转换到棋盘局部坐标。
- `LevelEditorBoardView.cs`：UI 方格临时配色和 P/C/G/T 标记；只有整张棋盘接收指针，子格和文字不消耗 raycast。宽高比固定为网格列／行比例，FitInParent 保持方格比例。
- `LevelEditorScreen.cs`：运行时 Canvas、画笔、尺寸、新建、提示；使用项目中文 TMP 字体与 InputSystemUIInputModule，输入框和按钮不会穿透到棋盘。
- `Editor/LevelEditorSceneSetup.cs`：Tools → Levels → Create Level Editor Scene，目标 `Assets/Game/Scenes/LevelEditor.unity`；由用户执行创建，不手写场景 YAML，不覆盖已有场景，脏场景先走 Unity 保存提示。
- `Editor/LevelEditorChecks.cs`：Tools → Levels → Run Editor Checks。

新建默认 10×10 全地面，两个死亡规则默认打开，分身额度 0；仍是草稿，不自动放玩家或通关目标。目标画笔是基础拾取入口：角色目标写入首个角色位置条件，细胞目标写入首个覆盖列表；多条件选择和字段面板由阶段 3 接入。

改变尺寸会整体替换 Draft 对象，后续 UI 不能缓存旧列表引用，应订阅 Changed 后重新读取 Session.Draft。Draft 的外部字段编辑完成后刷新通知机制在阶段 3 统一；当前 UI 仅通过 Session 修改。缩小时保留被裁空的目标条件（坐标置空／列表清空），交给试玩校验提示，而非静默删除通关要求。

### 验证结果

- `bash Docs/Tests/run-level-data-checks.sh`：47 项读写检查、33 项编辑逻辑检查全部通过。
- `python3 Docs/Tests/check-level-editor-compilation.py`：新 Runtime UI、Input System、TMP 与 Editor 场景工具使用本机实际 Unity DLL 编译通过。
- 33 项逻辑检查覆盖画笔互斥、唯一玩家、分层清除、重复目标、内部空洞、5→6／6→5／6→4 以及混合轴缩放、裁切不恢复、无效尺寸与失败替换保持原草稿、点击边界和 30×30 网格。
- 中文字体字符表已检查，当前界面源码中的汉字均存在于所选字体图集中。
- 自动化连接 Unity 后未显示新菜单，截图不可用，未运行新场景或验证可视点击。不能将独立 C# 编译视为 Unity 内场景验收；下一步按快速说明复查。

操作与手动验收见 [LevelEditorQuickStart.md](/Users/pomelo/UnityProjects/2026taptap/Docs/LevelEditorQuickStart.md)。当前没有保存 UI，退出 Play 会丢失草稿；不要开始正式关卡制作。阶段 3 将接入已有仓库、规则与额度、条件参数面板。


## 阶段 3：编辑器配置与文件操作

状态：代码、独立 C# 编译和配置服务检查完成；新版 Unity Play Mode 的界面点击与视觉验收尚未完成。阶段 4 未开始。

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
- 分身／迭代／撤销玩法未实现，没有试玩按钮；此阶段只负责编辑与持久化。

### 已执行验证

- 47 项读写检查、33 项编辑检查、34 项新增配置检查全部通过。
- Runtime UI、TMP/Input System 与 Editor 菜单的实际 DLL 独立编译通过，无编译警告。
- 新增检查覆盖默认值、非法配置不改数据、所选目标隔离、空目标草稿、保存回读、同 ID 覆盖、另存保留原文件、拒绝已有目的文件、改 ID 必须另存、失败读写保留草稿与文件、默认目标选择和源文件列表。
- 当前已连接的 Unity 正运行旧阶段 2 的 Play Mode，棋盘有用户尚未保存的内容。为避免丢失该内存草稿，未退出 Play 或触发重载；没有把旧界面截图当作新版视觉验证。
- 用户新增的字体资源、TitleScene 与 LevelEditor 场景改动原样保留；未提交或推送此次代码。

使用与手动验收见 [LevelEditorQuickStart.md](/Users/pomelo/UnityProjects/2026taptap/Docs/LevelEditorQuickStart.md)。确认新版 Play Mode 可保存／重载后，再进入阶段 4 的共用玩法逻辑。
