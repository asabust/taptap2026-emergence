using System;
using System.Linq;
using Game.Runtime.Data.Levels;
using Game.Runtime.Gameplay.LevelEditor;
using Game.Runtime.Gameplay.Levels;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace Game.Editor
{
    public static class LevelEditorChecks
    {
        private static int checks;
        private static void Require(bool value, string label)
        { if (!value) throw new Exception("FAILED: " + label); checks++; }
        private static void Paint(LevelEditorSession s, int x, int y, LevelBrush brush)
        { if (!s.TryPaint(x, y, brush, out var message)) throw new Exception(message); }

        public static int Run()
        {
            checks = 0;
            var s = new LevelEditorSession(5, 5);
            Require(s.Draft.tiles.Count == 25 && s.Draft.cells.Count == 0, "new all floor");
            int changes = 0; s.Changed += () => changes++;
            Require(!s.TryPaint(-1, 0, LevelBrush.Normal, out _ ) && changes == 0, "invalid click no mutation");
            Paint(s, 0, 0, LevelBrush.Player); Paint(s, 2, 2, LevelBrush.Player);
            Require(s.Draft.cells.Count == 1 && s.CellAt(2, 2) == LevelIds.Player, "unique player relocation");
            Require(!s.TryPaint(2, 2, LevelBrush.Normal, out _) && s.CellAt(2, 2) == LevelIds.Player, "normal cannot replace player");
            Paint(s, 1, 1, LevelBrush.Normal);
            Require(!s.TryPaint(1, 1, LevelBrush.Player, out _) && s.CellAt(2, 2) == LevelIds.Player, "player cannot overwrite normal");
            Paint(s, 2, 2, LevelBrush.ActorGoal); Paint(s, 2, 2, LevelBrush.CellGoal);
            Paint(s, 2, 2, LevelBrush.CellGoal);
            Require(s.Draft.winConditions.conditions[1].positions.Count == 1, "no duplicate targets");
            Paint(s, 2, 2, LevelBrush.EraseCell);
            Require(s.CellAt(2, 2) == null && s.IsActorGoal(2, 2) && s.TerrainAt(2, 2) == LevelIds.Floor, "erase object preserves goals/floor");
            Paint(s, 2, 2, LevelBrush.Normal); Paint(s, 2, 2, LevelBrush.Wall);
            Require(s.CellAt(2, 2) == null && !s.IsActorGoal(2, 2) && !s.IsCellGoal(2, 2), "wall clears contents");
            Require(!s.TryPaint(2, 2, LevelBrush.Normal, out _) && !s.TryPaint(2, 2, LevelBrush.CellGoal, out _), "nonfloor rejects contents");
            Paint(s, 2, 2, LevelBrush.EraseTerrain);
            Require(s.TerrainAt(2, 2) == null, "internal hole");
            Paint(s, 2, 2, LevelBrush.Floor); Paint(s, 2, 2, LevelBrush.Player); Paint(s, 2, 2, LevelBrush.CellGoal);
            Paint(s, 2, 2, LevelBrush.EraseGoals);
            Require(s.CellAt(2, 2) == LevelIds.Player && s.TerrainAt(2, 2) == LevelIds.Floor && !s.IsCellGoal(2, 2), "goal eraser isolated");
            s.New(5, 5); Paint(s, 2, 2, LevelBrush.Player); Paint(s, 1, 1, LevelBrush.Normal); Paint(s, 1, 1, LevelBrush.CellGoal);
            s.Resize(6, 6);
            Require(s.CellAt(2, 2) == LevelIds.Player && s.IsCellGoal(1, 1), "5 to 6 no offset");
            Require(s.TerrainAt(5, 5) == null && s.Draft.tiles.Count == 25, "expanded area invalid");
            s.Resize(5, 5);
            Require(s.CellAt(1, 1) == LevelIds.Player && s.CellAt(0, 0) == LevelIds.Normal && s.IsCellGoal(0, 0), "6 to 5 floor negative offset");
            s.New(6, 6); Paint(s, 0, 0, LevelBrush.Player); Paint(s, 0, 0, LevelBrush.ActorGoal); Paint(s, 1, 1, LevelBrush.Normal); Paint(s, 1, 1, LevelBrush.CellGoal);
            Paint(s, 5, 5, LevelBrush.Normal); Paint(s, 5, 5, LevelBrush.CellGoal);
            s.Resize(4, 4);
            Require(!s.Draft.cells.Any(c => c.type == LevelIds.Player), "cropped player removed");
            Require(s.Draft.winConditions.conditions[0].position == null, "cropped actor goal invalidated");
            Require(s.Draft.cells.Count == 1 && s.CellAt(0, 0) == LevelIds.Normal && s.IsCellGoal(0, 0), "6 to 4 retained content aligned");
            Require(s.Draft.winConditions.conditions[1].positions.Count == 1, "cropped cell targets removed");
            Require(!LevelValidator.ValidateForPlay(s.Draft).CanPlay, "cropped player prevents play");
            s.Resize(6, 6);
            Require(s.CellAt(0, 0) == null && s.CellAt(5, 5) == null && s.Draft.cells.Count == 1, "cropped data never restored");
            s.New(6, 4); Paint(s, 3, 2, LevelBrush.Player); s.Resize(4, 6);
            Require(s.CellAt(2, 3) == LevelIds.Player, "mixed shrink/expand axes");
            string before = LevelJson.Serialize(s.Draft);
            try { s.Resize(31, 5); throw new Exception("accepted 31"); }
            catch (ArgumentOutOfRangeException) { Require(LevelJson.Serialize(s.Draft) == before, "invalid resize preserves draft"); }
            var source = LevelJson.DeepCopy(s.Draft); s.ReplaceDraft(source); source.cells.Clear();
            Require(s.Draft.cells.Count == 1, "replacement draft independent");
            before = LevelJson.Serialize(s.Draft);
            try { s.ReplaceDraft(new LevelDefinition { grid = null }); throw new Exception("accepted null grid"); }
            catch (ArgumentException) { Require(LevelJson.Serialize(s.Draft) == before, "failed replacement preserves draft"); }
            s.New(1, 1); Require(s.Draft.tiles.Count == 1 && s.Draft.cells.Count == 0 && s.Draft.winConditions.conditions.Count == 0, "new clears old draft");
            s.New(30, 30); Require(s.Draft.tiles.Count == 900, "maximum board");
            int x, y;
            Require(LevelEditorSession.TryPickNormalized(0, 0, 5, 5, out x, out y) && x == 0 && y == 0, "pick origin");
            Require(LevelEditorSession.TryPickNormalized(.999f, .999f, 30, 30, out x, out y) && x == 29 && y == 29, "pick far cell");
            Require(LevelEditorSession.TryPickNormalized(.4f, .6f, 5, 5, out x, out y) && x == 2 && y == 3, "pick internal boundary");
            Require(!LevelEditorSession.TryPickNormalized(1, .5f, 5, 5, out x, out y), "right boundary excluded");
            Require(!LevelEditorSession.TryPickNormalized(.5f, 1, 5, 5, out x, out y), "top boundary excluded");
            Require(!LevelEditorSession.TryPickNormalized(-.01f, 0, 5, 5, out x, out y), "outside excluded");
            Require(!LevelEditorSession.TryPickNormalized(float.NaN, 0, 5, 5, out x, out y), "NaN excluded");
            return checks;
        }
#if UNITY_EDITOR
        [MenuItem("Tools/Levels/Run Editor Checks")]
        public static void RunInEditor() { Debug.Log("Level editor checks passed: " + Run()); }
#endif
    }
}
