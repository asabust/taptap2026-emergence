using System;
using System.Collections.Generic;
using Game.Runtime.Data.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.LevelEditor
{
    /// <summary>Stage 2 editor shell. Scene bootstrap creates this UI at runtime.</summary>
    public sealed class LevelEditorScreen : MonoBehaviour
    {
        public TMP_FontAsset uiFont;
        public LevelEditorSession Session { get; private set; }
        private LevelEditorBoardView board;
        private TMP_InputField widthInput, heightInput;
        private TextMeshProUGUI status, summary, brushLabel;
        private readonly Dictionary<LevelBrush, Image> brushImages = new Dictionary<LevelBrush, Image>();
        private LevelBrush brush = LevelBrush.Floor;
        private GameObject canvasObject, ownedEventSystem;
        private static readonly Color Ink = new Color(.86f, .9f, .95f);
        private static readonly Color Panel = new Color(.09f, .13f, .2f);

        private void Awake()
        {
            if (!uiFont) uiFont = TMP_Settings.defaultFontAsset;
            Session = new LevelEditorSession();
            Build();
            Session.Changed += Refresh;
            board.CellClicked += Paint;
            Refresh();
            SetStatus("选择左侧画笔，点击右侧单个格子。当前是编辑模式，不执行游戏规则。");
        }
        private void OnDestroy()
        {
            if (Session != null) Session.Changed -= Refresh;
            if (board) board.CellClicked -= Paint;
            if (canvasObject) Destroy(canvasObject);
            if (ownedEventSystem) Destroy(ownedEventSystem);
        }
        private void Build()
        {
            canvasObject = new GameObject("Level Editor Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // Keep generated objects with the scene/bootstrap, not a global persistent object.
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            RectTransform root = (RectTransform)canvasObject.transform;
            Fill(root, "Background", new Color(.055f, .08f, .13f));
            Label(root, "LEVEL EDITOR  /  关卡编辑器", 24, 18, 850, 36, 25);
            Label(root, "阶段 2 · 基础编辑     左下角 (0,0)  /  X 向右，Y 向上", 24, 60, 1100, 30, 17);
            RectTransform tools = Box(root, "Tools", 20, 110, 286, 690, Panel);
            Label(tools, "网格尺寸  1 — 30", 16, 14, 250, 28, 19);
            Label(tools, "X", 16, 54, 20, 32, 18);
            widthInput = NumberInput(tools, 42, 52, "10");
            Label(tools, "Y", 154, 54, 20, 32, 18);
            heightInput = NumberInput(tools, 180, 52, "10");
            Button(tools, "居中调整尺寸", 16, 100, 254, 36, () => ChangeSize(false));
            Button(tools, "新建空白关卡", 16, 146, 254, 36, () => ChangeSize(true));
            Label(tools, "画笔  /  单击一格", 16, 202, 254, 28, 19);
            string[] names = { "有效地块", "墙面", "普通细胞 C", "玩家 P", "擦除细胞", "删除地块", "角色终点 G", "细胞目标 T", "擦除目标" };
            var values = (LevelBrush[])Enum.GetValues(typeof(LevelBrush));
            for (int i = 0; i < values.Length; i++)
            {
                LevelBrush selected = values[i];
                var b = Button(tools, names[i], 16 + (i % 2) * 132, 244 + (i / 2) * 44, 122, 36, () => SelectBrush(selected));
                brushImages.Add(selected, b.GetComponent<Image>());
            }
            brushLabel = Label(tools, "", 16, 476, 254, 44, 16);
            Label(tools, "删除地块会清除该格细胞与目标。\n缩小直接裁切，扩大不会恢复。", 16, 530, 254, 66, 15);
            Label(tools, "保存、规则与额度面板\n将在阶段 3 接入。", 16, 613, 254, 56, 15);
            var area = new GameObject("Board Area", typeof(RectTransform));
            var areaRect = (RectTransform)area.transform; areaRect.SetParent(root, false);
            areaRect.anchorMin = Vector2.zero; areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(336, 110); areaRect.offsetMax = new Vector2(-26, -114);
            var boardObject = new GameObject("Board", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter), typeof(LevelEditorBoardView));
            var br = (RectTransform)boardObject.transform; br.SetParent(areaRect, false);
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = br.offsetMax = Vector2.zero;
            boardObject.GetComponent<Image>().color = new Color(.18f, .23f, .3f);
            boardObject.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            board = boardObject.GetComponent<LevelEditorBoardView>();
            summary = Label(root, "", 336, 0, 1000, 34, 17);
            // Bottom anchored status and summary remain visible at different aspect ratios.
            Bottom(summary.rectTransform, 336, 67, -26, 32);
            status = Label(root, "", 24, 0, 1390, 48, 16);
            Bottom(status.rectTransform, 24, 12, -24, 46);
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Level Editor EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(transform, false);
            }
        }
        private void Refresh()
        {
            board.Render(Session, uiFont);
            widthInput.SetTextWithoutNotify(Session.Draft.grid.width.ToString());
            heightInput.SetTextWithoutNotify(Session.Draft.grid.height.ToString());
            int normal = Session.Draft.cells.FindAll(c => c.type == LevelIds.Normal).Count;
            bool player = Session.Draft.cells.Exists(c => c.type == LevelIds.Player);
            summary.text = Session.Draft.grid.width + " × " + Session.Draft.grid.height + "   |   普通细胞 " + normal
                + "   |   玩家 " + (player ? "已放置" : "未放置（草稿）") + "   |   P 玩家 · C 细胞 · G 出口 · T 细胞目标";
            SelectBrush(brush);
        }
        private void SelectBrush(LevelBrush selected)
        {
            brush = selected;
            foreach (var entry in brushImages) entry.Value.color = entry.Key == brush ? new Color(.2f, .46f, .56f) : new Color(.16f, .22f, .3f);
            brushLabel.text = "当前画笔：" + brushImages[selected].GetComponentInChildren<TextMeshProUGUI>().text;
        }
        private void Paint(int x, int y)
        {
            bool ok = Session.TryPaint(x, y, brush, out string message);
            SetStatus(message, !ok);
        }
        private void ChangeSize(bool create)
        {
            if (!int.TryParse(widthInput.text, out int width) || !int.TryParse(heightInput.text, out int height)
                || width < 1 || width > 30 || height < 1 || height > 30)
            { SetStatus("X、Y 必须是 1～30 的整数；草稿没有改变。", true); return; }
            if (create) Session.New(width, height); else Session.Resize(width, height);
            SetStatus(create ? "新建完成，全部为有效地块。" : "已居中调整尺寸；范围外内容已丢弃，新增区域为空。请检查玩家和目标。");
        }
        private void SetStatus(string text, bool error = false) { status.text = text; status.color = error ? new Color(1f, .65f, .49f) : Ink; }
        private TMP_InputField NumberInput(RectTransform parent, float x, float y, string value)
        {
            var rect = Box(parent, "Size input", x, y, 84, 36, new Color(.18f, .25f, .34f));
            var field = rect.gameObject.AddComponent<TMP_InputField>();
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var vr = (RectTransform)viewport.transform; vr.SetParent(rect, false); vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = new Vector2(6, 2); vr.offsetMax = new Vector2(-6, -2);
            var text = Label(vr, "", 0, 0, 72, 32, 19); Stretch(text.rectTransform);
            field.textViewport = vr; field.textComponent = text; field.targetGraphic = rect.GetComponent<Image>();
            field.contentType = TMP_InputField.ContentType.IntegerNumber; field.characterLimit = 3; field.text = value;
            return field;
        }
        private Button Button(RectTransform parent, string text, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var rect = Box(parent, text, x, y, w, h, new Color(.16f, .22f, .3f));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(action);
            var label = Label(rect, text, 4, 1, w - 8, h - 2, 17); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private TextMeshProUGUI Label(RectTransform parent, string text, float x, float y, float w, float h, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); Place(rect, x, y, w, h);
            var label = go.GetComponent<TextMeshProUGUI>(); label.font = uiFont; label.fontSize = size; label.color = Ink;
            label.text = text; label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = true;
            return label;
        }
        private static RectTransform Box(RectTransform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); Place(rect, x, y, w, h);
            go.GetComponent<Image>().color = color; return rect;
        }
        private static void Fill(RectTransform parent, string name, Color color)
        { var rect = Box(parent, name, 0, 0, 0, 0, color); Stretch(rect); }
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Bottom(RectTransform rect, float left, float bottom, float right, float h)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0); rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(right, bottom + h); }
    }
}
