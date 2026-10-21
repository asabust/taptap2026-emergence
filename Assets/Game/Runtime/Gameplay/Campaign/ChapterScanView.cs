using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.Campaign
{
    /// <summary>Both full images share exactly the same transform; the stencil reveals only the circular overlap.</summary>
    [ExecuteAlways]
    public sealed class ChapterScanView : MonoBehaviour
    {
        public RectTransform viewport, scanCircle;
        public Image skeleton, anatomy;
        [Range(1, 8)] public float magnification = 3;
        public Vector2 previewFocus = new Vector2(.4f, .35f);
        private Vector2 focus;
        private bool runtimeFocus;
        public void Focus(Vector2 position)
        {
            focus = new Vector2(Mathf.Clamp01(position.x), Mathf.Clamp01(position.y)); runtimeFocus = true; Refresh();
        }
        private void OnEnable() { Refresh(); }
        private void LateUpdate() { Refresh(); }
        private void Refresh()
        {
            if (!viewport || !scanCircle || !skeleton || !anatomy || !skeleton.sprite) return;
            Vector2 p = Application.isPlaying && runtimeFocus ? focus : previewFocus;
            float aspect = skeleton.sprite.rect.width / skeleton.sprite.rect.height;
            float height = Mathf.Min(viewport.rect.height, viewport.rect.width / aspect) * magnification;
            Vector2 size = new Vector2(height * aspect, height);
            // Align the normalized source point to the lens center; do not clamp it at image edges.
            var target = skeleton.rectTransform;
            target.anchorMin = target.anchorMax = target.pivot = new Vector2(.5f,.5f);
            target.sizeDelta = size;
            Vector2 center = viewport.InverseTransformPoint(scanCircle.TransformPoint(scanCircle.rect.center));
            target.anchoredPosition = center - viewport.rect.center + Vector2.Scale(new Vector2(.5f,.5f) - p, size);
            var reveal = anatomy.rectTransform;
            reveal.anchorMin = reveal.anchorMax = reveal.pivot = new Vector2(.5f,.5f);
            reveal.sizeDelta = size; reveal.position = target.position;
        }
    }
}
