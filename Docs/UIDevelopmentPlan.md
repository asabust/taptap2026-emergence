# UI 结项交接

更新：2026-11-03。原讨论稿已移至 [Archive/UIDevelopmentPlan.md](Archive/UIDevelopmentPlan.md)，保留设计取舍和源表读取纠正；其中的建议、未勾选验收和旧路径不代表当前状态。

## 当前实现

- 正式 UI 使用 Assets/Resources/UI 的 Prefab；CampaignInterface 负责页面与模态窗口，GameplayHUDView 连接玩法控件。棋盘仍为程序／Shader 渲染。
- 标题同时提供新游戏和继续：新游戏确认后清完成／阅读记录，继续返回选关，不恢复局内。
- 人体总览五个节点对应章节，点击进入带圆形透视的章节关卡列表；小关由模板生成，不再逐关创建人体节点。
- 完成全部章成员解锁下一章；胜利面板继续后串行播放关后对话／未读章档案并返回选关，不能自动进入下一关。
- BeforeLevel 在棋盘和淡入就绪后播放。剧情／教程可确认跳过，看完／跳过记已读，无对话回顾；档案可以回看。
- 正式 HUD 回收按钮隐藏，N 保留；Z 支持迭代、创建、回收各自撤销。静态方形渐变地砖、蓝色终点、柔橙玩家与蓝紫普通细胞是当前表现。
- 全局设置在非 Gameplay 正式页显示；游戏内通过暂停打开。WebGL 隐藏退出应用入口。

## 配置与维护

具体 Prefab 表、源表字段、导出命令、扫描配准、按钮绑定和音频表见 [UIEditingQuickStart](UIEditingQuickStart.md)。唯一正式源表为 Docs/GameData/Database.xlsx，Narrative.json 是导出产物，关卡 JSON 独立保存；旧 Downloads 工作簿及通用空表框架不是运行入口。

保留序列化组件引用，不叠加第二套按钮监听；修改模板及 LayoutGroup 控制小关列表；配准图层由 ChapterScanView 同步，不能分别移动。现有 Prefab 无需重新生成，Create Editable UI 仅创建缺失项并同步目录。

## 验证状态

2026-11-03：315 项逻辑检查、相关 Editor／Player 独立编译通过；本次没有重新执行人工 UI 验收。历史人工结果见 [ImplementationStatus](ImplementationStatus.md)。完整章末、档案、多画幅、最终混音与目标平台构建仍需实际复核。历史规划中的双立绘槽、角色目录 ScriptableObject、对话回顾等不是现有功能合同。
