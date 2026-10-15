using System;
using Game.Runtime.Data.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.LevelEditor
{
    /// <summary>UI board; children never consume raycasts, so picking has a single owner.</summary>
    public sealed class LevelEditorBoardView : MonoBehaviour, IPointerClickHandler
    {
        private Image[] backgrounds;
        private TextMeshProUGUI[] labels;
        private int width, height;
        private TMP_FontAsset font;
        public event Action<int, int> CellClicked;

        public void Render(LevelEditorSession session, TMP_FontAsset uiFont)
        {
            font = uiFont;
            if (backgrounds == null || width != session.Draft.grid.width || height != session.Draft.grid.height)
                Rebuild(session.Draft.grid.width, session.Draft.grid.height);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = x + y * width;
                string ground = session.TerrainAt(x, y), cell = session.CellAt(x, y);
                bool actorGoal = session.IsActorGoal(x, y), cellGoal = session.IsCellGoal(x, y);
                Color color = ground == LevelIds.Floor ? new Color(.84f, .88f, .9f)
                    : ground == LevelIds.Wall ? new Color(.28f, .34f, .4f) : new Color(.1f, .14f, .2f);
                if (cell == LevelIds.Normal) color = new Color(.29f, .67f, .65f);
                if (cell == LevelIds.Player) color = new Color(1f, .69f, .38f);
                if (cell == null && (actorGoal || cellGoal)) color = new Color(.65f, .57f, .88f);
                backgrounds[i].color = color;
                string mark = cell == LevelIds.Player ? "P" : cell == LevelIds.Normal ? "C" : "";
                if (actorGoal) mark += " G";
                if (cellGoal) mark += " T";
                labels[i].text = mark.Trim();
                labels[i].color = ground == LevelIds.Floor ? new Color(.07f, .12f, .18f) : Color.white;
            }
        }

        private void Rebuild(int w, int h)
        {
            foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            width = w; height = h;
            GetComponent<AspectRatioFitter>().aspectRatio = (float)w / h;
            backgrounds = new Image[w * h]; labels = new TextMeshProUGUI[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int i = x + y * w;
                var tile = new GameObject("Cell " + x + "," + y, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = (RectTransform)tile.transform; rect.SetParent(transform, false);
                rect.anchorMin = new Vector2((float)x / w, (float)y / h);
                rect.anchorMax = new Vector2((float)(x + 1) / w, (float)(y + 1) / h);
                rect.offsetMin = Vector2.one; rect.offsetMax = -Vector2.one;
                backgrounds[i] = tile.GetComponent<Image>(); backgrounds[i].raycastTarget = false;
                var text = new GameObject("Mark", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                var tr = (RectTransform)text.transform; tr.SetParent(rect, false);
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
                labels[i] = text.GetComponent<TextMeshProUGUI>();
                labels[i].font = font; labels[i].alignment = TextAlignmentOptions.Center;
                labels[i].enableAutoSizing = true; labels[i].fontSizeMin = 6; labels[i].fontSizeMax = 28;
                labels[i].raycastTarget = false;
            }
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || width == 0) return;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var point)) return;
            Rect bounds = rect.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            if (LevelEditorSession.TryPickNormalized((point.x - bounds.xMin) / bounds.width,
                (point.y - bounds.yMin) / bounds.height, width, height, out int x, out int y))
                CellClicked?.Invoke(x, y);
        }
    }
}
