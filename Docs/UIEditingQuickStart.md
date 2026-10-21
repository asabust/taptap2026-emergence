# UI Prefab 编辑与交接

更新：2026-10-21。首版功能骨架已落地，排版与按钮仍可继续调整。

## 直接编辑

停止 Play，在 Project 打开 `Assets/Resources/UI`，双击 Prefab。选中子物体后，用 Rect Transform 调位置/尺寸/锚点，Image → Source Image 换图，TMP 组件调整字体与样式。保存退出 Prefab Mode，再打开 `Assets/Scenes/Persistent.unity` 运行。

| Prefab | 用途 / 可改区域 |
| --- | --- |
| TitlePanel | 标题背景、Logo、开始/继续、设置、退出 |
| LevelSelectPanel | 完整人体图、五个 ChapterNode 按钮；节点坐标直接在此调整 |
| ChapterSelectPanel | 章节标题、ChapterArtwork、小关列表 LevelList、档案入口 |
| GameplayHUDPanel | 棋盘容器 BoardHost、计数、目标、操作按钮；暂停/失败/胜利为嵌套 Prefab |
| DialoguePanel | 通用背景、Portrait、SpeakerName、DialogueBox、DialogueText、继续与跳过 |
| ChapterArchivePanel | 标题、可滚动正文、配图、图注、返回/继续 |
| PausePanel / FailurePanel / VictoryPanel | 暂停、失败、胜利状态与按钮 |
| RulesPanel / SettingsPanel / ConfirmPanel / MessagePanel / LoadingPanel | 规则、音量、确认、错误、读取 |

小关按钮来自 ChapterSelectPanel 的 LevelButtonTemplate；默认关闭是模板状态，运行会按关卡数复制。调整模板尺寸与样式，以及 LevelList 的 VerticalLayoutGroup.spacing；布局组件管理子按钮的位置，不要逐个手工摆放运行时复制项。

可以改对象名称及增加装饰，但不要删除已被 CampaignPanelView / GameplayHUDView 引用的对象、清空字段引用或增加第二套点击监听。若重新组织层级，保留引用。普通图为 Image + Sprite；棋盘是 RawImage / Shader 画面，调整 BoardHost 容器，不能通过 Source Image 改细胞规则。

章节局部图目前用线稿占位；可换 ChapterSelectPanel/ChapterArtwork 的 Sprite。Catalog.chapters 中的 artwork 若配置了具体章节图，会优先用于该章；坐标不会在运行时覆盖人体节点的 Prefab 布局。

## 数据与导入

源表为 `Docs/GameData/Database.xlsx`。第一行为英文字段，第二行为说明，第三行起为数据；粉红色策划标记保留。只导出正式工作表，`Dialogue原始` 不参与运行。

```bash
python3 Docs/Tools/import-ui-database.py Docs/GameData/Database.xlsx
```

命令输出 `Assets/Resources/Campaign/Narrative.json`。目前表中已填充全部 18 个 Levels 入口、13 个对话组实际 levelId（保留 001/002 前导零）。实际关卡内容仍在 `Assets/StreamingAssets/Levels/*.json`，不由表格覆盖。

当前章节映射：第一章 001/002；第二章 101–104；第三章 201–204；第四章 301–304；最终章 401–404。标题暂沿用 JSON 的 name，因此策划旧命名中的“第一章”可能与 UI 五章编号不同。若修改章节数量、成员或最后一关，请更新 Levels 中的成员/顺序和 DialogueGroups 中的实际关卡绑定，而不是只改对话 ID 的名字。正式表的完整 Levels 列表优先于转换旧草稿时的备用映射。

在 Unity 执行 `Tools → UI → Create Editable UI` 同步 Catalog 并创建缺失 Prefab。**已有 Prefab 不会覆盖**；改 Excel 不必重新生成布局。不使用旧 `Tools → Export Database To JSON`，该菜单属于旧空表框架，不读取本次新源表。

姓名与立绘独立：speakerName 空则隐藏名字，portraitId 空则隐藏立绘，支持“档案摘要”等有名无图文本。立绘 ID player/o/kui 对应 `Assets/Resources/UI/Portraits` 中的 Sprite。剧情和教程首次播放或确认跳过后记为已读，重玩不自动重复，没有“对话回顾”入口；档案仍可在通关章节内回看。

## 已验证与待验

