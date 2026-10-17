using System;
using System.Collections.Generic;
using Game.Runtime.Data.Levels;
using Game.Runtime.Gameplay.Levels;

namespace Game.Runtime.Gameplay.LevelEditor
{
    public enum LevelBrush { Floor, Wall, Normal, Player, EraseCell, EraseTerrain, ActorGoal, CellGoal, EraseGoals, EraseSelectedGoal }

    /// <summary>Single editor draft owner. No scene objects, input or file I/O.</summary>
    public sealed class LevelEditorSession
    {
        public LevelDefinition Draft { get; private set; }
        public event Action Changed;
        public int SelectedGoalIndex { get; private set; } = -1;

        public LevelEditorSession(int width = 10, int height = 10) { New(width, height); }

        public void New(int width, int height)
        {
            RequireSize(width, height);
            var next = new LevelDefinition
            {
                id = "untitled", name = "未命名关卡", grid = new GridSize { width = width, height = height }
            };
            next.rules.Add(new LevelRule { id = LevelIds.LonelyDeath });
            next.rules.Add(new LevelRule { id = LevelIds.CrowdedDeath });
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                next.tiles.Add(new LevelTile { x = x, y = y, type = LevelIds.Floor });
            Draft = next;
            SelectedGoalIndex = -1;
            Changed?.Invoke();
        }

        // File UI in stage 3 should call this only after Load succeeds.
        public void ReplaceDraft(LevelDefinition definition)
        {
            if (definition == null || definition.grid == null) throw new ArgumentException("草稿缺少网格。");
            RequireSize(definition.grid.width, definition.grid.height);
            var copy = LevelJson.DeepCopy(definition);
            if (copy.rules == null || copy.tiles == null || copy.cells == null || copy.winConditions == null || copy.winConditions.conditions == null)
                throw new ArgumentException("草稿缺少必要列表。");
            Draft = copy;
            SelectedGoalIndex = copy.winConditions.conditions.Count == 0 ? -1 : 0;
            Changed?.Invoke();
        }

        public bool Contains(int x, int y) { return x >= 0 && y >= 0 && x < Draft.grid.width && y < Draft.grid.height; }
        public string TerrainAt(int x, int y) { return Draft.tiles.Find(t => t.x == x && t.y == y)?.type; }
        public string CellAt(int x, int y) { return Draft.cells.Find(c => c.x == x && c.y == y)?.type; }
        public bool IsActorGoal(int x, int y)
        {
            return Draft.winConditions.conditions.Exists(g => g.type == LevelIds.ActorAtPosition && Same(g.position, x, y));
        }
        public bool IsCellGoal(int x, int y)
        {
            return Draft.winConditions.conditions.Exists(g => g.type == LevelIds.CoverPositions && g.positions != null
                && g.positions.Exists(p => Same(p, x, y)));
        }

