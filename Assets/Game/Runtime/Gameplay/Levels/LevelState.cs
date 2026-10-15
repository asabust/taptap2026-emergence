using System;
using System.Collections.Generic;
using Game.Runtime.Data.Levels;

namespace Game.Runtime.Gameplay.Levels
{
    public sealed class LivingCellState
    {
        public int X;
        public int Y;
        public bool IsClone;
    }

    /// <summary>Mutable play data; never shares objects with editor draft/initial definition.</summary>
    public sealed class LevelState
    {
        public int PlayerX;
        public int PlayerY;
        public int Iteration;
        public List<LivingCellState> Cells = new List<LivingCellState>();
        public int LivingCloneCount
        {
            get
            {
                int count = 0;
                foreach (var cell in Cells) if (cell.IsClone) count++;
                return count;
            }
        }

        public static LevelState FromDefinition(LevelDefinition definition)
        {
            var validation = LevelValidator.ValidateForPlay(definition);
            if (!validation.CanPlay) throw new ArgumentException(string.Join("\n", validation.Errors));
            var state = new LevelState();
            foreach (var cell in definition.cells)
            {
                if (cell.type == LevelIds.Player) { state.PlayerX = cell.x; state.PlayerY = cell.y; }
                else state.Cells.Add(new LivingCellState { X = cell.x, Y = cell.y, IsClone = false });
            }
            return state;
        }
        public LevelState DeepCopy()
        {
            var copy = new LevelState { PlayerX = PlayerX, PlayerY = PlayerY, Iteration = Iteration };
            foreach (var cell in Cells)
                copy.Cells.Add(new LivingCellState { X = cell.X, Y = cell.Y, IsClone = cell.IsClone });
            return copy;
        }
    }
}
