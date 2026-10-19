using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.Campaign
{
    internal static class CampaignUI
    {
        internal static TMP_FontAsset Font;
        internal static readonly Color Ink = new Color(.1f, .16f, .22f);
        internal static RectTransform Canvas(Transform parent, string name, int order = 40)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            return (RectTransform)go.transform;
        }
        internal static void EnsureInput()
        {
            if (EventSystem.current) return;
            new GameObject("Campaign EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
        internal static RectTransform Box(RectTransform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); Place(rect, x, y, w, h);
            go.GetComponent<Image>().color = color; return rect;
        }
        internal static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        }
        internal static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        internal static TextMeshProUGUI Label(RectTransform parent, string text, float x, float y, float w, float h, int size = 22)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); Place(rect, x, y, w, h);
            var label = go.GetComponent<TextMeshProUGUI>(); label.font = Font ? Font : TMP_Settings.defaultFontAsset;
            label.text = text; label.fontSize = size; label.color = Ink; label.raycastTarget = false;
            label.enableWordWrapping = true; return label;
        }
        internal static Button Button(RectTransform parent, string text, float x, float y, float w, Action clicked)
        {
            var rect = Box(parent, text, x, y, w, 48, new Color(.17f, .36f, .42f));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null); clicked(); });
            var label = Label(rect, text, 0, 0, w, 48, 20); label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        internal static RawImage Picture(RectTransform parent, Texture texture, float x, float y, float w, float h)
        {
            var go = new GameObject("Picture", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); Place(rect, x, y, w, h);
            var image = go.GetComponent<RawImage>(); image.texture = texture; image.raycastTarget = false;
            return image;
        }
    }
}