        public bool TryPaint(int x, int y, LevelBrush brush, out string message)
        {
            if (!Contains(x, y)) { message = "位置不在网格内。"; return false; }
            bool needsFloor = brush == LevelBrush.Normal || brush == LevelBrush.Player
                || brush == LevelBrush.ActorGoal || brush == LevelBrush.CellGoal;
            if (needsFloor && TerrainAt(x, y) != LevelIds.Floor)
            { message = "请先放置有效地块。"; return false; }
            switch (brush)
            {
                case LevelBrush.Floor:
                case LevelBrush.Wall:
                    Draft.tiles.RemoveAll(t => t.x == x && t.y == y);
                    Draft.tiles.Add(new LevelTile { x = x, y = y, type = brush == LevelBrush.Floor ? LevelIds.Floor : LevelIds.Wall });
                    if (brush == LevelBrush.Wall) ClearContents(x, y);
                    break;
                case LevelBrush.EraseTerrain:
                    Draft.tiles.RemoveAll(t => t.x == x && t.y == y);
                    ClearContents(x, y);
                    break;
                case LevelBrush.Normal:
                    if (CellAt(x, y) == LevelIds.Player) { message = "不能用普通细胞覆盖玩家，请先移走玩家。"; return false; }
                    Draft.cells.RemoveAll(c => c.x == x && c.y == y);
                    Draft.cells.Add(new LevelCell { x = x, y = y, type = LevelIds.Normal });
                    break;
                case LevelBrush.Player:
                    if (CellAt(x, y) == LevelIds.Normal) { message = "该格已有普通细胞，请先删除。"; return false; }
                    Draft.cells.RemoveAll(c => c.type == LevelIds.Player);
                    Draft.cells.Add(new LevelCell { x = x, y = y, type = LevelIds.Player });
                    break;
                case LevelBrush.EraseCell:
                    Draft.cells.RemoveAll(c => c.x == x && c.y == y);
                    break;
                case LevelBrush.ActorGoal:
                    var actor = SelectedOrFirst(LevelIds.ActorAtPosition);
                    if (actor == null) { actor = new LevelGoal { type = LevelIds.ActorAtPosition }; Draft.winConditions.conditions.Add(actor); }
                    actor.position = new GridPosition { x = x, y = y };
                    SelectedGoalIndex = Draft.winConditions.conditions.IndexOf(actor);
                    break;
                case LevelBrush.CellGoal:
                    var cover = SelectedOrFirst(LevelIds.CoverPositions);
                    if (cover == null)
                    {
                        cover = new LevelGoal { type = LevelIds.CoverPositions, positions = new List<GridPosition>() };
                        Draft.winConditions.conditions.Add(cover);
                    }
                    if (cover.positions == null) cover.positions = new List<GridPosition>();
                    if (!cover.positions.Exists(p => Same(p, x, y))) cover.positions.Add(new GridPosition { x = x, y = y });
                    SelectedGoalIndex = Draft.winConditions.conditions.IndexOf(cover);
                    break;
                case LevelBrush.EraseGoals:
                    RemoveGoalsAt(x, y);
                    break;
                case LevelBrush.EraseSelectedGoal:
                    if (SelectedGoalIndex < 0 || SelectedGoalIndex >= Draft.winConditions.conditions.Count)
                    { message = "请先选择一个目标。"; return false; }
                    var selectedGoal = Draft.winConditions.conditions[SelectedGoalIndex];
                    if (Same(selectedGoal.position, x, y)) selectedGoal.position = null;
                    selectedGoal.positions?.RemoveAll(p => Same(p, x, y));
                    break;
                default: message = "未知画笔。"; return false;
            }
            message = "已编辑 (" + x + ", " + y + ")";
            Changed?.Invoke();
            return true;
        }

        public bool TryConfigure(bool lonely, bool crowded, string cloneText, string iterationText, out string message)
        {
            int clones = 0, iterations = 1000;
            if ((!string.IsNullOrWhiteSpace(cloneText) && !int.TryParse(cloneText, out clones)) || clones < 0)
            { message = "分身上限必须是非负整数，留空为 0。"; return false; }
            if ((!string.IsNullOrWhiteSpace(iterationText) && !int.TryParse(iterationText, out iterations)) || iterations <= 0)
            { message = "迭代上限必须是正整数，留空为 1000。"; return false; }
            Draft.cloneLimit = clones; Draft.iterationLimit = iterations;
            Draft.rules.Clear();
            if (lonely) Draft.rules.Add(new LevelRule { id = LevelIds.LonelyDeath });
            if (crowded) Draft.rules.Add(new LevelRule { id = LevelIds.CrowdedDeath });
            message = lonely || crowded ? "配置已应用。" : "配置已应用：两种死亡规则均关闭，迭代不会改变细胞。";
            Changed?.Invoke();
            return true;
        }
        public int AddGoal(string type)
        {
            if (type != LevelIds.ActorAtPosition && type != LevelIds.CellCount && type != LevelIds.CoverPositions)
                throw new ArgumentException("未知目标类型。");
            var goal = new LevelGoal { type = type };
            if (type == LevelIds.CoverPositions) goal.positions = new List<GridPosition>();
            if (type == LevelIds.CellCount) goal.@operator = ">";
            Draft.winConditions.conditions.Add(goal);
            SelectedGoalIndex = Draft.winConditions.conditions.Count - 1;
            Changed?.Invoke();
            return SelectedGoalIndex;
        }
        public void SelectGoal(int index)
        {
            if (index < -1 || index >= Draft.winConditions.conditions.Count) throw new ArgumentOutOfRangeException(nameof(index));
            SelectedGoalIndex = index;
        }
        public void RemoveSelectedGoal()
        {
            if (SelectedGoalIndex < 0) return;
            Draft.winConditions.conditions.RemoveAt(SelectedGoalIndex);
            SelectedGoalIndex = Math.Min(SelectedGoalIndex, Draft.winConditions.conditions.Count - 1);
            Changed?.Invoke();
        }
        public bool TrySetCountGoal(string comparison, string text, out string message)
        {
            if (SelectedGoalIndex < 0 || Draft.winConditions.conditions[SelectedGoalIndex].type != LevelIds.CellCount)
            { message = "请先选择一个数量目标。"; return false; }
            int value = 0;
            bool empty = string.IsNullOrWhiteSpace(text);
            if ((comparison != ">" && comparison != "<") || (!empty && !int.TryParse(text, out value)) || value < 0)
            { message = "数量阈值必须是非负整数，比较符只能为 > 或 <。"; return false; }
            var goal = Draft.winConditions.conditions[SelectedGoalIndex];
            goal.@operator = comparison; goal.value = empty ? (int?)null : value;
            message = empty ? "阈值未填写，可保存草稿，但不能试玩。" : "数量目标已应用。";
            Changed?.Invoke();
            return true;
        }
        public void ClearSelectedGoalPositions()
        {
            if (SelectedGoalIndex < 0) return;
            var goal = Draft.winConditions.conditions[SelectedGoalIndex];
            goal.position = null; goal.positions?.Clear();
            Changed?.Invoke();
        }
        private LevelGoal SelectedOrFirst(string type)
        {
            if (SelectedGoalIndex >= 0 && SelectedGoalIndex < Draft.winConditions.conditions.Count
                && Draft.winConditions.conditions[SelectedGoalIndex].type == type)
                return Draft.winConditions.conditions[SelectedGoalIndex];
            return Draft.winConditions.conditions.Find(g => g.type == type);
        }

