using UnityEngine;

namespace OrganicGrid
{
    public static class GridLife
    {
        public const int Size = 7;
        public static int Index(int x, int y) => y * Size + x;
        public static bool Contains(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Size && p.y < Size;
        public static int Neighbours(bool[] board, Vector2Int player, int x, int y)
        {
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var p = new Vector2Int(x + dx, y + dy);
                if (Contains(p) && (board[Index(p.x, p.y)] || p == player)) count++;
            }
            return count;
        }
        public static bool[] Next(bool[] board, Vector2Int player, bool births)
        {
            var result = new bool[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (player == new Vector2Int(x, y)) continue;
                int n = Neighbours(board, player, x, y);
                int i = Index(x, y);
                result[i] = board[i] ? n == 2 || n == 3 : births && n == 3;
            }
            return result;
        }
    }
}
