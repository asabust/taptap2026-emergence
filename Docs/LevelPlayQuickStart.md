# 同场景编辑／试玩与 Organic 细胞表现

更新：2026-10-17。项目：`/Users/aoi/UnityProjects/taptap2026-emergence`。

阶段 6 已接入当前草稿的同场景试玩。编辑态使用方格画笔；试玩与 Gameplay 共用 OrganicGridDemo 风格的细胞膜融合、核、收缩和移动尾部。原 Demo 保留，正式规则不包含自动繁殖。

## 在 LevelEditor 中直接试玩

1. 若正在旧版本 Play 中，先保存需要保留的草稿，再退出 Play，让 Unity 导入编译。
2. 打开现有 `Assets/Scenes/LevelEditor.unity`，点击 Play，不需要创建新场景。
3. 加载关卡或直接编辑当前草稿。可从文件列表加载新增验证关 **stage6_probe**；也支持完全未保存的合法草稿。
4. 点击左下 **试玩当前草稿**。规则、额度和数量输入先应用；未应用尺寸需先明确调整。缺少玩家、目标或参数非法会列出错误并继续编辑。
5. 编辑界面隐藏，显示有机细胞棋盘与 HUD。没有 Scene 切换，没有 JSON 保存或重新读取。
6. 点击 Game 窗口取得键盘焦点。方向键移动，Space 迭代，Z 撤销，R 重置；B（Birth）或分身按钮进入放置模式，再点击合法空格；回收用按钮再选格。
7. 点击右侧 **结束试玩 · 返回编辑**。从暂停、分身选择、胜利或失败状态均可返回，原草稿、画笔、目标选择和视图保留。游玩状态与历史丢弃，不写回草稿。

若右侧内容未完整显示，可滚动 HUD。试玩不写正式完成进度，也不解锁关卡。

## 细胞表现和操作

- 粉色细胞为玩家，浅蓝为初始普通细胞，深蓝为分身；细胞核对应真实占位格，膜连接不会合并逻辑细胞。
- 金环为角色终点，紫环为细胞覆盖目标；黄色框为当前可放置／可回收的格子。
- 玩家移动约 0.2 秒并保留缩短的尾部；细胞变化约 0.55 秒，分身生长、核延迟出现，死亡细胞的膜与核收缩。
- 过渡期间不执行普通移动、迭代或选格；仍可 Z／R、暂停、取消和返回编辑。暂停冻结动画；Z／R 直接显示准确恢复状态。
- 分身／回收模式中，先选格或取消，再进行移动／迭代／Z／R。非法格保留选择模式，成功一次后返回移动。
- Esc 在选择中取消，在移动中暂停，在暂停中继续。胜败后可 Z 或 R；无历史时撤销按钮禁用。
- 你的 **002 分身额度为 0**，不能创建分身是其正常配置。使用 stage6_probe 可验证分身和回收，不必修改 002。

## 验证关 stage6_probe 的通关步骤

```text
y=1   P   .   C   G
y=0   C   T
      0   1   2   3
```

空白为无效区域。分身上限 1、迭代上限 2，两死亡规则开启；要求玩家到 G 且细胞覆盖 T。

| 操作 | 迭代轮数 | 存活分身 | 预期状态 |
| --- | --- | --- | --- |
| 初始 | 0 | 0 | 玩家 `(0,1)`；普通细胞 `(0,0)`、`(2,1)` |
| 分身 → 点 `(1,0)` | 0 | 1 | 保持玩家原位，T 被分身占据 |
| Space | 1 | 1 | `(0,0)` 和分身存活；通道的 `(2,1)` 死亡 |
| 右 → 右 → 右 | 1 | 1 | 玩家 `(3,1)`，T 同时覆盖，胜利 |
| Z | 0 | 1 | 恢复迭代前的玩家、分身及两个普通细胞 |
| R | 0 | 0 | 恢复这次试玩开始时的完整初始数据，历史清空 |

