using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Runtime.Data.Levels;
using Game.Runtime.Gameplay.LevelEditor;
using Game.Runtime.Gameplay.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.LevelPlay
{
    /// <summary>Shared organic play HUD for Gameplay and same-scene editor playtesting.</summary>
    public sealed partial class LevelPlayScreen : MonoBehaviour
    {
        public string levelId = "002";
        public TMP_FontAsset uiFont;
        public bool loadOnStart = true;
        public bool showReturnToEditor;
        public bool formalGame;
        public event Action ExitRequested;
        public event Action<string> VictoryReached;
        public LevelPlaySession Session { get; private set; }
        private LevelPlayController controller;
        private LevelDefinition definition;
        private GameObject canvasObject, ownedEventSystem, overlay;
        private OrganicLevelBoardView board;
        private TextMeshProUGUI title, counters, goals, mode, status, overlayText;
        private Button undo, reset, iterate, clone, recycle, cancel, pause;
        private readonly List<LevelPlayRequest> requests = new List<LevelPlayRequest>();
        private int initializedFrame;
        private bool renderFailed;
        private bool victoryReported;
        private Button overlayContinue, overlayRetry, overlayBack;
        private static readonly Color Ink = new Color(.88f, .92f, .97f);

        private void Start()
        {
            if (!uiFont) uiFont = TMP_Settings.defaultFontAsset;
            Build();
            if (loadOnStart && Session == null)
            {
                try { Initialize(LevelRepository.DeveloperDefault().Load(levelId)); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is FormatException)
                { status.text = "无法开始试玩：" + e.Message + "\n请停止 Play，检查 Level Id 和 JSON。"; }
            }
            Refresh();
            NotifyVictory();
        }

        public void Initialize(LevelDefinition initial)
        {
            // Build the replacement first; an invalid level cannot destroy a running session.
            var next = new LevelPlaySession(initial);
            Session = next;
            definition = next.Definition;
            controller = new LevelPlayController(next);
            renderFailed = false;
            victoryReported = false;
            requests.Clear();
            initializedFrame = Time.frameCount;
            if (status) status.text = GamePresentation ? "" : "方向键移动 · 空格迭代 · B 分身（Birth） · Z 撤销 · R 重置 · Esc 取消选择／暂停";
            if (board) Refresh(false);
        }

        private void LateUpdate()
        {
            if (rulesOpen)
            {
                requests.Clear();
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseRules();
                return;
            }
            if (Session == null || renderFailed || Time.frameCount <= initializedFrame) { requests.Clear(); return; }
            var keyboard = Keyboard.current;
            // Focused text fields keep their own keyboard input in a future embedded HUD.
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            bool typing = selected && selected.GetComponentInParent<TMP_InputField>() != null;
            if (keyboard != null && !typing && Application.isFocused)
            {
                if (keyboard.rKey.wasPressedThisFrame) Queue(LevelPlayCommand.Reset);
                if (keyboard.zKey.wasPressedThisFrame) Queue(LevelPlayCommand.Undo);
                if (keyboard.escapeKey.wasPressedThisFrame)
                    Queue(controller.Paused || Session.Mode != LevelInteractionMode.Move ? LevelPlayCommand.Cancel : LevelPlayCommand.Pause);
                if (keyboard.spaceKey.wasPressedThisFrame) Queue(LevelPlayCommand.Iterate);
                if (keyboard.bKey.wasPressedThisFrame) Queue(LevelPlayCommand.Clone);
                if (keyboard.upArrowKey.wasPressedThisFrame) Queue(LevelPlayCommand.Up);
                if (keyboard.rightArrowKey.wasPressedThisFrame) Queue(LevelPlayCommand.Right);
                if (keyboard.downArrowKey.wasPressedThisFrame) Queue(LevelPlayCommand.Down);
                if (keyboard.leftArrowKey.wasPressedThisFrame) Queue(LevelPlayCommand.Left);
            }
            if (requests.Count > 0)
            {
                if (board.IsAnimating)
                    requests.RemoveAll(r => r.Command != LevelPlayCommand.Reset && r.Command != LevelPlayCommand.Undo
                        && r.Command != LevelPlayCommand.Cancel && r.Command != LevelPlayCommand.Pause);
                controller.ProcessFrame(Time.frameCount, requests, out string message);
                requests.Clear();
                if (!string.IsNullOrEmpty(message)) status.text = Translate(message);
                Refresh(message != "Level reset." && message != "Iteration undone.");
            }
            NotifyVictory();
        }

        private void OnDisable()
        {
            requests.Clear();
            Session?.CancelSelection();
            if (canvasObject) canvasObject.SetActive(false);
        }
        private void OnEnable() { if (canvasObject) { canvasObject.SetActive(true); Refresh(); } }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                requests.Clear(); Session?.CancelSelection();
                if (board && Session != null) Refresh();
            }
        }
        private void OnDestroy()
        {
            if (board) { board.CellClicked -= SelectCell; board.AnimationCompleted -= FinishAnimation; }
            if (canvasObject) Destroy(canvasObject);
            if (ownedEventSystem) Destroy(ownedEventSystem);
        }
        private void Queue(LevelPlayCommand command) { if (isActiveAndEnabled) requests.Add(new LevelPlayRequest(command)); }
        private void SelectCell(int x, int y) { if (isActiveAndEnabled) requests.Add(new LevelPlayRequest(LevelPlayCommand.Select, x, y)); }
        private void FinishAnimation() { Refresh(); NotifyVictory(); }
        private void NotifyVictory()
        {
            if (!formalGame || !isActiveAndEnabled || renderFailed || Session == null
                || Session.Status != LevelPlayStatus.Won) return;
            if (!victoryReported)
            {
                victoryReported = true;
                VictoryReached?.Invoke(definition.id);
            }
            if (!board.IsAnimating) ReturnToEditor();
        }
        private void ReturnToEditor()
        {
            requests.Clear();
            enabled = false;
            ExitRequested?.Invoke();
        }

        private void Build()
        {
            if (GamePresentation) { BuildGamePresentation(); return; }
            canvasObject = new GameObject("Play Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)canvasObject.transform;
            var background = Box(root, "Background", new Color(.055f, .08f, .13f)); Stretch(background);
            title = Label(root, "玩法测试", 24, 18, 1050, 42, 25);
            var area = Box(root, "Board Area", Color.clear);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(24, 100); area.offsetMax = new Vector2(-390, -86);
            area.GetComponent<Image>().raycastTarget = false;
            var boardObject = new GameObject("Organic Board", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RectTransform>();
            boardObject.SetParent(area, false); Stretch(boardObject);
            var fitter = boardObject.gameObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            board = boardObject.gameObject.AddComponent<OrganicLevelBoardView>(); board.CellClicked += SelectCell;
            board.AnimationCompleted += FinishAnimation;
            var cover = Box(area, "State Overlay", new Color(.035f, .055f, .09f, .72f)); Stretch(cover); overlay = cover.gameObject;
            overlayText = Label(cover, "", 0, 0, 0, 0, 26); Stretch(overlayText.rectTransform);
            overlayText.alignment = TextAlignmentOptions.Center;
            if (formalGame)
            {
                overlayText.rectTransform.anchorMin = overlayText.rectTransform.anchorMax = new Vector2(.5f, .5f);
                overlayText.rectTransform.pivot = new Vector2(.5f, .5f);
                overlayText.rectTransform.sizeDelta = new Vector2(500, 100);
                overlayText.rectTransform.anchoredPosition = new Vector2(0, 100);
                overlayContinue = OverlayButton(cover, "继续", 10, () => Queue(LevelPlayCommand.Cancel));
                overlayRetry = OverlayButton(cover, "重新开始", -48, () =>
                {
                    controller = new LevelPlayController(Session);
                    Queue(LevelPlayCommand.Reset);
                });
                overlayBack = OverlayButton(cover, "返回选关", -106, ReturnToEditor);
            }

            var panel = Box(root, "HUD", new Color(.09f, .13f, .2f));
            panel.anchorMin = new Vector2(1, 0); panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(-366, 100); panel.offsetMax = new Vector2(-24, -24);
            var scroll = panel.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = Box(panel, "Viewport", Color.clear); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0, 1);
            content.sizeDelta = new Vector2(0, 1100); content.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport; scroll.content = content; scroll.movementType = ScrollRect.MovementType.Clamped;
            counters = Label(content, "", 14, 14, 308, 90, 21);
            mode = Label(content, "", 14, 110, 308, 70, 18);
            iterate = ActionButton(content, "迭代  Space", 184, LevelPlayCommand.Iterate);
            undo = ActionButton(content, "撤销一轮  Z", 236, LevelPlayCommand.Undo);
            reset = ActionButton(content, "重新开始  R", 288, LevelPlayCommand.Reset);
            clone = ActionButton(content, "放置分身  B（Birth）", 340, LevelPlayCommand.Clone);
            recycle = ActionButton(content, "回收分身", 392, LevelPlayCommand.Recycle);
            cancel = ActionButton(content, "取消选择  Esc", 444, LevelPlayCommand.Cancel);
            pause = ActionButton(content, "暂停／继续  Esc", 496, LevelPlayCommand.Pause);
            if (showReturnToEditor || formalGame)
            {
                var back = ActionButton(content, formalGame ? "返回选关" : "结束试玩 · 返回编辑", 548, LevelPlayCommand.Cancel);
                back.onClick.RemoveAllListeners(); back.onClick.AddListener(ReturnToEditor);
            }
            Label(content, "粉色：玩家 · 浅蓝：初始细胞\n深蓝：分身 · 金环：玩家终点\n紫环：细胞目标 · 黄框：可选位置", 14, 606, 308, 92, 16);
            goals = Label(content, "", 14, 710, 306, 430, 17);
            status = Label(root, "", 24, 0, 1390, 66, 17);
            status.rectTransform.anchorMin = Vector2.zero; status.rectTransform.anchorMax = new Vector2(1, 0);
            status.rectTransform.offsetMin = new Vector2(24, 16); status.rectTransform.offsetMax = new Vector2(-24, 82);
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Play EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(transform, false);
            }
        }

        private void Refresh(bool animate = true)
        {
            if (renderFailed) return;
            bool loaded = Session != null;
            bool playing = loaded && Session.Status == LevelPlayStatus.Playing;
            bool paused = loaded && controller.Paused;
            if (formalGame || GamePresentation)
            {
                bool failed = loaded && Session.Status == LevelPlayStatus.Failed;
                overlayContinue.gameObject.SetActive(paused || failed);
                overlayContinue.GetComponentInChildren<TextMeshProUGUI>().text = paused ? "继续" : "撤销一轮  Z";
                overlayContinue.onClick.RemoveAllListeners();
                overlayContinue.onClick.AddListener(() => Queue(paused ? LevelPlayCommand.Cancel : LevelPlayCommand.Undo));
                overlayContinue.interactable = paused || failed && Session.CanUndo;
                overlayRetry.gameObject.SetActive(paused || failed);
                overlayBack.gameObject.SetActive(true);
            }
            if (loaded)
            {
                try { board.Render(definition, Session, animate); board.Paused = paused || rulesOpen; }
                catch (Exception e) when (e is InvalidOperationException || e is UnityException)
                {
                    renderFailed = true;
                    iterate.interactable = clone.interactable = recycle.interactable = undo.interactable
                        = reset.interactable = cancel.interactable = pause.interactable = false;
                    overlay.SetActive(true); overlayText.text = "细胞 Shader 无法加载\n请检查 Console 或返回编辑";
                    status.text = "渲染失败：" + e.Message;
                    return;
                }
            }
            bool moving = playing && !paused && !board.IsAnimating && Session.Mode == LevelInteractionMode.Move;
            iterate.interactable = clone.interactable = recycle.interactable = moving;
            undo.interactable = loaded && !paused && Session.Mode == LevelInteractionMode.Move && Session.CanUndo;
            reset.interactable = loaded && (!paused || GamePresentation) && Session.Mode == LevelInteractionMode.Move;
            cancel.interactable = playing && !paused && Session.Mode != LevelInteractionMode.Move;
            pause.interactable = playing;
            overlay.SetActive(!loaded || paused || !playing && !board.IsAnimating);
            if (!loaded) { overlayText.text = "未加载可试玩的关卡\n请检查底部提示"; return; }
            if (GamePresentation)
            {
                RefreshGamePresentation(paused);
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
                return;
            }
            title.text = (formalGame ? "" : "玩法测试 · ") + definition.id + " · " + definition.name;
            counters.text = "迭代  " + Session.Iteration + " / " + Session.IterationLimit + "\n分身  " + Session.LivingCloneCount + " / " + Session.CloneLimit
                + "\n可撤销  " + Session.HistoryCount + " / " + LevelPlaySession.HistoryCapacity;
            mode.text = paused ? "已暂停" : Session.Mode == LevelInteractionMode.PlaceClone ? "分身模式：点击黄色空格\n取消后再进行其他操作"
                : Session.Mode == LevelInteractionMode.RecycleClone ? "回收模式：点击黄色分身\n取消后再进行其他操作" : "移动模式：方向键单次移动";
            overlayText.text = paused ? "已暂停\nEsc 或暂停／继续按钮返回" : Session.Status == LevelPlayStatus.Won ? "目标已达成\n可按 Z 撤销或 R 重新开始"
                : "迭代次数已超限\n可按 Z 撤销或 R 重新开始";
            goals.text = DescribeGoals(Session.State);
            goals.ForceMeshUpdate();
            float goalHeight = Mathf.Max(430, goals.preferredHeight);
            goals.rectTransform.sizeDelta = new Vector2(306, goalHeight);
            ((RectTransform)goals.transform.parent).sizeDelta = new Vector2(0, 730 + goalHeight);
            // Avoid keyboard Space submitting a previously selected UI button.
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        private string DescribeGoals(LevelState state)
        {
            var text = new StringBuilder("全部目标需同时满足\n");
            foreach (var goal in definition.winConditions.conditions)
            {
                if (goal.type == LevelIds.ActorAtPosition)
                    text.Append("玩家到达 (").Append(goal.position.x).Append(',').Append(goal.position.y).Append(")\n");
                else if (goal.type == LevelIds.CellCount)
                    text.Append("细胞数量 ").Append(goal.@operator).Append(' ').Append(goal.value).Append("，当前 ").Append(state.Cells.Count).Append('\n');
                else
                {
                    int covered = 0;
                    foreach (var pos in goal.positions) if (state.Cells.Exists(c => c.X == pos.x && c.Y == pos.y)) covered++;
                    text.Append("细胞覆盖 ").Append(covered).Append('/').Append(goal.positions.Count).Append('\n');
                }
            }
            bool lonely = definition.rules.Exists(r => r.id == LevelIds.LonelyDeath), crowded = definition.rules.Exists(r => r.id == LevelIds.CrowdedDeath);
            text.Append("\n孤独死亡：").Append(lonely ? "开启" : "关闭").Append("\n拥挤死亡：").Append(crowded ? "开启" : "关闭");
            if (!lonely && !crowded) text.Append("\n两个死亡规则均关闭，迭代不改变细胞。");
            return text.ToString();
        }

        private Button ActionButton(RectTransform parent, string caption, float y, LevelPlayCommand command)
        {
            var rect = Box(parent, caption, new Color(.16f, .25f, .35f)); Place(rect, 14, y, 308, 44);
            var button = rect.gameObject.AddComponent<LevelPlayButton>(); button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(() => Queue(command));
            var label = Label(rect, caption, 0, 0, 308, 44, 19); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private Button OverlayButton(RectTransform parent, string caption, float y, Action action)
        {
            var rect = Box(parent, caption, new Color(.16f, .25f, .35f));
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(280, 44); rect.anchoredPosition = new Vector2(0, y);
            var button = rect.gameObject.AddComponent<LevelPlayButton>(); button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(() => action());
            var label = Label(rect, caption, 0, 0, 280, 44, 20); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private TextMeshProUGUI Label(RectTransform parent, string text, float x, float y, float w, float h, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); Place(rect, x, y, w, h);
            var label = go.GetComponent<TextMeshProUGUI>(); label.font = uiFont; label.text = text; label.fontSize = size; label.color = Ink;
            label.raycastTarget = false; label.enableWordWrapping = true; label.alignment = TextAlignmentOptions.TopLeft;
            return label;
        }
        private static RectTransform Box(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); go.GetComponent<Image>().color = color; return rect;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }

        private static string Translate(string message)
        {
            switch (message)
            {
                case "Moved.": return "已移动。";
                case "Iterated.": return "已完成一轮迭代。";
                case "Iteration undone.": return "已恢复迭代前状态，并丢弃该轮之后的移动、分身与回收。";
                case "Level reset.": return "已恢复初始布局并清空历史。";
                case "No iteration history to undo.": return "已到可撤销历史的起点；Z 不单独撤销移动、分身或回收。";
                case "Paused.": return "已暂停，棋盘不接受操作。";
                case "Resumed.": return "已继续。";
                case "Selection cancelled.": return "已取消选择。";
                case "Destination is not floor.": return "目标是墙、空洞或棋盘之外，无法移动。";
                case "Destination is occupied by a living cell.": return "目标被存活细胞占据，无法移动。";
                case "Cloning is disabled in this level.": return "本关分身额度为 0，不能创建分身。";
                case "Clone limit reached; recycle a clone first.": return "分身已满，请先回收一个。";
                case "No adjacent empty floor is available.": return "玩家周围没有可放置分身的空地。";
                case "Select an adjacent empty floor tile.": return "请选择黄色高亮的相邻空地，或按 Esc 取消。";
                case "Select an adjacent empty floor tile within the clone limit.": return "该位置不能放置分身，请选择黄色高亮空地。";
                case "Clone created.": return "已创建分身，返回移动模式。";
                case "No living clones to recycle.": return "当前没有可回收的存活分身。";
                case "Select a living clone anywhere on the board.": return "请选择棋盘任意位置的黄色分身，或按 Esc 取消。";
                case "Only living clones can be recycled.": return "只能回收分身，不能回收玩家或初始普通细胞。";
                case "Clone recycled.": return "已回收分身并释放名额，返回移动模式。";
                case "Iteration limit exceeded.": return "迭代次数已超限，本次没有改变棋盘。可按 Z 撤销或 R 重试。";
                default: return message;
            }
        }
    }
}