        public void Resize(int width, int height)
        {
            RequireSize(width, height);
            if (width == Draft.grid.width && height == Draft.grid.height) return;
            // Math.Floor is essential for negative odd differences (6 -> 5 is -1, not 0).
            int dx = (int)Math.Floor((width - Draft.grid.width) / 2.0);
            int dy = (int)Math.Floor((height - Draft.grid.height) / 2.0);
            var next = LevelJson.DeepCopy(Draft);
            foreach (var tile in next.tiles) { tile.x += dx; tile.y += dy; }
            next.tiles.RemoveAll(t => !Inside(t.x, t.y, width, height));
            foreach (var cell in next.cells) { cell.x += dx; cell.y += dy; }
            next.cells.RemoveAll(c => !Inside(c.x, c.y, width, height));
            foreach (var goal in next.winConditions.conditions)
            {
                if (goal.position != null)
                {
                    goal.position.x += dx; goal.position.y += dy;
                    if (!Inside(goal.position.x, goal.position.y, width, height)) goal.position = null;
                }
                if (goal.positions == null) continue;
                foreach (var pos in goal.positions) { pos.x += dx; pos.y += dy; }
                goal.positions.RemoveAll(p => !Inside(p.x, p.y, width, height));
            }
            next.grid.width = width; next.grid.height = height;
            Draft = next;
            Changed?.Invoke();
        }

        public static bool TryPickNormalized(float u, float v, int width, int height, out int x, out int y)
        {
            x = y = -1;
            if (width < 1 || height < 1 || float.IsNaN(u) || float.IsNaN(v)
                || u < 0 || v < 0 || u >= 1 || v >= 1) return false;
            x = (int)Math.Floor(u * width); y = (int)Math.Floor(v * height);
            return Inside(x, y, width, height);
        }
        private void ClearContents(int x, int y) { Draft.cells.RemoveAll(c => c.x == x && c.y == y); RemoveGoalsAt(x, y); }
        private void RemoveGoalsAt(int x, int y)
        {
            foreach (var goal in Draft.winConditions.conditions)
            {
                if (goal.type == LevelIds.ActorAtPosition && Same(goal.position, x, y)) goal.position = null;
                if (goal.type == LevelIds.CoverPositions && goal.positions != null) goal.positions.RemoveAll(p => Same(p, x, y));
            }
        }
        private static bool Same(GridPosition p, int x, int y) { return p != null && p.x == x && p.y == y; }
        private static bool Inside(int x, int y, int width, int height) { return x >= 0 && y >= 0 && x < width && y < height; }
        private static void RequireSize(int width, int height)
        {
            if (width < 1 || width > 30 || height < 1 || height > 30) throw new ArgumentOutOfRangeException("size", "尺寸必须为 1～30。");
        }
    }
}
