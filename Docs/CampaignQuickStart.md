# 正式游戏流程与配置

更新：2026-11-03。以下按当前仓库代码与资源整理；运行环境见 [README](../README.md)。

## 启动与场景

从 `Assets/Scenes/Persistent.unity` 启动 Play。标题提供新游戏、继续游戏和退出，设置通过共用入口打开；已有可读取进度时继续游戏可用。新游戏在已有存档时先确认，确认后重置完成进度及对话／教程／档案阅读记录，保留音量。

正式流程：标题 → 人体章节总览 → 章节关卡列表 → 点击已解锁小关 → 加载 Gameplay 与棋盘 → 关前剧情／教程 → 游玩。人体节点对应章节，不再是每个关卡一个节点，也没有额外“预览后进入”按钮。继续游戏返回人体总览，不恢复局内状态。

正式构建执行 **Tools → Levels → Use Game Build Scenes**，列表为 Persistent、TitleScene、LevelSelect、Gameplay。策划工具构建使用 **Use Editor Build Scenes**，仅包含 LevelEditor。现有场景无需重新生成；`Configure Stage 7 Game Flow` 是早期场景与配置初始化工具。

## 五章关卡与配置

目录为 `Assets/Resources/Campaign/Catalog.asset`，共 18 关：

| 章节 ID | 内容／人体部位 | 关卡 ID |
| --- | --- | --- |
| chapter_01 | 教程／左大腿 | 001、002 |
| chapter_02 | 基础死亡／右盆骨 | 101–104 |
| chapter_03 | 分裂细胞／腰椎 | 201–204 |
| chapter_04 | 区域繁殖与额外目标／心脏 | 301–304 |
| chapter_05 | 综合考验／大脑 | 401–404 |

关卡棋盘位于 `Assets/StreamingAssets/Levels/<ID>.json`；ID 必须与文件名、Catalog 和叙事绑定一致。新增文件不会自动发布到目录。章节成员和顺序通过源表 `Docs/GameData/Database.xlsx` 维护，导出 Narrative 后执行 **Tools → UI → Create Editable UI** 同步 Catalog，详见 [UI 编辑说明](UIEditingQuickStart.md)。

人体节点实际位置在 `LevelSelectPanel.prefab` 中编辑；运行时从按钮位置计算章节透视中心，Catalog.bodyPosition 为备用坐标。章节画面使用 `ChapterScanView` 的配准线稿／彩色图及圆形 Mask。存在扫描组件时优先使用扫描画面，否则才使用章节 artwork。

章节完成条件取实际成员列表；完成整章后解锁下一章，章内按顺序推进。已完成关卡可重玩。当前前四章配置档案，最终章 archiveId 为空。

## 胜败、对话与返回

- 暂停提供继续、重新开始、设置、返回章节选关；离开本局先确认，局内状态不保存。
- 失败提供撤销、重试和返回；没有历史时不能撤销。撤销恢复有效操作前状态，规则见 [玩法说明](LevelPlayQuickStart.md)。
- 胜利先记录完成，等待表现结束后显示胜利面板；玩家可重玩，或点击继续，再串行播放未读 AfterWin 对话、展示已解锁且未读的章节档案，然后返回选关。不会自动进入下一关。
- 普通小关返回原章节列表；整章完成后返回人体总览。档案也可从已通关章节列表主动回看。
- BeforeLevel 在 Gameplay 的 HUD／棋盘初始化及淡入完成后播放。剧情／教程共用对话面板，Space／Enter 补全或翻页，跳过需确认；看完或确认跳过后记为已读，重玩不重复，没有对话回顾入口。
- 对话、设置、规则、加载及确认窗口阻挡底层玩法输入；游戏内设置从暂停菜单进入，其他正式页面使用全局设置按钮。

## 持久化与平台

PlayerPrefs：`Campaign.Progress.v1` 保存完成 ID；`Campaign.Narrative.v1` 保存对话与档案阅读记录；`MusicVolume`／`SFXVolume` 保存音量。章节解锁由完成 ID 和成员关系推导，不保存局内布局及撤销历史。损坏或未知版本记录保留原值，本次推进可仅留在内存。

只有正式 Campaign 会话记录完成。编辑器试玩及直接 Gameplay 测试不写正式完成进度。桌面通过 LevelRepository 读文件，WebGL 正式入口通过 UnityWebRequest 读取 StreamingAssets。

## 验证

命令及环境要求见 [README 的验证与构建](../README.md#验证与构建)。2026-11-03 核对时 315 项逻辑检查及独立 Editor／Player 编译通过，未重新执行 Play Mode 或平台打包。

**Tools → Levels → Run Campaign Integration Checks (Play Mode)** 要求先从 Persistent 启动。当前检查从 `Docs/Tests/Fixtures/stage6_probe.json` 创建临时随机 ID 关卡，在内存临时替换目录与进度，测试结束通过 finally 恢复目录和原进度／存档并删除临时 JSON；不要求正式目录包含 stage6_probe。它仍会切换运行场景，建议在专门的测试会话执行；不能替代全部章节通关验收。


**当前菜单限制：** `CampaignIntegrationChecks` 仍按旧对象名 `Victory Card` 和 `返回人体图` 查找 UI，而正式流程已迁移 Prefab。这轮未执行该菜单，不能保证适用于当前界面；需先更新检查工具的引用／查找方式，再报告正式流程集成通过。315 项纯逻辑检查和独立编译不覆盖此问题。
