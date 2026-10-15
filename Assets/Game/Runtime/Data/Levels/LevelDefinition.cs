using System;
using System.Collections.Generic;

namespace Game.Runtime.Data.Levels
{
    // Plain data only. JSON field names intentionally match the GDD contract.
    [Serializable]
    public sealed class LevelDefinition
    {
        public int schemaVersion = 1;
        public string id;
        public string name;
        public GridSize grid = new GridSize();
        public List<LevelRule> rules = new List<LevelRule>();
        public int iterationLimit = 1000;
        public int cloneLimit;
        public List<LevelTile> tiles = new List<LevelTile>();
        public List<LevelCell> cells = new List<LevelCell>();
        public LevelGoals winConditions = new LevelGoals();
    }

    [Serializable]
    public sealed class GridSize { public int width; public int height; }
    [Serializable]
    public sealed class GridPosition { public int x; public int y; }
    [Serializable]
    public sealed class LevelTile { public int x; public int y; public string type; }
    [Serializable]
    public sealed class LevelCell { public int x; public int y; public string type; }
    [Serializable]
    public sealed class LevelRule { public string id; }
    [Serializable]
    public sealed class LevelGoals
    {
        public string combine = "all";
        public List<LevelGoal> conditions = new List<LevelGoal>();
    }
    [Serializable]
    public sealed class LevelGoal
    {
        public string type;
        public GridPosition position;
        public List<GridPosition> positions;
        // Nullable distinguishes an unfinished draft from the valid threshold zero.
        public string @operator;
        public int? value;
    }

    public static class LevelIds
    {
        public const int SchemaVersion = 1;
        public const string Floor = "floor", Wall = "wall";
        public const string Normal = "normal", Player = "player";
        public const string LonelyDeath = "lonelyDeath", CrowdedDeath = "crowdedDeath";
        public const string ActorAtPosition = "actorAtPosition";
        public const string CellCount = "livingCellCount";
        public const string CoverPositions = "livingCellsCoverPositions";
    }
}
