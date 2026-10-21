using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.Campaign
{
    /// <summary>Vector circle for the UGUI stencil mask or its outline; no generated bitmap required.</summary>
    [AddComponentMenu("UI/Scan Circle")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScanCircleGraphic : MaskableGraphic
    {
        [Min(0)] public float outlineWidth;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); const int segments = 96;
            var r = rectTransform.rect; Vector2 center = r.center;
            float radius = Mathf.Min(r.width, r.height) * .5f;
            if (outlineWidth <= 0)
            {
                mesh.AddVert(center, color, Vector2.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments;
                    mesh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero);
                    if (i > 0) mesh.AddTriangle(0, i, i + 1);
                }
            }
            else
            {
                float inner = Mathf.Max(0, radius - outlineWidth);
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments; var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    mesh.AddVert(center + v * radius, color, Vector2.zero);
                    mesh.AddVert(center + v * inner, color, Vector2.zero);
                    if (i == 0) continue;
                    int n = i * 2; mesh.AddTriangle(n - 2, n, n - 1); mesh.AddTriangle(n, n + 1, n - 1);
                }
            }
        }
    }
}
