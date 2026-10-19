using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.LevelPlay
{
    /// <summary>Small vector marks for progress and the two-cell clone action.</summary>
    public sealed class OrganicGlyphGraphic : MaskableGraphic
    {
        public bool paired;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * (paired ? .34f : .45f);
            if (paired)
            {
                Diamond(vh, rect.center + new Vector2(radius * .43f, radius * .30f), radius, new Color(.43f, .51f, .71f, color.a));
                Diamond(vh, rect.center - new Vector2(radius * .43f, radius * .30f), radius, color);
            }
            else Diamond(vh, rect.center, radius, color);
        }
        private static void Diamond(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            const int segments = 40;
            int start = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float x = Mathf.Cos(angle), y = Mathf.Sin(angle);
                float r = radius / Mathf.Pow(Mathf.Pow(Mathf.Abs(x), 1.55f) + Mathf.Pow(Mathf.Abs(y), 1.55f), 1 / 1.55f);
                vh.AddVert(center + new Vector2(x, y) * r, tint, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
