using System;
using System.Collections.Generic;
using Game.Runtime.Data.Levels;

namespace Game.Runtime.Gameplay.Levels
{
    public enum LevelPlayStatus { Playing, Won, Failed }
    public enum LevelInteractionMode { Move, PlaceClone, RecycleClone }

    /// <summary>Scene-independent rules shared by Gameplay and editor playtesting.</summary>
    public sealed class LevelPlaySession
    {
        private readonly LevelDefinition definition;
        private readonly bool[,] floor;
        private readonly bool lonelyDeath;
        private readonly bool crowdedDeath;
        private LevelState state;
        private readonly LevelState initialState;
        private readonly List<LevelState> history = new List<LevelState>();
        public const int HistoryCapacity = 100;

        // Return copies so UI and editor code cannot bypass the operation contract.
        public LevelState State => state.DeepCopy();
        public LevelDefinition Definition => LevelJson.DeepCopy(definition);
        public LevelPlayStatus Status { get; private set; }
        public LevelInteractionMode Mode { get; private set; }
        public int LivingCloneCount => state.LivingCloneCount;
        public int Iteration => state.Iteration;
        public int CloneLimit => definition.cloneLimit;
        public int IterationLimit => definition.iterationLimit;
        public int HistoryCount => history.Count;
        public bool CanUndo => history.Count > 0;

        public LevelPlaySession(LevelDefinition initial)
        {
            var validation = LevelValidator.ValidateForPlay(initial);
            if (!validation.CanPlay) throw new ArgumentException(string.Join("\n", validation.Errors), nameof(initial));
            definition = LevelJson.DeepCopy(initial);
            state = LevelState.FromDefinition(definition);
            initialState = state.DeepCopy();
            floor = new bool[definition.grid.width, definition.grid.height];
            foreach (var tile in definition.tiles)
                floor[tile.x, tile.y] = tile.type == LevelIds.Floor;
            foreach (var rule in definition.rules)
            {
                if (rule.id == LevelIds.LonelyDeath) lonelyDeath = true;
                if (rule.id == LevelIds.CrowdedDeath) crowdedDeath = true;
            }
            EvaluateGoals();
        }

        public bool TryMove(int dx, int dy, out string message)
        {
            if (!CanOperate(out message)) return false;
            if (Mode != LevelInteractionMode.Move) return Reject("Cancel the current selection before moving.", out message);
            if (!((dx == 0 && (dy == -1 || dy == 1)) || (dy == 0 && (dx == -1 || dx == 1))))
                return Reject("Move must be one orthogonal step.", out message);
            int x = state.PlayerX + dx, y = state.PlayerY + dy;
            if (!IsFloor(x, y)) return Reject("Destination is not floor.", out message);
            if (CellAt(x, y) != null) return Reject("Destination is occupied by a living cell.", out message);
            state.PlayerX = x;
            state.PlayerY = y;
            EvaluateGoals();
            message = "Moved.";
            return true;
        }

        public bool TryBeginClonePlacement(out string message)
        {
            if (!CanOperate(out message)) return false;
            if (CloneLimit == 0) return Reject("Cloning is disabled in this level.", out message);
            if (LivingCloneCount >= CloneLimit) return Reject("Clone limit reached; recycle a clone first.", out message);
            bool available = false;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    available |= CanPlaceCloneAt(state.PlayerX + dx, state.PlayerY + dy);
            if (!available) return Reject("No adjacent empty floor is available.", out message);
            Mode = LevelInteractionMode.PlaceClone;
            message = "Select an adjacent empty floor tile.";
            return true;
        }

        public bool CanPlaceCloneAt(int x, int y)
        {
            if (Status != LevelPlayStatus.Playing || LivingCloneCount >= CloneLimit || !IsFloor(x, y)) return false;
            int dx = x - state.PlayerX, dy = y - state.PlayerY;
            return dx >= -1 && dx <= 1 && dy >= -1 && dy <= 1 && (dx != 0 || dy != 0) && CellAt(x, y) == null;
        }

        public bool TryCreateClone(int x, int y, out string message)
        {
            if (!CanOperate(out message)) return false;
            if (Mode != LevelInteractionMode.PlaceClone) return Reject("Enter clone placement first.", out message);
            if (!CanPlaceCloneAt(x, y)) return Reject("Select an adjacent empty floor tile within the clone limit.", out message);
            state.Cells.Add(new LivingCellState { X = x, Y = y, IsClone = true });
            Mode = LevelInteractionMode.Move;
            EvaluateGoals();
            message = "Clone created.";
            return true;
        }

