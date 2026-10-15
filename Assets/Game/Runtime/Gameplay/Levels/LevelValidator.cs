using System;
using System.Collections.Generic;
using System.Linq;
using Game.Runtime.Data.Levels;

namespace Game.Runtime.Gameplay.Levels
{
    public sealed class LevelValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool CanPlay => Errors.Count == 0;
    }

    public static class LevelValidator
    {
        public static LevelValidationResult ValidateForPlay(LevelDefinition level)
        {
            var result = new LevelValidationResult();
            if (level == null) { result.Errors.Add("Level is missing."); return result; }
            if (level.schemaVersion != LevelIds.SchemaVersion) result.Errors.Add("Unsupported schemaVersion.");
            if (!LevelRepository.IsValidId(level.id)) result.Errors.Add("Invalid level ID.");
            if (level.iterationLimit <= 0) result.Errors.Add("iterationLimit must be positive.");
            if (level.cloneLimit < 0) result.Errors.Add("cloneLimit cannot be negative.");
            bool validGrid = level.grid != null && level.grid.width >= 1 && level.grid.width <= 30
                && level.grid.height >= 1 && level.grid.height <= 30;
            if (!validGrid) result.Errors.Add("Grid dimensions must be 1..30.");
            Func<int, int, bool> inside = (x, y) => validGrid && x >= 0 && y >= 0
                && x < level.grid.width && y < level.grid.height;
            var terrain = new Dictionary<string, string>();
            var occupied = new Dictionary<string, string>();
            var rules = new HashSet<string>();
            if (level.rules == null) result.Errors.Add("Rules list is missing.");
            else foreach (var rule in level.rules)
            {
                if (rule == null || (rule.id != LevelIds.LonelyDeath && rule.id != LevelIds.CrowdedDeath))
                    result.Errors.Add("Unknown or missing rule.");
                else if (!rules.Add(rule.id)) result.Errors.Add("Duplicate rule: " + rule.id);
            }
            if (rules.Count == 0) result.Warnings.Add("Both death rules are disabled; iteration will not change cells.");
            if (level.tiles == null) result.Errors.Add("Tiles list is missing.");
            else foreach (var tile in level.tiles)
            {
                if (tile == null) { result.Errors.Add("Null tile."); continue; }
                string key = Key(tile.x, tile.y);
                if (!inside(tile.x, tile.y)) result.Errors.Add("Tile outside grid: " + key);
                if (tile.type != LevelIds.Floor && tile.type != LevelIds.Wall) result.Errors.Add("Unknown tile type.");
                if (terrain.ContainsKey(key)) result.Errors.Add("Duplicate tile: " + key);
                else terrain.Add(key, tile.type);
            }
            int players = 0;
            if (level.cells == null) result.Errors.Add("Cells list is missing.");
            else foreach (var cell in level.cells)
            {
                if (cell == null) { result.Errors.Add("Null cell."); continue; }
                string key = Key(cell.x, cell.y);
                if (!inside(cell.x, cell.y) || !terrain.TryGetValue(key, out var ground) || ground != LevelIds.Floor)
                    result.Errors.Add("Cell must be on floor: " + key);
                if (cell.type == LevelIds.Player) players++;
                else if (cell.type != LevelIds.Normal) result.Errors.Add("Unknown cell type.");
                if (occupied.ContainsKey(key)) result.Errors.Add("Duplicate cell: " + key);
                else occupied.Add(key, cell.type);
            }
            if (players != 1) result.Errors.Add("Exactly one player is required.");
            // More than the maximum possible simultaneous clones is a configuration error.
            int capacity = terrain.Count(p => p.Value == LevelIds.Floor) - 1;
            if (validGrid && level.cloneLimit > Math.Max(0, capacity))
                result.Errors.Add("cloneLimit exceeds floor capacity excluding the player.");
            var goals = level.winConditions;
            if (goals == null || goals.combine != "all") result.Errors.Add("Only all goal combination is supported.");
            if (goals == null || goals.conditions == null || goals.conditions.Count == 0)
                result.Errors.Add("At least one goal is required.");
            else foreach (var goal in goals.conditions)
            {
                if (goal == null) { result.Errors.Add("Null goal."); continue; }
                switch (goal.type)
                {
                    case LevelIds.ActorAtPosition:
                        CheckPosition(goal.position, inside, terrain, result);
                        if (goal.positions != null || goal.@operator != null || goal.value.HasValue)
                            result.Errors.Add("Actor goal has unrelated parameters.");
                        break;
                    case LevelIds.CoverPositions:
                        if (goal.positions == null || goal.positions.Count == 0) result.Errors.Add("Cover positions cannot be empty.");
                        else
                        {
                            var seen = new HashSet<string>();
                            foreach (var pos in goal.positions)
                            {
                                CheckPosition(pos, inside, terrain, result);
                                if (pos != null && !seen.Add(Key(pos.x, pos.y))) result.Errors.Add("Duplicate goal position.");
                            }
                        }
                        if (goal.position != null || goal.@operator != null || goal.value.HasValue)
                            result.Errors.Add("Cover goal has unrelated parameters.");
                        break;
                    case LevelIds.CellCount:
                        if (goal.@operator != ">" && goal.@operator != "<") result.Errors.Add("Count comparison must be > or <.");
                        if (!goal.value.HasValue || goal.value < 0) result.Errors.Add("Count threshold must be a nonnegative integer.");
                        if (goal.position != null || goal.positions != null) result.Errors.Add("Count goal has unrelated positions.");
                        break;
                    default: result.Errors.Add("Unknown goal type."); break;
                }
            }
            if (result.CanPlay && InitialGoalsSatisfied(level, occupied))
                result.Warnings.Add("Initial state already satisfies all goals.");
            return result;
        }

        private static string Key(int x, int y) { return x + "," + y; }
        private static void CheckPosition(GridPosition pos, Func<int, int, bool> inside,
            Dictionary<string, string> terrain, LevelValidationResult result)
        {
            if (pos == null) { result.Errors.Add("Goal position is missing."); return; }
            string key = Key(pos.x, pos.y);
            if (!inside(pos.x, pos.y) || !terrain.TryGetValue(key, out var type) || type != LevelIds.Floor)
                result.Errors.Add("Goal must be on floor: " + key);
        }
        private static bool InitialGoalsSatisfied(LevelDefinition level, Dictionary<string, string> cells)
        {
            int normalCount = cells.Count(p => p.Value == LevelIds.Normal);
            foreach (var goal in level.winConditions.conditions)
            {
                if (goal.type == LevelIds.ActorAtPosition)
                {
                    if (!cells.TryGetValue(Key(goal.position.x, goal.position.y), out var type) || type != LevelIds.Player) return false;
                }
                else if (goal.type == LevelIds.CoverPositions)
                {
                    foreach (var pos in goal.positions)
                        if (!cells.TryGetValue(Key(pos.x, pos.y), out var type) || type != LevelIds.Normal) return false;
                }
                else if (goal.@operator == ">" ? normalCount <= goal.value.Value : normalCount >= goal.value.Value) return false;
            }
            return true;
        }
    }
}
