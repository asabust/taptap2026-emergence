# 第七阶段：正式游戏流程

2026-10-19。

## 启动与打包

已提供 Unity 菜单 **Tools → Levels → Configure Stage 7 Game Flow**：生成 `Assets/Scenes/LevelSelect.unity` 与 `Assets/Resources/Campaign/Catalog.asset`；已有文件不覆盖。此步骤已在本机 Unity 执行。

正式游戏从 `Assets/Scenes/Persistent.unity` 点击 Play；也支持直接打开 TitleScene。启动进入现有 Title，美术布局保留，点击“新游戏”进入选关。开场剧情暂未制作，插入点为 `CampaignRuntime.BeginGame`。

**新游戏当前作为进入选关的入口，保留已完成进度，不静默删除存档。** 若后续需要真正清档开新档，应另外增加清档确认及继续游戏入口。

正式打包前执行 **Tools → Levels → Use Game Build Scenes**，构建列表顺序为 Persistent、TitleScene、LevelSelect、Gameplay；策划编辑器打包执行 **Use Editor Build Scenes**，仅包含 LevelEditor。两种构建共用项目资源，编辑器与直接打开 Gameplay 的测试均不写正式通关进度。

## 选关配置

选择 `Assets/Resources/Campaign/Catalog.asset`，在 Inspector 配置：

- `anatomy`：人体底图。
- `levels` 列表顺序：关卡推进顺序，JSON 文件 ID 作为稳定关联键，禁止重复 ID。
- `title`／`description`：选关标题与预览描述。
- `bodyPosition`：完整人体底图中的归一化坐标，左下 `(0,0)`、右上 `(1,1)`。
- `preview`：局部彩色图；未指定时从人体图相应位置裁切并着色作为流程占位图。

初始目录为 **stage6_probe → 001 → 002**，用于流程验证，不代表正式关卡排序或正式美术完成。后续按策划提供的关卡替换列表。新建 JSON 不会自动进入正式关卡目录，避免把草稿和测试关自动发布。

点击已解锁人体标记 → 查看局部图、准心与描述 → 点击进入关卡。未解锁标记禁用；通关后回到人体图，显示完成反馈并高亮下一关，不自动进入。已完成关可重玩；末关不请求不存在的下一关，全部完成时显示完成提示。

## 玩法与进度

正式 Gameplay 与编辑器试玩共用 LevelPlaySession、棋盘和操作入口。正式会话通过 CampaignRuntime 创建并订阅胜利事件；编辑器试玩和单独 Gameplay 不订阅。

- 暂停：继续、重新开始、返回选关；暂停重试会解除暂停并恢复初始状态。
- 失败：撤销一轮、重新开始、返回选关；无历史则撤销禁用。
- 胜利：目标成立后记录完成，棋盘动画结束再返回选关。
- 离开关卡丢弃本局布局与撤销历史，不保存局内状态。

进度使用独立 PlayerPrefs 键 `Campaign.Progress.v1`，格式为版本号和已完成关卡 ID 列表，完成时显式 Save。顺序调整不重映射 ID，暂时移出目录的已完成 ID 继续保留。损坏或未知版本进度不被覆盖，本次只在内存中推进并显示提示。音量沿用 `MusicVolume`／`SFXVolume`，标题“设置”可调并保存。

桌面关卡通过现有 Repository 路径读取；WebGL 正式入口使用 UnityWebRequest 请求 StreamingAssets，不走桌面 File API。浏览器持久化与 WebGL 实包仍须后续构建验证。

## 检查

```bash
bash Docs/Tests/run-level-data-checks.sh
python3 Docs/Tests/check-level-editor-compilation.py
```

逻辑检查新增 29 项进度检查，连同既有 236 项共 265 项。编译脚本使用 Unity 自带 Roslyn 编译实际相关模块，包含 UNITY_EDITOR 与 Player 两个运行时分支；不代表平台构建通过。

Unity 菜单 **Tools → Levels → Run Campaign Checks** 可复核进度逻辑。在 Persistent 启动 Play 后执行 **Run Campaign Integration Checks (Play Mode)**，会自动经过选关、加载 stage6_probe、执行通关操作、返回选关，并验证下一关解锁／高亮和存档重新读取；会保留现有完成进度并记录 probe 已完成。要求目录前两项包含 probe 及后续关，不用于正式玩家运行。

已实际验证与待验证范围见 ImplementationStatus.md。独立构建、WebGL 浏览器、正式局部彩图和开场对话不包含在本阶段 Play Mode 验证中。

## 正式关卡界面（2026-10-19 更新）

Gameplay 使用浅色细胞纸张背景，棋盘为蓝灰到淡紫渐变的圆角菱形地块，边缘轻微流动，并带整体软阴影。按实际地形范围取景，外围空格不占展示面积；图形形变不改变格子、坐标或规则。影子周边留白不接受选格。

左上 stop 图标打开暂停；右下最大的美术按钮为演化（Space），双细胞图标为分身（B），上方菱形箭头为撤销（Z），小感叹号打开可滚动的本关规则说明。回收为分身附近的次级文字入口；重试和返回选关在暂停／失败面板中。规则说明打开时冻结棋盘表现、拦截键盘和棋盘输入，Esc 或关闭按钮返回。

编辑器画笔与编辑器内试玩仍保留制作关卡使用的清晰诊断界面；使用相同玩法会话。正式界面的棋盘样式仅属于展示层。

美术引用集中在 `Assets/Resources/LevelPlay/Presentation.asset`；Unity 菜单 **Tools → Levels → Configure Game Presentation** 可在首次接入时创建，已有配置不覆盖。背景与按钮直接引用既有 Arts/UI 资源，没有把示例棋盘整张图片当作关卡地形。

**Capture Game Preview (Play Mode)** 可导出当前实际 Game 画面到系统临时目录的 `taptap-gameplay-preview.png`。字体动态字符表的变化单独作为美术提交。