        public bool TryBeginRecycling(out string message)
        {
            if (!CanOperate(out message)) return false;
            if (LivingCloneCount == 0) return Reject("No living clones to recycle.", out message);
            Mode = LevelInteractionMode.RecycleClone;
            message = "Select a living clone anywhere on the board.";
            return true;
        }

        public bool TryRecycleClone(int x, int y, out string message)
        {
            if (!CanOperate(out message)) return false;
            if (Mode != LevelInteractionMode.RecycleClone) return Reject("Enter recycling first.", out message);
            var cell = CellAt(x, y);
            if (cell == null || !cell.IsClone) return Reject("Only living clones can be recycled.", out message);
            state.Cells.Remove(cell);
            Mode = LevelInteractionMode.Move;
            EvaluateGoals();
            message = "Clone recycled.";
            return true;
        }

        public void CancelSelection() { Mode = LevelInteractionMode.Move; }

        public bool TryUndo(out string message)
        {
            if (!CanUndo) return Reject("No iteration history to undo.", out message);
            state = history[history.Count - 1].DeepCopy();
            history.RemoveAt(history.Count - 1);
            Mode = LevelInteractionMode.Move;
            EvaluateGoals();
            message = "Iteration undone.";
            return true;
        }

        public void Reset()
        {
            state = initialState.DeepCopy();
            history.Clear();
            Mode = LevelInteractionMode.Move;
            EvaluateGoals();
        }

        public bool TryIterate(out string message)
        {
            if (!CanOperate(out message)) return false;
            if (Mode != LevelInteractionMode.Move) return Reject("Cancel the current selection before iterating.", out message);
            if (state.Iteration >= IterationLimit)
            {
                Status = LevelPlayStatus.Failed;
                return Reject("Iteration limit exceeded.", out message);
            }
            var snapshot = state.DeepCopy();
            // Count every neighbour from the complete previous state, then apply deaths together.
            var occupied = new bool[definition.grid.width, definition.grid.height];
            occupied[state.PlayerX, state.PlayerY] = true;
            foreach (var cell in state.Cells) occupied[cell.X, cell.Y] = true;
            var survivors = new List<LivingCellState>();
            foreach (var cell in state.Cells)
            {
                int neighbours = 0;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int x = cell.X + dx, y = cell.Y + dy;
                        if ((dx != 0 || dy != 0) && Inside(x, y) && occupied[x, y]) neighbours++;
                    }
                if (!(lonelyDeath && neighbours < 2) && !(crowdedDeath && neighbours > 3)) survivors.Add(cell);
            }
            state.Cells = survivors;
            state.Iteration++;
            if (history.Count == HistoryCapacity) history.RemoveAt(0);
            history.Add(snapshot);
            EvaluateGoals();
            message = "Iterated.";
            return true;
        }

        private void EvaluateGoals()
        {
            bool satisfied = definition.winConditions.conditions.Count > 0;
            foreach (var goal in definition.winConditions.conditions)
            {
                switch (goal.type)
                {
                    case LevelIds.ActorAtPosition:
                        satisfied &= state.PlayerX == goal.position.x && state.PlayerY == goal.position.y;
                        break;
                    case LevelIds.CellCount:
                        satisfied &= goal.@operator == ">" ? state.Cells.Count > goal.value.Value : state.Cells.Count < goal.value.Value;
                        break;
                    case LevelIds.CoverPositions:
                        foreach (var position in goal.positions) satisfied &= CellAt(position.x, position.y) != null;
                        break;
                    default: satisfied = false; break;
                }
            }
            Status = satisfied ? LevelPlayStatus.Won : LevelPlayStatus.Playing;
            if (satisfied) Mode = LevelInteractionMode.Move;
        }

        private bool CanOperate(out string message)
        {
            message = "";
            return Status == LevelPlayStatus.Playing || Reject("The session has ended.", out message);
        }
        private bool Inside(int x, int y) => x >= 0 && y >= 0 && x < floor.GetLength(0) && y < floor.GetLength(1);
        private bool IsFloor(int x, int y) => Inside(x, y) && floor[x, y];
        private LivingCellState CellAt(int x, int y) => state.Cells.Find(c => c.X == x && c.Y == y);
        private static bool Reject(string reason, out string message) { message = reason; return false; }
    }
}