独立 Editor/Player 编译通过；306 项纯逻辑检查通过，其中包含章节全部完成后解锁下一章、稳定 ID 存档回读、末关边界。Unity 内已点击：标题 → 人体总览 → 第一章列表 → 剧情跳过 → 教程跳过 → 001 Gameplay → 规则关闭 → 暂停 → 设置返回 → 返回确认 → 原章节列表。Console 当时无错误/警告。

完整章节通关、胜利后的档案阅读与回看、失败恢复、全部目标画幅、独立包及 WebGL 构建仍需验收。美术布局为首版占位，尚非视觉定稿。未修改关卡 JSON、提交或推送代码。

全量 Unity 资源校验补充：14 个 Prefab、全部立绘引用及 13 个对话组检查通过；18 份 JSON 中 202 缺少通关目标，需策划补齐，其他 17 份通过玩法校验。本次不替策划填写目标。

## 章节圆形透视（2026-10-21）

已接入两张同尺寸背景：总览显示骨骼线稿；进入章节后，左侧放大相应部位，圆形 UGUI Mask 内显示彩色透视图，右侧保留关卡列表；右上角返回人体总览。两层图像始终保持同位置和同缩放。圆框和准星为 UI 图形。

停止 Play Mode 后双击 `Assets/Resources/UI/ChapterSelectPanel.prefab`：

- `ScanViewport`：Rect Transform 调整左侧区域的位置和大小；`Chapter Scan View → Magnification` 调放大倍率；`Preview Focus` 是编辑器预览坐标（左下 0,0，右上 1,1）。
- `ScanViewport/ScanCircle`：Rect Transform 调圆圈的位置、宽高；宽高保持相等。`ScanOutline`、`Reticle` 控制圆框和准星。
- 不要单独调整 `SkeletonLayer` 与 `AnatomyLayer` 的 Transform，组件会同步它们；换图用对应 Image 的 Source Image，两张新图必须有相同画布和人体配准。
- `LevelSelectPanel/Anatomy` 下的章节按钮可移动；运行时从按钮实际位置计算透视中心。

正常从 TitleScene 运行。仅预览时，也可进入 Play Mode 后执行 `Tools → UI → Preview Chapter Selection (Play Mode)`，不会写入通关或对话进度。布局修改仍应在停止运行后进行。

验证：Editor/Player 编译通过；Unity 中五个章节坐标、15 组图层对齐检查通过；已实际点击第一章，检查圆形显示、关卡锁定和右上角返回，Console 无错误或警告。其余章节受正常存档解锁限制，未逐章手动点击。

## 全局设置入口

所有正式界面共用 `Assets/Resources/UI/GlobalSettingsButton.prefab`，运行时固定显示在 UI 顶层；加载、设置及确认窗口期间隐藏。修改这个 Prefab 的 Rect Transform、图片和文字即可统一调整入口。标题原设置按钮、选章旧返回按钮由运行时隐藏，避免重复。章节返回仍用于回到人体总览。

`SettingsPanel.prefab` 中音乐/音效滑块即时生效，关闭时保存；包含返回标题和退出游戏确认。游戏中离开提示“退出本局状态不会保存”。打开设置时玩法输入及棋盘演化被阻挡，对话停止打字和翻页，关闭后恢复。WebGL 隐藏退出游戏。

已验证：共用设置入口打开菜单，退出确认取消，返回标题确认后进入标题。编译通过。测试中的 GameManager 单例报错由人为 inactive 造成，保留用户测试状态；未确认退出应用，也未逐页测试全部玩法状态。

## 游戏内按钮与棋盘（2026-10-21）

编辑 `Assets/Resources/UI/GameplayHUDPanel.prefab`：`UndoButton`、`CloneButton`、`IterateButton` 对应撤销、B 分身、Space 演化。图片已绑定 `Assets/Arts/UI/Game` 中的撤销按钮、分身按钮、分裂按钮；Image → Source Image 可换图，子物体 Caption 编辑名称及快捷键。回收分身暂保留文字按钮，尚无对应命名图片。

`BoardHost/Board` 的 Organic Level Board View → Goal Icon 已绑定终点图标。棋盘使用静止、轴对齐的方形地砖，浅色地面、深色墙面；普通细胞半径以约 4.2 秒周期轻微变化（±2.5%），分身与玩家不参与此呼吸动画。移动及演化的状态过渡仍保留。目标说明已更新为“抵达蓝色交叉终点”。

编译与 306 项逻辑检查通过；Unity 正式 Gameplay 运行画面已确认新按钮、方形地砖与蓝色叉号显示正常。未改关卡 JSON 和规则。

## 标题新游戏与继续游戏

