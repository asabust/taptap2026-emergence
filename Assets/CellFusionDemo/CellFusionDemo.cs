using UnityEngine;

namespace CellFusion
{
    [ExecuteAlways, RequireComponent(typeof(MeshRenderer))]
    public sealed class CellFusionDemo : MonoBehaviour
    {
        public enum Look { Translucent, WhiteWithColorRim }
        public Look look;
        [Range(0.001f, 0.5f)] public float fusion = 0.22f;
        [Range(0, 1)] public float separation;
        public bool animate = true;
        [Range(0.05f, 3)] public float speed = 0.6f;
        [Range(0.002f, 0.15f)] public float rimWidth = 0.025f;
        [Range(0.005f, 0.25f)] public float glowWidth = 0.07f;
        [Range(0, 1)] public float opacity = 0.55f;
        [Range(0, 1)] public float nucleus = 0.7f;

        private readonly Vector4[] cells = new Vector4[8];
        private readonly Vector4[] colors = new Vector4[8];
        private MaterialPropertyBlock block;
        private MeshRenderer target;

        private void Update()
        {
            if (!target) target = GetComponent<MeshRenderer>();
            if (block == null) block = new MaterialPropertyBlock();
            float t = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartupAsDouble;
            float phase = animate ? 0.5f - 0.5f * Mathf.Cos(t * speed) : separation;
            float distance = Mathf.Lerp(0.08f, 0.9f, phase);
            cells[0] = new Vector4(-distance, 0.08f * Mathf.Sin(t * speed), 0.52f, 0);
            cells[1] = new Vector4(distance, -0.08f * Mathf.Sin(t * speed), 0.48f, 0);
            cells[2] = new Vector4(0.28f * Mathf.Sin(t * speed * 0.7f), 0.74f, 0.22f, 0);
            colors[0] = new Color(0.35f, 0.7f, 0.95f);
            colors[1] = new Color(1, 0.43f, 0.52f);
            colors[2] = new Color(0.65f, 0.85f, 0.38f);
            target.GetPropertyBlock(block);
            block.SetVectorArray("_Cells", cells);
            block.SetVectorArray("_CellColors", colors);
            block.SetInt("_CellCount", 3);
            // Radius and positions are in local world units; non-square quads retain circular cells.
            block.SetVector("_Domain", new Vector4(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y), 0, 0));
            block.SetFloat("_Fusion", fusion);
            block.SetFloat("_RimWidth", rimWidth);
            block.SetFloat("_GlowWidth", glowWidth);
            block.SetFloat("_Opacity", opacity);
            block.SetFloat("_Nucleus", nucleus);
            block.SetFloat("_Style", look == Look.WhiteWithColorRim ? 1 : 0);
            target.SetPropertyBlock(block);
        }
    }
}
