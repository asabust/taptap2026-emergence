using System;
using System.Collections.Generic;
using System.Linq;
using Game.Runtime.Data.Levels;
using Game.Runtime.Gameplay.Levels;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace Game.Editor
{
    public static class LevelPlayChecks
    {
        private static int checks;
        private static void Check(bool success, string name)
        { if (!success) throw new Exception("FAILED: " + name); checks++; }

        // A deliberately unmet goal keeps rule fixtures playing without accidental early wins.
        private static LevelDefinition Board(int px = 0, int py = 0, params int[] cells)
        {
            var level = new LevelDefinition
            {
                id = "play_checks", grid = new GridSize { width = 5, height = 5 }, cloneLimit = 3,
                rules = new List<LevelRule> { new LevelRule { id = LevelIds.LonelyDeath }, new LevelRule { id = LevelIds.CrowdedDeath } }
            };
            for (int x = 0; x < 5; x++) for (int y = 0; y < 5; y++)
                level.tiles.Add(new LevelTile { x = x, y = y, type = LevelIds.Floor });
            level.cells.Add(new LevelCell { x = px, y = py, type = LevelIds.Player });
            for (int i = 0; i < cells.Length; i += 2)
                level.cells.Add(new LevelCell { x = cells[i], y = cells[i + 1], type = LevelIds.Normal });
            level.winConditions.conditions.Add(Count(">", 99));
            return level;
        }
        private static LevelGoal Count(string op, int n) => new LevelGoal { type = LevelIds.CellCount, @operator = op, value = n };
        private static LevelGoal Actor(int x, int y) => new LevelGoal { type = LevelIds.ActorAtPosition, position = new GridPosition { x = x, y = y } };
        private static LevelGoal Cover(params int[] positions)
        {
            var goal = new LevelGoal { type = LevelIds.CoverPositions, positions = new List<GridPosition>() };
            for (int i = 0; i < positions.Length; i += 2) goal.positions.Add(new GridPosition { x = positions[i], y = positions[i + 1] });
            return goal;
        }
        private static bool Has(LevelPlaySession s, int x, int y) => s.State.Cells.Any(c => c.X == x && c.Y == y);
        private static string Snapshot(LevelPlaySession s)
        {
            var state = s.State;
            return state.PlayerX + "," + state.PlayerY + ":" + state.Iteration + ":" +
                string.Join(";", state.Cells.Select(c => c.X + "," + c.Y + "," + c.IsClone));
        }
        private static void Clone(LevelPlaySession s, int x, int y)
        {
            if (!s.TryBeginClonePlacement(out var message) || !s.TryCreateClone(x, y, out message)) throw new Exception(message);
        }
        private static void Iterate(LevelPlaySession s)
        { if (!s.TryIterate(out var message)) throw new Exception(message); }

        public static int Run()
        {
            checks = 0;
            var level = Board(0, 0, 1, 0);
            level.tiles.Find(t => t.x == 0 && t.y == 1).type = LevelIds.Wall;
            level.tiles.RemoveAll(t => t.x == 1 && t.y == 1);
            var s = new LevelPlaySession(level);
            string before = Snapshot(s);
            Check(!s.TryMove(1, 0, out _) && Snapshot(s) == before, "living cell blocks movement");
            Check(!s.TryMove(0, 1, out _) && Snapshot(s) == before, "wall blocks movement");
            Check(!s.TryMove(-1, 0, out _) && Snapshot(s) == before, "boundary does not wrap");
            Check(!s.TryMove(1, 1, out _) && !s.TryMove(0, 0, out _) && !s.TryMove(2, 0, out _)
                && !s.TryMove(int.MaxValue, 0, out _) && Snapshot(s) == before, "only one orthogonal step accepted");
            s = new LevelPlaySession(Board());
            Check(s.TryMove(1, 0, out _) && s.TryMove(0, 1, out _) && s.TryMove(-1, 0, out _)
                && s.TryMove(0, -1, out _) && s.Iteration == 0 && s.State.Cells.Count == 0, "four directions, no evolution or player trail");
            level = Board(); level.tiles.RemoveAll(t => t.x == 1 && t.y == 0); s = new LevelPlaySession(level);
            Check(!s.TryMove(1, 0, out _), "internal void blocks movement");

            s = new LevelPlaySession(Board(4, 4, 1, 1, 2, 1)); Iterate(s);
            Check(s.State.Cells.Count == 0, "one neighbour dies with loneliness enabled");
            s = new LevelPlaySession(Board(1, 2, 1, 1, 2, 1)); Iterate(s);
            Check(Has(s, 1, 1) && Has(s, 2, 1) && s.State.PlayerX == 1 && s.State.PlayerY == 2, "player supplies second neighbour and survives");
            s = new LevelPlaySession(Board(4, 4, 2, 2, 1, 2, 2, 1, 3, 2)); Iterate(s);
            Check(Has(s, 2, 2), "three neighbours survive");
            s = new LevelPlaySession(Board(2, 3, 2, 2, 1, 2, 2, 1, 3, 2)); Iterate(s);
            Check(!Has(s, 2, 2), "player supplies fourth neighbour causing crowded death");
            level = Board(4, 4, 2, 2, 1, 2, 2, 1, 3, 2); level.rules.RemoveAll(r => r.id == LevelIds.LonelyDeath);
            s = new LevelPlaySession(level); Iterate(s);
            Check(Has(s, 1, 2) && Has(s, 3, 2) && Has(s, 2, 2), "loneliness switch off preserves sparse cells");
            level = Board(4, 4, 0, 0); level.rules.RemoveAll(r => r.id == LevelIds.LonelyDeath);
            s = new LevelPlaySession(level); Iterate(s);
            Check(Has(s, 0, 0), "disabled loneliness preserves a zero-neighbour cell");
            level = Board(2, 3, 2, 2, 1, 2, 2, 1, 3, 2); level.rules.RemoveAll(r => r.id == LevelIds.CrowdedDeath);
            s = new LevelPlaySession(level); Iterate(s);
            Check(Has(s, 2, 2), "crowding switch off preserves crowded cells");
            level = Board(0, 0, 2, 2, 1, 2, 2, 1, 3, 2, 4, 4); level.rules.Clear();
            s = new LevelPlaySession(level); before = Snapshot(s); Iterate(s);
            Check(s.State.Cells.Count == 5 && s.Iteration == 1 && s.State.PlayerX == 0, "both rules off, iteration still advances");
            s = new LevelPlaySession(Board(4, 4, 1, 2, 2, 1, 3, 2)); Iterate(s);
            Check(!Has(s, 2, 2), "three neighbours never produce birth");
            s = new LevelPlaySession(Board(4, 4, 1, 2, 2, 2, 3, 2)); Iterate(s);
            Check(s.State.Cells.Count == 1 && Has(s, 2, 2), "synchronous deaths preserve centre from previous-state neighbours");
            s = new LevelPlaySession(Board(4, 4, 0, 0, 4, 0, 0, 4)); Iterate(s);
            Check(s.State.Cells.Count == 0, "opposite edges do not contribute neighbours");
            s = new LevelPlaySession(Board()); Iterate(s);
            Check(s.State.PlayerX == 0 && s.State.PlayerY == 0 && s.State.Cells.Count == 0, "isolated player never dies");
            s = new LevelPlaySession(Board(2, 2, 1, 1, 1, 2, 1, 3, 2, 1, 2, 3, 3, 1, 3, 2, 3, 3)); Iterate(s);
            Check(s.State.PlayerX == 2 && s.State.PlayerY == 2, "crowded player never dies");

            level = Board(2, 2, 1, 2); level.tiles.Find(t => t.x == 2 && t.y == 3).type = LevelIds.Wall;
            level.tiles.RemoveAll(t => t.x == 3 && t.y == 2); s = new LevelPlaySession(level);
            before = Snapshot(s);
            Check(!s.TryCreateClone(1, 1, out _) && Snapshot(s) == before, "clone requires placement mode");
            Check(s.TryBeginClonePlacement(out _) && s.Mode == LevelInteractionMode.PlaceClone, "begin clone placement");
            Check(!s.TryMove(0, -1, out _) && !s.TryIterate(out _) && Snapshot(s) == before, "placement pauses movement and iteration");
            Check(!s.TryCreateClone(2, 2, out _) && !s.TryCreateClone(1, 2, out _) && !s.TryCreateClone(2, 3, out _)
                && !s.TryCreateClone(3, 2, out _) && !s.TryCreateClone(4, 4, out _) && !s.TryCreateClone(-1, 2, out _)
                && Snapshot(s) == before && s.Mode == LevelInteractionMode.PlaceClone, "invalid placement preserves data and selection mode");
            Check(s.CanPlaceCloneAt(1, 1) && s.TryCreateClone(1, 1, out _) && s.Mode == LevelInteractionMode.Move
                && s.LivingCloneCount == 1 && s.Iteration == 0, "diagonal clone, source tag, no iteration, return to move");
            Check(!s.TryMove(-1, -1, out _) && !s.TryMove(-1, 0, out _), "normal cells and invalid directions remain blocked");
            s.TryBeginClonePlacement(out _); s.CancelSelection();
            Check(s.Mode == LevelInteractionMode.Move && s.LivingCloneCount == 1, "cancel selection without changing cells");
            level = Board(2, 2); level.cloneLimit = 0; s = new LevelPlaySession(level);
            Check(!s.TryBeginClonePlacement(out _) && !s.CanPlaceCloneAt(1, 1), "zero clone limit disables placement");
            level = Board(2, 2); level.cloneLimit = 1; s = new LevelPlaySession(level); Clone(s, 1, 1);
            before = Snapshot(s);
            Check(!s.TryBeginClonePlacement(out _) && Snapshot(s) == before && s.LivingCloneCount == 1, "full quota rejects without auto recycling");
            Check(s.TryMove(-1, 0, out _) && !s.TryMove(0, -1, out _), "clones block orthogonal movement");
            s = new LevelPlaySession(Board(0, 0, 1, 0, 0, 1, 1, 1));
            Check(!s.TryBeginClonePlacement(out _) && s.LivingCloneCount == 0, "no legal neighbour reported");
            Check(!s.TryBeginRecycling(out _), "no clones available for recycling");

            level = Board(2, 2, 4, 4); level.rules.Clear(); s = new LevelPlaySession(level); Clone(s, 1, 1);
            Check(!s.TryRecycleClone(1, 1, out _), "recycling requires selection mode");
            s.TryMove(1, 0, out _); s.TryMove(0, 1, out _);
            Check(s.TryBeginRecycling(out _) && !s.TryMove(0, 1, out _) && !s.TryIterate(out _), "recycling pauses movement and iteration");
            before = Snapshot(s);
            Check(!s.TryRecycleClone(4, 4, out _) && !s.TryRecycleClone(0, 0, out _) && !s.TryRecycleClone(3, 3, out _)
                && Snapshot(s) == before && s.Mode == LevelInteractionMode.RecycleClone, "cannot recycle initial cells, empty floor or player");
            Check(s.TryRecycleClone(1, 1, out _) && s.LivingCloneCount == 0 && Has(s, 4, 4)
                && s.Mode == LevelInteractionMode.Move && s.Iteration == 0, "global recycling releases quota and keeps initial cells");
            Clone(s, 4, 3); Check(s.LivingCloneCount == 1, "recycled quota reusable");
            level = Board(2, 2); level.cloneLimit = 1; s = new LevelPlaySession(level); Clone(s, 1, 1); Iterate(s);
            Check(s.LivingCloneCount == 0 && s.TryBeginClonePlacement(out _), "dead clone releases quota");
            level = Board(2, 2); level.rules.Clear(); s = new LevelPlaySession(level); Clone(s, 1, 1); Iterate(s);
            Check(s.LivingCloneCount == 1 && s.State.Cells.Single().IsClone, "surviving clones keep their source tag");

            level = Board(); level.winConditions.conditions = new List<LevelGoal> { Actor(1, 0) }; s = new LevelPlaySession(level);
            Check(s.TryMove(1, 0, out _) && s.Status == LevelPlayStatus.Won, "movement checks actor goal");
            before = Snapshot(s);
            Check(!s.TryMove(0, 1, out _) && !s.TryIterate(out _) && !s.TryBeginClonePlacement(out _)
                && !s.TryBeginRecycling(out _) && !s.TryCreateClone(2, 0, out _) && !s.TryRecycleClone(2, 0, out _)
                && Snapshot(s) == before && s.Status == LevelPlayStatus.Won, "won session blocks all board operations");
            level = Board(); level.winConditions.conditions = new List<LevelGoal> { Actor(0, 0) }; s = new LevelPlaySession(level);
            Check(s.Status == LevelPlayStatus.Won, "initial goals evaluated");
            level = Board(); level.winConditions.conditions = new List<LevelGoal> { Count(">", 0) }; s = new LevelPlaySession(level);
            Check(s.Status == LevelPlayStatus.Playing, "player excluded from count, equality is not greater");
            Clone(s, 1, 1); Check(s.Status == LevelPlayStatus.Won, "clone counts and creation checks goals");
            level = Board(0, 0, 2, 2); level.winConditions.conditions = new List<LevelGoal> { Count("<", 1) }; s = new LevelPlaySession(level);
            Check(s.Status == LevelPlayStatus.Playing, "equality is not less");
            Iterate(s); Check(s.Status == LevelPlayStatus.Won, "death iteration checks count goal");
            level = Board(0, 0, 3, 3); level.winConditions.conditions = new List<LevelGoal> { Cover(3, 3, 1, 1) }; s = new LevelPlaySession(level);
            Check(s.Status == LevelPlayStatus.Playing, "coverage requires all positions simultaneously");
            Clone(s, 1, 1); Check(s.Status == LevelPlayStatus.Won, "coverage includes initial cells and clones");
            level = Board(); level.winConditions.conditions = new List<LevelGoal> { Cover(0, 0) }; s = new LevelPlaySession(level);
            Check(s.Status == LevelPlayStatus.Playing, "player cannot cover a cell goal");
            level = Board(0, 0, 1, 0); level.winConditions.conditions = new List<LevelGoal> { Actor(1, 0) }; s = new LevelPlaySession(level);
            Check(s.Status == LevelPlayStatus.Playing, "normal cell cannot satisfy actor goal");
            level = Board(); level.winConditions.conditions = new List<LevelGoal> { Actor(1, 0), Cover(0, 1) }; s = new LevelPlaySession(level);
            s.TryMove(1, 0, out _); s.TryMove(0, 1, out _); Clone(s, 0, 1);
            Check(s.Status == LevelPlayStatus.Playing, "previous actor satisfaction is not accumulated");
            Check(s.TryMove(0, -1, out _) && s.Status == LevelPlayStatus.Won, "combination wins only when both currently true");
            level = Board(); level.winConditions.conditions = new List<LevelGoal> { Actor(1, 0), Count("<", 1) }; s = new LevelPlaySession(level);
            Clone(s, 0, 1); s.TryMove(1, 0, out _); s.TryBeginRecycling(out _);
            Check(s.TryRecycleClone(0, 1, out _) && s.Status == LevelPlayStatus.Won, "recycling checks goals immediately");

            level = Board(); level.rules.Clear(); level.iterationLimit = 1; s = new LevelPlaySession(level); Iterate(s);
            Check(s.Iteration == 1 && s.Status == LevelPlayStatus.Playing, "reaching iteration limit is not failure");
            Check(s.TryMove(1, 0, out _), "movement allowed after final iteration");
            Clone(s, 1, 1); s.TryBeginRecycling(out _);
            Check(s.TryRecycleClone(1, 1, out _) && s.LivingCloneCount == 0, "clone creation and recycling allowed after final iteration");
            before = Snapshot(s);
            Check(!s.TryIterate(out var reason) && !string.IsNullOrEmpty(reason) && Snapshot(s) == before
                && s.Status == LevelPlayStatus.Failed && s.Iteration == 1, "over-limit attempt fails without board or iteration mutation");
            Check(!s.TryMove(1, 0, out _) && !s.TryBeginClonePlacement(out _) && !s.TryBeginRecycling(out _)
                && !s.TryCreateClone(2, 0, out _) && !s.TryRecycleClone(2, 0, out _) && !s.TryIterate(out _)
                && Snapshot(s) == before && s.Status == LevelPlayStatus.Failed, "failed session blocks all board operations");
            level = Board(); level.iterationLimit = 1;
            level.winConditions.conditions = new List<LevelGoal> { Actor(1, 0) }; s = new LevelPlaySession(level); Iterate(s);
            Check(s.TryMove(1, 0, out _) && s.Status == LevelPlayStatus.Won, "can win by movement after final iteration");
            level = Board(0, 0, 2, 2); level.iterationLimit = 1;
            level.winConditions.conditions = new List<LevelGoal> { Count("<", 1) }; s = new LevelPlaySession(level); Iterate(s);
            Check(s.Status == LevelPlayStatus.Won && s.Iteration == 1, "winning final iteration is a win rather than failure");

            level = Board(); s = new LevelPlaySession(level); before = Snapshot(s);
            level.cells.Clear(); level.rules.Clear(); level.cloneLimit = 0; level.winConditions.conditions.Clear();
            var exposed = s.State; exposed.PlayerX = 99; exposed.Cells.Add(new LivingCellState { X = 99 });
            var exposedDefinition = s.Definition; exposedDefinition.cloneLimit = 0; exposedDefinition.tiles.Clear();
            Check(Snapshot(s) == before && s.CloneLimit == 3 && s.TryMove(1, 0, out _), "draft and returned copies cannot mutate live session");
            var second = new LevelPlaySession(Board()); Clone(s, 2, 0);
            Check(second.LivingCloneCount == 0 && second.State.PlayerX == 0, "sessions have independent states");
            level = Board(); level.winConditions.conditions.Clear(); bool rejected = false;
            try { new LevelPlaySession(level); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "empty goals cannot enter play or win as an empty set");
            rejected = false;
            try { new LevelPlaySession(null); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "null level rejected");
            return checks;
        }
#if UNITY_EDITOR
        [MenuItem("Tools/Levels/Run Play Checks")]
        public static void RunInEditor() { Debug.Log("Level play checks passed: " + Run()); }
#endif
    }
}
