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
    public static class LevelHistoryChecks
    {
        private static int checks;
        private static void Check(bool success, string name)
        { if (!success) throw new Exception("FAILED: " + name); checks++; }
        private static LevelDefinition Board()
        {
            var level = new LevelDefinition { id = "history_checks", grid = new GridSize { width = 4, height = 4 }, cloneLimit = 3 };
            for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++)
                level.tiles.Add(new LevelTile { x = x, y = y, type = LevelIds.Floor });
            level.cells.Add(new LevelCell { x = 1, y = 1, type = LevelIds.Player });
            level.cells.Add(new LevelCell { x = 3, y = 3, type = LevelIds.Normal });
            level.winConditions.conditions.Add(new LevelGoal { type = LevelIds.CellCount, @operator = ">", value = 99 });
            return level;
        }
        private static string Layout(LevelPlaySession session)
        {
            var s = session.State;
            return s.PlayerX + "," + s.PlayerY + ":" + s.Iteration + ":" +
                string.Join(";", s.Cells.Select(c => c.X + "," + c.Y + "," + c.IsClone));
        }
        private static void Iterate(LevelPlaySession s)
        { if (!s.TryIterate(out var message)) throw new Exception(message); }
        private static void Clone(LevelPlaySession s, int x, int y)
        { if (!s.TryBeginClonePlacement(out var message) || !s.TryCreateClone(x, y, out message)) throw new Exception(message); }
        private static bool Frame(LevelPlayController c, int frame, params LevelPlayCommand[] commands)
        { return c.ProcessFrame(frame, commands.Select(x => new LevelPlayRequest(x)).ToArray(), out _); }

        public static int Run()
        {
            checks = 0;
            var level = Board(); var s = new LevelPlaySession(level);
            Check(!s.CanUndo && s.HistoryCount == 0, "new session has no history");
            s.TryMove(1, 0, out _); Clone(s, 2, 2); string before = Layout(s);
            Check(!s.TryUndo(out _) && Layout(s) == before && s.HistoryCount == 0, "round zero undo never reverses movement or cloning");
            s.TryBeginRecycling(out _); s.TryRecycleClone(2, 2, out _);
            Check(s.HistoryCount == 0, "recycling does not create history");
            s.TryBeginClonePlacement(out _); before = Layout(s);
            Check(!s.TryIterate(out _) && s.HistoryCount == 0 && Layout(s) == before, "blocked iteration creates no snapshot");
            s.CancelSelection(); Clone(s, 2, 2); before = Layout(s); Iterate(s);
            Check(s.HistoryCount == 1 && s.CanUndo && s.Iteration == 1, "successful iteration records one previous state");
            s.TryBeginRecycling(out _); s.TryRecycleClone(2, 2, out _); Clone(s, 3, 1); s.TryMove(-1, 0, out _);
            Check(s.HistoryCount == 1, "post-iteration actions do not add history");
            Check(s.TryUndo(out _) && Layout(s) == before && s.LivingCloneCount == 1 && s.HistoryCount == 0,
                "undo restores clone A, removes B and restores pre-iteration player position");
            Check(s.State.Cells.Count(c => c.IsClone && c.X == 2 && c.Y == 2) == 1
                && s.State.Cells.Any(c => !c.IsClone && c.X == 3 && c.Y == 3), "undo preserves clone versus initial cell provenance");
            Iterate(s); s.TryMove(-1, 0, out _); string round1 = Layout(s); Iterate(s);
            var copy = s.State; copy.Cells.Clear(); copy.PlayerX = 99;
            s.TryMove(0, -1, out _);
            Check(s.TryUndo(out _) && Layout(s) == round1 && s.HistoryCount == 1, "latest snapshot independent of returned state and later movement");
            Check(s.TryUndo(out _) && Layout(s) == before && s.Iteration == 0, "older snapshot independent of newer restore");
            Iterate(s); s.TryUndo(out _); s.TryMove(0, -1, out _); string branch = Layout(s); Iterate(s); s.TryUndo(out _);
            Check(Layout(s) == branch && !s.CanUndo, "undo then iterate creates a new branch without future history");
            s.TryBeginClonePlacement(out _); s.Reset();
            Check(s.Mode == LevelInteractionMode.Move && s.Iteration == 0 && s.HistoryCount == 0 && s.LivingCloneCount == 0
                && s.State.PlayerX == 1 && s.State.PlayerY == 1 && s.State.Cells.Single().X == 3, "reset restores initial cells, player and clean mode/history");
            level.cells.Clear(); level.winConditions.conditions.Clear(); s.State.Cells.Clear(); s.Reset();
            Check(s.State.Cells.Count == 1 && s.Status == LevelPlayStatus.Playing, "reset initial copy isolated from caller and returned copies");
            Clone(s, 0, 0); s.Reset(); Clone(s, 0, 0); s.Reset();
            Check(s.State.Cells.Count == 1 && s.LivingCloneCount == 0, "repeated reset does not mutate initial state");

            level = Board(); level.rules.Add(new LevelRule { id = LevelIds.LonelyDeath }); s = new LevelPlaySession(level); Clone(s, 0, 0); Iterate(s);
            Check(s.LivingCloneCount == 0 && s.State.Cells.Count == 0, "history fixture cells die");
            Check(s.TryUndo(out _) && s.LivingCloneCount == 1 && s.State.Cells.Count == 2, "undo restores dead clones and initial cells");
            level = Board(); level.iterationLimit = 1; s = new LevelPlaySession(level); Iterate(s); before = Layout(s); s.TryIterate(out _);
            Check(s.Status == LevelPlayStatus.Failed && Layout(s) == before && s.HistoryCount == 1, "over-limit failure creates no extra history");
            Check(s.TryUndo(out _) && s.Status == LevelPlayStatus.Playing && s.Iteration == 0 && !s.CanUndo, "undo recovers from failure");
            Iterate(s); s.TryIterate(out _); s.Reset();
            Check(s.Status == LevelPlayStatus.Playing && s.Iteration == 0 && !s.CanUndo, "reset clears failure and history");
            level = Board(); level.winConditions.conditions = new List<LevelGoal> {
                new LevelGoal { type = LevelIds.ActorAtPosition, position = new GridPosition { x = 2, y = 1 } } };
            s = new LevelPlaySession(level); Iterate(s); s.TryMove(1, 0, out _);
            Check(s.Status == LevelPlayStatus.Won && s.TryUndo(out _) && s.Status == LevelPlayStatus.Playing && s.State.PlayerX == 1, "undo re-evaluates goals after win");
            level.winConditions.conditions[0].position.x = 1; s = new LevelPlaySession(level); s.Reset();
            Check(s.Status == LevelPlayStatus.Won && !s.CanUndo, "reset re-evaluates an initially won level");

            s = new LevelPlaySession(Board());
            for (int i = 0; i < 101; i++) Iterate(s);
            Check(s.HistoryCount == LevelPlaySession.HistoryCapacity && s.HistoryCount == 100 && s.Iteration == 101, "101 iterations retain exactly latest 100 snapshots");
            int undone = 0; while (s.TryUndo(out _)) undone++;
            Check(undone == 100 && s.Iteration == 1 && !s.CanUndo, "history boundary stops at round one");
            before = Layout(s);
            Check(!s.TryUndo(out _) && Layout(s) == before, "history boundary rejection preserves board");
            s.Reset(); Check(s.Iteration == 0 && !s.CanUndo, "reset reaches initial state even after history eviction");
            for (int i = 0; i < 101; i++)
            {
                s.TryMove(i % 2 == 0 ? 1 : -1, 0, out _); Iterate(s);
            }
            for (int i = 0; i < 100; i++) s.TryUndo(out _);
            Check(s.Iteration == 1 && s.State.PlayerX == 1 && s.State.PlayerY == 1, "evicted history restores second iteration pre-position");

            s = new LevelPlaySession(Board()); var controller = new LevelPlayController(s);
            Check(Frame(controller, 0, LevelPlayCommand.Up, LevelPlayCommand.Right) && s.State.PlayerY == 2 && s.State.PlayerX == 1, "fixed direction priority up before right");
            Check(!Frame(controller, 0, LevelPlayCommand.Right) && s.State.PlayerX == 1, "second dispatch in same frame ignored");
            Check(Frame(controller, 1, LevelPlayCommand.Right, LevelPlayCommand.Iterate) && s.Iteration == 1 && s.State.PlayerX == 1, "iteration outranks movement");
            Check(Frame(controller, 2, LevelPlayCommand.Iterate, LevelPlayCommand.Undo) && s.Iteration == 0 && !s.CanUndo, "undo outranks iteration");
            Check(Frame(controller, 3, LevelPlayCommand.Right, LevelPlayCommand.Reset) && s.State.PlayerY == 1 && s.State.PlayerX == 1, "reset outranks movement");
            Check(!Frame(controller, 4, LevelPlayCommand.Undo, LevelPlayCommand.Iterate) && s.Iteration == 0, "failed chosen action never falls through to a second action");
            controller.ProcessFrame(5, new[] { new LevelPlayRequest(LevelPlayCommand.Select, 0, 0), new LevelPlayRequest(LevelPlayCommand.Clone) }, out _);
            Check(s.Mode == LevelInteractionMode.PlaceClone && s.LivingCloneCount == 0, "mode entry click cannot also create a clone");
            before = Layout(s);
            Check(!Frame(controller, 6, LevelPlayCommand.Up, LevelPlayCommand.Iterate, LevelPlayCommand.Undo, LevelPlayCommand.Reset)
                && Layout(s) == before, "selection blocks all non-selection state actions");
            controller.ProcessFrame(7, new[] { new LevelPlayRequest(LevelPlayCommand.Select, 3, 3) }, out _);
            Check(s.Mode == LevelInteractionMode.PlaceClone && s.LivingCloneCount == 0, "invalid target stays in selection");
            controller.ProcessFrame(8, new[] { new LevelPlayRequest(LevelPlayCommand.Select, 0, 0) }, out _);
            Check(s.Mode == LevelInteractionMode.Move && s.LivingCloneCount == 1, "valid selection creates exactly one clone");
            Frame(controller, 9, LevelPlayCommand.Recycle);
            controller.ProcessFrame(10, new[] { new LevelPlayRequest(LevelPlayCommand.Select, 0, 0), new LevelPlayRequest(LevelPlayCommand.Cancel) }, out _);
            Check(s.LivingCloneCount == 1 && s.Mode == LevelInteractionMode.Move, "cancel outranks selecting a clone");
            Frame(controller, 11, LevelPlayCommand.Clone); Frame(controller, 12, LevelPlayCommand.Pause, LevelPlayCommand.Select);
            Check(controller.Paused && s.Mode == LevelInteractionMode.Move && s.LivingCloneCount == 1, "pause cancels unfinished placement");
            before = Layout(s);
            Check(!Frame(controller, 13, LevelPlayCommand.Up, LevelPlayCommand.Iterate, LevelPlayCommand.Reset, LevelPlayCommand.Undo)
                && Layout(s) == before, "paused controller blocks gameplay and history actions");
            Check(Frame(controller, 14, LevelPlayCommand.Cancel, LevelPlayCommand.Right) && !controller.Paused && Layout(s) == before, "resume does not execute movement in same frame");
            Frame(controller, 15, LevelPlayCommand.Iterate); Frame(controller, 16, LevelPlayCommand.Recycle);
            Frame(controller, 17, LevelPlayCommand.Cancel); Frame(controller, 18, LevelPlayCommand.Undo);
            Check(s.LivingCloneCount == 1 && s.Mode == LevelInteractionMode.Move && !s.CanUndo, "controller routes undo through same session");
            Frame(controller, 19, LevelPlayCommand.Reset);
            Check(s.LivingCloneCount == 0 && s.Iteration == 0 && !s.CanUndo, "controller reset uses same session initial state");
            level = Board(); level.iterationLimit = 1; s = new LevelPlaySession(level); controller = new LevelPlayController(s);
            Frame(controller, 0, LevelPlayCommand.Iterate); Frame(controller, 1, LevelPlayCommand.Iterate); before = Layout(s);
            Check(!Frame(controller, 2, LevelPlayCommand.Up, LevelPlayCommand.Clone) && Layout(s) == before, "failure locks normal controller inputs");
            Check(Frame(controller, 3, LevelPlayCommand.Undo) && s.Status == LevelPlayStatus.Playing, "failure accepts undo through controller");
            return checks;
        }
#if UNITY_EDITOR
        [MenuItem("Tools/Levels/Run History Checks")]
        public static void RunInEditor() { Debug.Log("Level history checks passed: " + Run()); }
#endif
    }
}