`TitlePanel.prefab` 的 `NewGameButton` 与 `ContinueButton` 分别绑定 Campaign Panel View 的 Primary、Secondary。两个按钮同时显示；Continue 只在存在可读取进度时可用。首次开始即保存空通关进度，因此未通关也能继续到选关。继续仍记录关卡通关/解锁，不保存本局棋盘状态。

已有进度时，新游戏先弹确认，确认才重置通关、章节档案解锁及对话/教程阅读记录；音量设置保留。未执行用户真实存档重置来测试。Unity 已确认标题双按钮显示，编译通过。

有效地砖恢复浅蓝到灰蓝的整体静态渐变；墙面保持深色，方形轮廓、间隙和位置固定。渐变不再随时间或玩家移动变化。

## 开场对话时机与游戏内设置入口

BeforeLevel 对话在 Gameplay 场景完成关卡数据初始化、HUD/棋盘构建以及场景淡入后播放，背景为已加载的关卡。等待期间保留加载遮罩并阻止玩法输入；对话关闭后继续关卡。已读跳过规则不变，不清除现有阅读记录。AfterWin 对话流程不变。

Gameplay 场景隐藏 GlobalSettingsButton；左上角暂停菜单继续提供设置。标题、章节和档案界面仍显示全局设置入口。编译通过。

游戏内 `RecycleButton` 已隐藏，保留 Z 整轮撤销。规则说明同步移除手动回收入口提示。撤销不等于单独删除分身，玩法规则和编辑器诊断功能不变。

回收功能保留为键盘入口：N 进入回收模式，点击任意存活分身回收，Esc 取消。屏幕按钮隐藏，底部及规则中提供快捷键提示。初始普通细胞不可回收，Z 仍为整轮撤销。

## 音乐与音效

沿用 AudioManager，配置资产为 `Assets/GameData/AudioInfoListSO.asset`，Persistent 场景已绑定。两个 AudioSource 分别负责音乐与音效，均为 2D。音乐播放和滑块调节始终保留各 Clip 的基础音量；音效使用 PlayOneShot，避免移动/UI 声切断其他音效。

| Audio Name | 文件 / 触发 |
| --- | --- |
| MenuBGM | 主菜单bgm：标题、选章、选关，循环 |
| BGM | 关卡循环bgm：进入 Gameplay，循环 |
| Hover / Click | UI悬停 / UI点击：可用按钮 |
| Move | 玩家移动：成功移动 |
| Divide | 分裂分身：成功演化或创建分身 |
| Invalid | 无效操作：玩法操作被规则拒绝 |
| Victory | 关卡过关瞬间：首次判定胜利 |

暂停/对话期间音乐继续播放。音乐/音效设置独立保存；不随新游戏清除。编辑 Audio Infos 内每项 Volume 调基础混音，Loop 只勾选两首 BGM。`Tools → UI → Configure Named Audio` 可按命名文件重新生成初始配置（会覆盖表内基础音量）；`Validate Named Audio` 检查八个 Clip 和循环配置，Play Mode 下还检查当前音乐播放源。

Unity 内已经重新生成和保存配置，Inspector 显示 8 项；Play Mode 音乐源加载主菜单曲目并处于播放状态验证通过。编译通过。最终响度、音效听感和循环接缝仍需人工试听。

## 分身回收与撤销更新

N 按关卡配置的 cloneLimit 判断：上限等于 1 且有存活分身时直接回收；上限大于 1 时进入选择，即使目前只剩一个分身也须选择。无分身时提示无可回收目标。选择模式用蓝白双圈标记可回收分身，标记绘制在细胞之上；Esc 取消。

Z 按倒序撤销成功的演化、创建分身、回收分身，最多保留 100 次操作。撤销恢复操作前的完整棋盘、玩家位置、迭代数与分身名额；移动不单独记步。进入/取消选择、失败操作不占历史。撤销可在选择模式使用，使用后回到移动模式。此前文档的“只撤销演化”规则由本条替代。新增单分身直接回收、混合撤销、无效目标、选择中撤销等回归检查。

## 本次提交状态

收录已调整的 UI Prefab、章节圆形透视、标题双入口、共用设置、关卡后加载对话、静态渐变方砖、蓝色终点、图标按钮、音频接入，以及 N 回收/Z 分身撤销。演化按钮美术现命名为“迭代按钮.png”。用户已为 202.json 补充玩家到达目标，先前“202 无目标”的记录已失效。场景、美术重命名与打包产品设置一同保存；不包含本机 Finder 缓存。
