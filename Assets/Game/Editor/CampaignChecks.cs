using System;
using Game.Runtime.Gameplay.Campaign;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace Game.Editor
{
    public static class CampaignChecks
    {
        private static int checks;
        private static void Check(bool ok, string message) { if (!ok) throw new Exception("FAILED: " + message); checks++; }
        private static void Reject(Action action, string message)
        {
            try { action(); } catch (Exception e) when (e is FormatException || e is ArgumentException || e is InvalidOperationException)
            { checks++; return; }
            throw new Exception("FAILED: " + message);
        }
        public static int Run()
        {
            checks = 0;
            var p = new CampaignProgress(new[] { "a", "b", "c" });
            Check(p.IsUnlocked("a") && !p.IsUnlocked("b") && !p.IsUnlocked("c"), "first level only unlocked");
            Check(!p.IsUnlocked("missing") && !p.AllCompleted, "unknown ID and fresh completion");
            Check(p.RecommendedId == "a", "fresh recommendation");
            Reject(() => p.Complete("b"), "cannot complete locked level");
            Reject(() => p.Complete("missing"), "cannot complete unknown level");
            Check(p.Complete("a") && p.IsCompleted("a"), "record first win");
            Check(p.IsUnlocked("b") && !p.IsUnlocked("c"), "unlock next only");
            Check(!p.Complete("a") && p.RecommendedId == "b", "repeat win idempotent");
            var loaded = new CampaignProgress(new[] { "a", "b", "c" }, p.Serialize());
            Check(loaded.IsCompleted("a") && loaded.IsUnlocked("b") && !loaded.IsCompleted("b"), "roundtrip progress");
            Check(loaded.NextId("a") == "b" && loaded.NextId("c") == null && loaded.NextId("unknown") == null, "next and final boundaries");
            loaded.Complete("b"); loaded.Complete("c");
            Check(loaded.AllCompleted && loaded.RecommendedId == "c", "final completion does not select missing next");
            Check(loaded.IsUnlocked("a") && loaded.IsUnlocked("b"), "completed levels replayable");
            var reordered = new CampaignProgress(new[] { "c", "b", "a", "d" }, loaded.Serialize());
            Check(reordered.IsCompleted("a") && reordered.IsCompleted("c") && reordered.IsUnlocked("d"), "progress follows stable IDs");
            var absent = new CampaignProgress(new[] { "a" }, loaded.Serialize());
            var restored = new CampaignProgress(new[] { "a", "b", "c" }, absent.Serialize());
            Check(restored.AllCompleted, "temporarily removed IDs preserved");
            var single = new CampaignProgress(new[] { "only" }); single.Complete("only");
            Check(single.AllCompleted && single.NextId("only") == null, "single-level campaign");
            Reject(() => new CampaignProgress(new string[0]), "empty catalog rejected");
            Reject(() => new CampaignProgress(new[] { "a", "a" }), "duplicate catalog rejected");
            Reject(() => new CampaignProgress(new[] { "../a" }), "invalid ID rejected");
            foreach (string json in new[] { "{", "null", "[]", "{}", "{\"version\":2,\"completed\":[]}", "{\"version\":1,\"completed\":null}",
                "{\"version\":1,\"completed\":[3]}", "{\"version\":1,\"completed\":[\"../a\"]}", "{\"version\":1,\"completed\":[],\"extra\":true}",
                "{\"version\":1,\"version\":1,\"completed\":[]}" })
                Reject(() => new CampaignProgress(new[] { "a" }, json), "malformed progress rejected");
            Check(p.Serialize() == new CampaignProgress(new[] { "a", "b", "c" }, p.Serialize()).Serialize(), "deterministic serialization");
            return checks;
        }
#if UNITY_EDITOR
        [MenuItem("Tools/Levels/Run Campaign Checks")]
        public static void Menu() { Debug.Log("Campaign checks passed: " + Run()); }
#endif
    }
}
