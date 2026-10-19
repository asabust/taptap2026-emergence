#if UNITY_EDITOR
using System;
using System.Collections;
using Game.Runtime.Gameplay.Campaign;
using Game.Runtime.Gameplay.LevelPlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>Opt-in Play Mode smoke test using the committed probe, without deleting saved progress.</summary>
    public static class CampaignIntegrationChecks
    {
        [MenuItem("Tools/Levels/Run Campaign Integration Checks (Play Mode)")]
        public static void Run()
        {
            var flow = CampaignRuntime.Instance;
            if (!EditorApplication.isPlaying || !flow) { Debug.LogWarning("请以 Persistent 启动 Play 后运行集成检查。"); return; }
            flow.StartCoroutine(CheckFlow(flow));
        }
        private static IEnumerator WaitFor(Func<bool> ready, string step)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!ready())
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("Campaign integration timeout: " + step);
                yield return null;
            }
        }
        private static IEnumerator CheckFlow(CampaignRuntime flow)
        {
            Debug.Log("Campaign integration started: scene=" + SceneManager.GetActiveScene().name + ", loading=" + flow.Loading);
            yield return WaitFor(() => !flow.Loading, "initial load");
            if (!flow.Catalog || flow.Catalog.levels.Count < 2 || flow.Catalog.levels[0].id != "stage6_probe")
                throw new Exception("Integration check requires stage6_probe as the first catalog entry.");
            flow.BeginGame();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "LevelSelect" && !flow.Loading, "title to selection");
            string error = null;
            flow.EnterLevel("stage6_probe", message => error = message);
            yield return WaitFor(() => error != null || SceneManager.GetActiveScene().name == "Gameplay" && !flow.Loading, "selection to gameplay");
            if (error != null) throw new Exception(error);
            yield return null; // Let LevelPlayScreen.Start build the HUD.
            var screen = UnityEngine.Object.FindObjectOfType<LevelPlayScreen>();
            if (!screen || !screen.formalGame || screen.Session == null) throw new Exception("Formal session not initialized.");
            var session = screen.Session;
            if (!session.TryBeginClonePlacement(out error) || !session.TryCreateClone(1, 0, out error)
                || !session.TryIterate(out error) || !session.TryMove(1, 0, out error)
                || !session.TryMove(1, 0, out error) || !session.TryMove(1, 0, out error)) throw new Exception(error);
            // Victory notification uses the normal screen's LateUpdate, not a test-only completion call.
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "LevelSelect" && !flow.Loading, "victory return");
            if (!flow.Progress.IsCompleted("stage6_probe") || !flow.Progress.IsUnlocked(flow.Catalog.levels[1].id))
                throw new Exception("Victory did not complete and unlock the next ID.");
            if (flow.HighlightId != flow.Catalog.levels[1].id) throw new Exception("Next level not highlighted.");
            var reloaded = new CampaignProgress(new[] { "stage6_probe", flow.Catalog.levels[1].id }, PlayerPrefs.GetString(CampaignRuntime.ProgressKey));
            if (!reloaded.IsCompleted("stage6_probe")) throw new Exception("Victory was not persisted.");
            Debug.Log("PASS: campaign scene loading, formal initialization, victory return, next unlock/highlight and saved progress reload");
        }
    }
}
#endif
