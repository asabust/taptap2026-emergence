using UnityEngine;

namespace OrganicGrid
{
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class OrganicGridDemo : MonoBehaviour
    {
        [Range(0.15f, 1.5f)] public float generationDuration = 0.55f;
        [Range(0.02f, 0.5f)] public float fusion = 0.3f;
        public bool births = true;
        public bool showGrid = true;
        public bool showNeighbourCounts;
        private bool[] board, pending;
        private readonly Vector4[] cells = new Vector4[49];
        private readonly Vector4[] nuclei = new Vector4[49];
        private readonly Vector4[] colors = new Vector4[49];
        private MaterialPropertyBlock block;
        private MeshRenderer meshRenderer;
        private Vector2Int player;
        private Vector2 playerFrom, playerVisual;
        private float generationTime = -1, movementTime = -1;
        private int generation, born, died;
        private bool goalReached;
        private string message;
        private static readonly Vector2Int Goal = new Vector2Int(3, 3);
        private bool Busy => generationTime >= 0 || movementTime >= 0;

        private void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            ResetBoard();
        }
        public void ResetBoard()
        {
            board = new bool[49];
            // A stable 2x2 block gives the target two neighbours until the player intervenes.
            board[GridLife.Index(2, 4)] = board[GridLife.Index(3, 4)] = true;
            board[GridLife.Index(2, 5)] = board[GridLife.Index(3, 5)] = true;
            player = new Vector2Int(5, 2);
            playerVisual = player;
            pending = null;
            generationTime = movementTime = -1;
            generation = born = died = 0;
            goalReached = false;
            message = "Reach the gold ring's neighbourhood; provide its third neighbour.";
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) ResetBoard();
            if (!Busy)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Move(Vector2Int.left);
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(Vector2Int.right);
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(Vector2Int.up);
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(Vector2Int.down);
                else if (Input.GetKeyDown(KeyCode.Space)) Step();
            }
            if (movementTime >= 0)
            {
                movementTime += Time.deltaTime;
                float p = Mathf.Clamp01(movementTime / 0.2f);
                playerVisual = Vector2.Lerp(playerFrom, player, Mathf.SmoothStep(0, 1, p));
                if (p >= 1) movementTime = -1;
            }
            if (generationTime >= 0)
            {
                generationTime += Time.deltaTime;
                if (generationTime >= generationDuration)
                {
                    board = pending;
                    pending = null;
                    generationTime = -1;
                    generation++;
                    if (board[GridLife.Index(Goal.x, Goal.y)]) goalReached = true;
                    message = goalReached ? "Target born! Continue experimenting, or Reset." : "Observe the nuclei: each nucleus represents one occupied square.";
                }
            }
            Upload();
        }
        public void Move(Vector2Int direction)
        {
            if (Busy) return;
            var next = player + direction;
            if (!GridLife.Contains(next) || board[GridLife.Index(next.x, next.y)]) return;
            playerFrom = playerVisual;
            player = next;
            movementTime = 0;
        }
        public void Step()
        {
            if (Busy) return;
            pending = GridLife.Next(board, player, births);
            born = died = 0;
            for (int i = 0; i < 49; i++)
            {
                if (!board[i] && pending[i]) born++;
                if (board[i] && !pending[i]) died++;
            }
            generationTime = 0;
            message = "All cells evolve together. Movement is paused during the transition.";
        }
        private void Upload()
        {
            float t = generationTime < 0 ? 1 : Mathf.Clamp01(generationTime / generationDuration);
            float eased = Mathf.SmoothStep(0, 1, t);
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
            {
                int i = GridLife.Index(x, y);
                bool was = board[i], will = pending == null ? was : pending[i];
                float life = Mathf.Lerp(was ? 1 : 0, will ? 1 : 0, eased);
                Vector2 center = new Vector2(x - 3, y - 3);
                // New membrane buds from the average position of its old neighbours.
                Vector2 source = Vector2.zero;
                int count = 0;
                if (!was && will)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var p = new Vector2Int(x + dx, y + dy);
                        if (GridLife.Contains(p) && (board[GridLife.Index(p.x, p.y)] || p == player))
                        { source += new Vector2(p.x - 3, p.y - 3); count++; }
                    }
                    if (count > 0) center = Vector2.Lerp(source / count, center, eased);
                }
                float nucleusAlpha = !was && will ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 0.35f) / 0.65f)) : life;
                cells[i] = new Vector4(center.x, center.y, 0.48f * life, life);
                nuclei[i] = new Vector4(x - 3, y - 3, nucleusAlpha, 0);
                colors[i] = new Color(0.38f + y * 0.025f, 0.66f, 0.8f + x * 0.018f);
            }
            meshRenderer.GetPropertyBlock(block);
            block.SetVectorArray("_Cells", cells);
            block.SetVectorArray("_Nuclei", nuclei);
            block.SetVectorArray("_CellColors", colors);
            block.SetVector("_Player", new Vector4(playerVisual.x - 3, playerVisual.y - 3, 0.46f, 0));
            block.SetVector("_PlayerTail", new Vector4(playerFrom.x - 3, playerFrom.y - 3, movementTime >= 0 ? (1 - Mathf.Clamp01(movementTime / 0.2f)) * 0.4f : 0, 0));
            block.SetFloat("_Fusion", fusion);
            block.SetFloat("_ShowGrid", showGrid ? 1 : 0);
            block.SetFloat("_GoalReached", goalReached ? 1 : 0);
            meshRenderer.SetPropertyBlock(block);
        }
        private void OnGUI()
        {
            if (board == null) return;
            float scale = Mathf.Clamp(Screen.height / 800f, 0.65f, 1.5f);
            var savedMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(16, 16, 420, 285), GUI.skin.box);
            GUILayout.Label("ORGANIC GRID  /  Generation " + generation);
            GUILayout.Label("WASD / arrows: move   |   Space: evolve   |   R: reset");
            GUILayout.Label("Blue: life   Coral: you   Gold ring: birth target");
            GUILayout.Label("Target neighbours: " + GridLife.Neighbours(board, player, Goal.x, Goal.y) + " / needs exactly 3");
            GUILayout.Label("Last step: +" + born + " births, -" + died + " deaths");
            GUILayout.Label(message, GUILayout.Height(42));
            GUI.enabled = !Busy;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Evolve (Space)")) Step();
            if (GUILayout.Button("Reset (R)")) ResetBoard();
            GUILayout.EndHorizontal();
            births = GUILayout.Toggle(births, "Enable births (B3 / S23)");
            GUI.enabled = true;
            showGrid = GUILayout.Toggle(showGrid, "Show grid");
            showNeighbourCounts = GUILayout.Toggle(showNeighbourCounts, "Show neighbour counts");
            GUILayout.EndArea();
            GUI.matrix = savedMatrix;
            if (showNeighbourCounts && Camera.main != null)
            {
                for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                {
                    var screen = Camera.main.WorldToScreenPoint(transform.TransformPoint(new Vector3((x - 3) / 8f, (y - 3) / 8f, 0)));
                    GUI.Label(new Rect(screen.x + 12, Screen.height - screen.y - 24, 30, 24), GridLife.Neighbours(board, player, x, y).ToString());
                }
            }
        }
    }
}
