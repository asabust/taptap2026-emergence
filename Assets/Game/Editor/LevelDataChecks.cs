using System;
using System.IO;
using System.Linq;
using Game.Runtime.Data.Levels;
using Game.Runtime.Gameplay.Levels;
using Newtonsoft.Json.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace Game.Editor
{
    /// <summary>Deterministic data-contract checks; no scene or production-file mutation.</summary>
    public static class LevelDataChecks
    {
        private static int checks;
        private static void Assert(bool value, string name)
        {
            if (!value) throw new Exception("FAILED: " + name);
            checks++;
        }
        private static void Reject(Action action, string name)
        {
            try { action(); }
            catch (FormatException) { checks++; return; }
            throw new Exception("FAILED: accepted " + name);
        }
        private static LevelDefinition Sample()
        {
            var d = new LevelDefinition { id = "check_level", name = "测试", grid = new GridSize { width = 3, height = 2 }, cloneLimit = 3 };
            for (int y = 0; y < 2; y++) for (int x = 0; x < 3; x++)
                d.tiles.Add(new LevelTile { x = x, y = y, type = LevelIds.Floor });
            d.rules.Add(new LevelRule { id = LevelIds.LonelyDeath });
            d.cells.Add(new LevelCell { x = 0, y = 0, type = LevelIds.Player });
            d.cells.Add(new LevelCell { x = 1, y = 1, type = LevelIds.Normal });
            d.winConditions.conditions.Add(new LevelGoal { type = LevelIds.ActorAtPosition, position = new GridPosition { x = 2, y = 0 } });
            return d;
        }
        public static int Run()
        {
            checks = 0;
            var sample = Sample();
            string json = LevelJson.Serialize(sample);
            Assert(LevelValidator.ValidateForPlay(sample).CanPlay, "valid sample");
            Assert(LevelJson.Serialize(LevelJson.Deserialize(json)) == json, "round trip");
            var copy = LevelJson.DeepCopy(sample); copy.cells[0].x = 2; copy.winConditions.conditions[0].position.x = 1;
            Assert(sample.cells[0].x == 0 && sample.winConditions.conditions[0].position.x == 2, "definition deep copy");
            var root = JObject.Parse(json); root.Remove("cloneLimit"); root.Remove("iterationLimit");
            var defaults = LevelJson.Deserialize(root.ToString());
            Assert(defaults.cloneLimit == 0 && defaults.iterationLimit == 1000, "missing defaults");
            Reject(() => LevelJson.Deserialize("{broken"), "broken JSON");
            Reject(() => LevelJson.Deserialize("null"), "null document");
            Reject(() => LevelJson.Deserialize(json + " {}"), "trailing content");
            Reject(() => LevelJson.Deserialize("{\"schemaVersion\":1,\"schemaVersion\":1}"), "duplicate property");
            root = JObject.Parse(json); root.Remove("schemaVersion");
            Reject(() => LevelJson.Deserialize(root.ToString()), "missing version");
            root = JObject.Parse(json); root["cloneLimit"] = 2147483648L;
            Reject(() => LevelJson.Deserialize(root.ToString()), "integer overflow");
            root = JObject.Parse(json); root["schemaVersion"] = 2;
            Reject(() => LevelJson.Deserialize(root.ToString()), "unknown version");
            root = JObject.Parse(json); root["cloneLimit"] = "3";
            Reject(() => LevelJson.Deserialize(root.ToString()), "string integer");
            root = JObject.Parse(json); root["cloneLimit"] = 1.5;
            Reject(() => LevelJson.Deserialize(root.ToString()), "fractional integer");
            root = JObject.Parse(json); root["iterationLimit"] = JValue.CreateNull();
            Reject(() => LevelJson.Deserialize(root.ToString()), "null limit");
            root = JObject.Parse(json); root["extra"] = true;
            Reject(() => LevelJson.Deserialize(root.ToString()), "unknown field");
            root = JObject.Parse(json); root["rules"][0]["id"] = "birth";
            Reject(() => LevelJson.Deserialize(root.ToString()), "birth rule");
            root = JObject.Parse(json); root["rules"][0]["parameters"] = new JObject();
            Assert(LevelJson.Deserialize(root.ToString()).rules[0].id == LevelIds.LonelyDeath, "empty reserved parameters");
            var d = Sample(); d.cloneLimit = -1; Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "negative clone limit");
            d = Sample(); d.iterationLimit = 0; Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "zero iteration limit");
            d = Sample(); d.grid.width = 31; Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "oversize grid");
            d = Sample(); d.tiles.Add(d.tiles[0]); Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "duplicate terrain");
            d = Sample(); d.cells.Add(d.cells[0]); Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "duplicate cell/player");
            d = Sample(); d.cells.RemoveAt(0); Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "missing player");
            d = Sample(); d.tiles[0].type = LevelIds.Wall; Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "cell on wall");
            d = Sample(); d.winConditions.conditions.Clear(); Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "empty goals");
            Assert(LevelJson.DeepCopy(d).winConditions.conditions.Count == 0, "incomplete draft encodable");
            d = Sample(); d.winConditions.conditions[0].position.x = -1;
            Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "goal out of bounds");
            d = Sample(); d.winConditions.conditions[0] = new LevelGoal { type = LevelIds.CoverPositions };
            Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "empty cover positions");
            d.winConditions.conditions[0].positions = new System.Collections.Generic.List<GridPosition>
                { new GridPosition { x = 1, y = 1 }, new GridPosition { x = 1, y = 1 } };
            Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "duplicate cover positions");
            d = Sample(); d.winConditions.conditions[0] = new LevelGoal { type = LevelIds.CellCount, @operator = ">", value = 1 };
            Assert(!LevelValidator.ValidateForPlay(d).Warnings.Any(w => w.Contains("Initial state")), "count excludes player/strict comparison");
            d.winConditions.conditions[0].value = 0;
            Assert(LevelValidator.ValidateForPlay(d).Warnings.Any(w => w.Contains("Initial state")), "initial goal warning");
            d.winConditions.conditions[0].@operator = "=";
            Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "unknown comparison");
            d = Sample(); d.rules.Clear(); Assert(LevelValidator.ValidateForPlay(d).CanPlay && LevelValidator.ValidateForPlay(d).Warnings.Count > 0, "disabled rules allowed");
            d = Sample(); d.rules.Add(d.rules[0]); Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "duplicate rules");
            d = Sample(); d.winConditions.conditions[0] = new LevelGoal { type = LevelIds.CellCount, @operator = ">" };
            Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "missing threshold");
            d = Sample(); d.winConditions.conditions[0] = new LevelGoal { type = LevelIds.CoverPositions,
                positions = new System.Collections.Generic.List<GridPosition> { new GridPosition { x = 1, y = 1 } } };
            Assert(LevelValidator.ValidateForPlay(d).Warnings.Any(w => w.Contains("Initial state")), "covered initial goal warning");
            d = Sample(); d.cloneLimit = 6; Assert(!LevelValidator.ValidateForPlay(d).CanPlay, "clone capacity");
            Assert(!LevelRepository.IsValidId("../evil") && !LevelRepository.IsValidId("x/y") && !LevelRepository.IsValidId("CON"), "safe IDs");
            var state = LevelState.FromDefinition(sample); state.Cells[0].IsClone = true;
            var snapshot = state.DeepCopy(); state.Cells[0].X = 2;
            Assert(snapshot.Cells[0].X == 1 && snapshot.LivingCloneCount == 1 && sample.cells[1].x == 1, "runtime source and snapshot isolation");
            string directory = Path.Combine(Path.GetTempPath(), "level-data-checks-" + Guid.NewGuid().ToString("N"));
            try
            {
                var repo = new LevelRepository(directory); repo.Save(sample);
                Assert(LevelJson.Serialize(repo.Load(sample.id)) == json, "file round trip UTF8");
                sample.name = "更新"; repo.Save(sample);
                Assert(repo.Load(sample.id).name == "更新", "atomic overwrite");
                string old = File.ReadAllText(repo.PathFor(sample.id)); sample.schemaVersion = 2;
                Reject(() => repo.Save(sample), "save unsupported schema");
                Assert(File.ReadAllText(repo.PathFor("check_level")) == old, "failed save preserves file");
                Assert(Directory.GetFiles(directory).Length == 1, "no temporary leftovers");
                sample = Sample(); sample.winConditions.conditions.Clear(); repo.Save(sample);
                Assert(!LevelValidator.ValidateForPlay(repo.Load(sample.id)).CanPlay, "draft save/load");
                File.WriteAllText(repo.PathFor(sample.id), "bad");
                Reject(() => repo.Load(sample.id), "corrupt load");
                Assert(sample.cells.Count == 2, "failed load leaves caller untouched");
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            return checks;
        }
#if UNITY_EDITOR
        [MenuItem("Tools/Levels/Run Data Checks")]
        public static void RunInEditor()
        {
            Debug.Log("Level data checks passed: " + Run());
        }
#endif
    }
}
