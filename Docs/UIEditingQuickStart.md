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
