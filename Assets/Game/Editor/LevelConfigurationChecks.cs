using System;
using System.IO;
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
    public static class LevelConfigurationChecks
    {
        private static int checks;
        private static void Check(bool success, string name)
        { if (!success) throw new Exception("FAILED: " + name); checks++; }
        private static void Paint(LevelEditorSession s, int x, int y, LevelBrush brush)
        { if (!s.TryPaint(x, y, brush, out var message)) throw new Exception(message); }
        public static int Run()
        {
            checks = 0;
            var session = new LevelEditorSession(4, 4);
            Check(session.TryConfigure(false, false, "", "", out _) && session.Draft.cloneLimit == 0
                && session.Draft.iterationLimit == 1000 && session.Draft.rules.Count == 0, "empty defaults, both rules off");
            string before = LevelJson.Serialize(session.Draft);
            Check(!session.TryConfigure(true, true, "-1", "10", out _) && LevelJson.Serialize(session.Draft) == before, "negative clone rejects without mutation");
            Check(!session.TryConfigure(true, true, "1", "0", out _) && LevelJson.Serialize(session.Draft) == before, "zero iteration rejects atomically");
            Check(!session.TryConfigure(true, true, "x", "10", out _), "nonnumeric clone");
            Check(!session.TryConfigure(true, true, "1", "2147483648", out _), "overflow limit");
            Check(session.TryConfigure(true, false, "3", "5", out _) && session.Draft.rules.Single().id == LevelIds.LonelyDeath, "one rule, quotas");
            int first = session.AddGoal(LevelIds.CoverPositions); Paint(session, 0, 0, LevelBrush.CellGoal);
            int second = session.AddGoal(LevelIds.CoverPositions); Paint(session, 1, 1, LevelBrush.CellGoal); Paint(session, 1, 1, LevelBrush.CellGoal);
            Check(session.Draft.winConditions.conditions[first].positions.Single().x == 0
                && session.Draft.winConditions.conditions[second].positions.Count == 1, "selected target editing, deduplication");
            session.SelectGoal(first); Paint(session, 1, 1, LevelBrush.CellGoal);
            Paint(session, 1, 1, LevelBrush.EraseSelectedGoal);
            Check(session.Draft.winConditions.conditions[first].positions.Count == 1
                && session.Draft.winConditions.conditions[second].positions.Count == 1, "selected eraser preserves other target");
            session.ClearSelectedGoalPositions(); Check(session.Draft.winConditions.conditions[first].positions.Count == 0, "clear selected positions");
            int actor = session.AddGoal(LevelIds.ActorAtPosition); Paint(session, 2, 2, LevelBrush.ActorGoal);
            int actor2 = session.AddGoal(LevelIds.ActorAtPosition); Paint(session, 3, 3, LevelBrush.ActorGoal);
            Check(session.Draft.winConditions.conditions[actor].position.x == 2 && session.Draft.winConditions.conditions[actor2].position.x == 3, "selected actor goal");
            int count = session.AddGoal(LevelIds.CellCount);
            Check(session.TrySetCountGoal("<", "", out _) && !session.Draft.winConditions.conditions[count].value.HasValue, "unfinished count draft");
            Check(session.TrySetCountGoal("<", "3", out _) && session.Draft.winConditions.conditions[count].value == 3, "set count");
            before = LevelJson.Serialize(session.Draft);
            Check(!session.TrySetCountGoal("=", "3", out _) && LevelJson.Serialize(session.Draft) == before, "bad comparator no mutation");
            Check(!session.TrySetCountGoal(">", "-1", out _) && LevelJson.Serialize(session.Draft) == before, "negative threshold no mutation");
            session.RemoveSelectedGoal(); Check(session.SelectedGoalIndex == count - 1, "remove selection advances safely");
            session.New(4, 4); Check(session.SelectedGoalIndex == -1, "new resets selection");
            session.AddGoal(LevelIds.ActorAtPosition); Paint(session, 3, 3, LevelBrush.ActorGoal); Paint(session, 0, 0, LevelBrush.Player);
            Check(LevelValidator.ValidateForPlay(session.Draft).CanPlay, "complete editor draft valid");
            string directory = Path.Combine(Path.GetTempPath(), "level-config-checks-" + Guid.NewGuid().ToString("N"));
            try
            {
                var repo = new LevelRepository(directory);
                Check(repo.ListIds().Length == 0 && !Directory.Exists(directory), "list missing directory read only");
                using (var doc = new LevelEditorDocument(session, repo))
                {
                    string path = doc.Save("level_a", "第一关", false);
                    Check(File.Exists(path) && doc.SavedId == "level_a" && !doc.IsDirty, "save new draft");
                    Paint(session, 1, 1, LevelBrush.Normal); Check(doc.IsDirty, "paint marks dirty");
                    doc.Save("level_a", "修改名称", false);
                    Check(repo.Load("level_a").name == "修改名称" && !doc.IsDirty, "overwrite same ID");
                    string original = File.ReadAllText(path);
                    doc.Save("level_b", "另存的关卡", true);
                    Check(File.ReadAllText(path) == original && doc.SavedId == "level_b", "save as preserves source");
                    Check(repo.ListIds().SequenceEqual(new[] { "level_a", "level_b" }), "sorted file list");
                    bool rejected = false;
                    try { doc.Save("level_b", "x", true); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "save as requires new ID");
                    rejected = false;
                    try { doc.Save("level_a", "x", true); } catch (IOException) { rejected = true; }
                    Check(rejected && File.ReadAllText(path) == original, "save as cannot overwrite existing file");
                    rejected = false;
                    try { doc.Save("changed", "x", false); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && !File.Exists(repo.PathFor("changed")), "rename requires save as");
                    doc.Load("level_a");
                    Check(doc.SavedId == "level_a" && !doc.IsDirty && session.SelectedGoalIndex == 0, "load selects first goal and clears dirty");
                    before = LevelJson.Serialize(session.Draft);
                    File.WriteAllText(repo.PathFor("broken"), "{bad");
                    rejected = false;
                    try { doc.Load("broken"); } catch (FormatException) { rejected = true; }
                    Check(rejected && LevelJson.Serialize(session.Draft) == before && doc.SavedId == "level_a" && !doc.IsDirty, "failed load preserves draft and metadata");
                    File.WriteAllText(repo.PathFor("invalid_grid"), "{\"schemaVersion\":1,\"id\":\"invalid_grid\",\"grid\":{\"width\":31,\"height\":2}}");
                    rejected = false;
                    try { doc.Load("invalid_grid"); } catch (ArgumentException) { rejected = true; }
                    Check(rejected && LevelJson.Serialize(session.Draft) == before, "uneditable grid rejected without replacing draft");
                    File.WriteAllText(repo.PathFor("null_rules"), "{\"schemaVersion\":1,\"id\":\"null_rules\",\"grid\":{\"width\":2,\"height\":2},\"rules\":null}");
                    rejected = false;
                    try { doc.Load("null_rules"); } catch (ArgumentException) { rejected = true; }
                    Check(rejected && LevelJson.Serialize(session.Draft) == before && doc.SavedId == "level_a", "null rules cannot replace editor draft");
                    doc.New(2, 2); Check(doc.SavedId == null && doc.IsDirty, "new clears saved identity");
                    doc.Save("incomplete", "草稿", false); doc.Load("incomplete");
                    Check(session.Draft.cells.Count == 0 && !LevelValidator.ValidateForPlay(session.Draft).CanPlay, "incomplete draft persists");
                    before = LevelJson.Serialize(session.Draft);
                    rejected = false;
                    try { doc.Save("../escape", "x", true); } catch (ArgumentException) { rejected = true; }
                    Check(rejected && LevelJson.Serialize(session.Draft) == before && doc.SavedId == "incomplete", "unsafe ID cannot alter state");
                    // A path which is a file rather than a directory exercises a real I/O failure.
                    string blocked = Path.Combine(directory, "blocked"); File.WriteAllText(blocked, "keep");
                    using (var failing = new LevelEditorDocument(session, new LevelRepository(blocked)))
                    {
                        rejected = false;
                        try { failing.Save("save_fail", "x", false); } catch (IOException) { rejected = true; }
                        Check(rejected && LevelJson.Serialize(session.Draft) == before && failing.SavedId == null && failing.IsDirty
                            && File.ReadAllText(blocked) == "keep", "failed save preserves draft and original file");
                    }
                }
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            return checks;
        }
#if UNITY_EDITOR
        [MenuItem("Tools/Levels/Run Configuration Checks")]
        public static void RunInEditor() { Debug.Log("Level configuration checks passed: " + Run()); }
#endif
    }
}