每次移动等过渡结束后再按下一次。若创建分身后先移到 `(1,1)` 再迭代，玩家会给阻挡细胞补足邻居，`(2,1)` 会存活，验证站位的作用。

## 独立 Gameplay 场景

已有 `Assets/Scenes/Gameplay.unity` 不必重建，Play 时会自动使用同一有机细胞视图。Inspector 的 Level Id 控制读哪张 JSON；默认 002，可改成 stage6_probe。它用于正式玩法表现与流程验证；编辑关卡时直接用 LevelEditor 的试玩按钮。

尚无此场景时可执行 **Tools → Levels → Create Gameplay Scene**；所有新场景仍放 `Assets/Scenes`，已有同名场景不会被覆盖。

## 自动检查与实际验证范围

在 Unity 等待脚本编译完成后，可执行：

| 菜单 Tools → Levels | 预期通过数 |
| --- | ---: |
| Run Data Checks | 47 |
| Run Editor Checks | 33 |
| Run Configuration Checks | 34 |
| Run Play Checks | 63 |
| Run History Checks | 44 |
| Run Playtest Checks | 15 |

另有 **Check Organic Shader** 检查本机 Shader 支持、首个 Pass 及编译错误。它只做 Shader 检查，不替代画面对照。

```bash
bash Docs/Tests/run-level-data-checks.sh
python3 Docs/Tests/check-level-editor-compilation.py
```

本次已运行：**236 项逻辑检查通过**；使用实际 Unity 2022.3.62f3 DLL 独立 C# 编译通过。新增界面的汉字已核对现有静态字体字符表。

用户已确认阶段 5 可以玩。新版同场景切换、Shader 编译和实际画面尚未在本次执行；UI 连接 Unity 超时，未触碰运行中的草稿。GPU Shader、Play Mode 和 PC/WebGL 构建不包含在上述独立检查结果内。

## 新版手动验收

1. 在未保存的新草稿中进入试玩，移动／分身／迭代后返回，确认草稿和“未保存”状态保持，Scene 始终是 LevelEditor。
2. 更改规则、目标或细胞，再次试玩，确认使用最新数据；R 恢复本次起点，不恢复上一次试玩。
3. 连续进出 5 次，确认 Hierarchy 不累积 Editor Playtest、Canvas 或 EventSystem，方向键不重复移动。
4. 无玩家／无目标／非法参数不能进入，原草稿仍在。尺寸输入不允许隐式裁切。
5. 在暂停、放置／回收、胜败覆盖层中点击返回编辑，确认无遗留键盘或棋盘点击。
6. 与 OrganicGridDemo 对照细胞膜、细胞核、融合颈、移动拖尾和死亡收缩；确认墙／空洞、终点环和选择高亮正确。
7. 将棋盘调为不同比例、30×30，检查四角拾取、缩放比例、右侧滚动与帧率。性能尚未实测。

## 共用入口与交接

`LevelPlaySession` 继续持有唯一玩法真值，`State`／`Definition` 返回副本。Z 的历史仍按迭代最多保留 100 轮，R 清空历史并重评目标。

`LevelPlayScreen`（命名空间 `Game.Runtime.Gameplay.LevelPlay`）支持 `loadOnStart=false`、`Initialize(draft)`、`showReturnToEditor=true` 与 `ExitRequested`。编辑器按此创建子对象并隐藏原 Canvas；结束时立即停用、解除事件、销毁试玩对象，再恢复原 Canvas，无磁盘重载。

`OrganicLevelBoardView` 只渲染会话状态；两张线性 Point 纹理传入生命／地形／目标／来源数据，Shader 局部采样，不复用 Demo 的出生规则。Shader 放在 Resources 确保能随项目资源加载；图形平台兼容性仍须构建验证。

完整记录见 [ImplementationStatus.md](/Users/aoi/UnityProjects/taptap2026-emergence/Docs/ImplementationStatus.md)。阶段 7 的标题、人体选关、解锁和正式存档未在本次实现。
