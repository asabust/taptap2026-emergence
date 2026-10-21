#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Runtime.Data.Campaign;
using Game.Runtime.Gameplay.Campaign;
using Game.Runtime.Gameplay.LevelPlay;
using Game.Runtime.Gameplay.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Opt-in smoke test with temporary content; restores catalog and save even on failure.</summary>
    public static class CampaignIntegrationChecks
    {
        private static bool running;
        private static string ResultPath => Path.Combine(Application.temporaryCachePath, "CampaignIntegrationResult.txt");
        [MenuItem("Tools/Levels/Run Campaign Integration Checks (Play Mode)")]
        public static void Run()
        {
            var flow = CampaignRuntime.Instance;
            if (!EditorApplication.isPlaying || !flow || running) { Debug.LogWarning("请以 Persistent 启动 Play 后运行集成检查，且勿重复启动。"); return; }
            File.WriteAllText(ResultPath,"RUNNING");
            flow.StartCoroutine(CheckFlow(flow));
        }
        private static IEnumerator WaitFor(Func<bool> ready, string step)
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (!ready())
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("Campaign integration timeout: " + step);
                yield return null;
            }
        }
        private static IEnumerator CheckFlow(CampaignRuntime flow)
        {
            running = true;
            var entries = flow.Catalog.levels.ToArray();
            var originalProgress = flow.Progress;
            bool hadSave = PlayerPrefs.HasKey(CampaignRuntime.ProgressKey);
            string saved = PlayerPrefs.GetString(CampaignRuntime.ProgressKey, "");
            string id = "flow_check_" + Guid.NewGuid().ToString("N");
            string nextId = id + "_next";
            string path = LevelRepository.DeveloperDefault().PathFor(id);
            var progressProperty = typeof(CampaignRuntime).GetProperty("Progress");
            bool passed = false;
            try
            {
                yield return WaitFor(() => !flow.Loading, "initial load");
                var fixture = LevelJson.Deserialize(File.ReadAllText(Path.Combine(Application.dataPath,"../Docs/Tests/Fixtures/stage6_probe.json")));
                fixture.id = id;
                File.WriteAllText(path,LevelJson.Serialize(fixture));
                flow.Catalog.levels.Clear();
                flow.Catalog.levels.Add(new CampaignLevel { id=id,title="流程检查",bodyPart="测试部位",bodyPosition=new Vector2(.45f,.25f),description="临时测试，不进入正式关卡目录。" });
                flow.Catalog.levels.Add(new CampaignLevel { id=nextId,title="后续解锁检查",bodyPosition=new Vector2(.56f,.25f) });
                progressProperty.SetValue(flow,new CampaignProgress(new[] { id,nextId }));
                flow.BeginGame();
                yield return WaitFor(() => SceneManager.GetActiveScene().name=="LevelSelect" && !flow.Loading,"title to selection");
                yield return null;
                if (!UnityEngine.Object.FindObjectOfType<LevelSelectionScreen>()) throw new Exception("Selection screen missing.");
                string error=null;
                flow.EnterLevel(id, message=>error=message);
                yield return WaitFor(() => error!=null || SceneManager.GetActiveScene().name=="Gameplay" && !flow.Loading,"selection to gameplay");
                if (error!=null) throw new Exception(error);
                yield return null;
                var screen=UnityEngine.Object.FindObjectOfType<LevelPlayScreen>();
                if (!screen || !screen.formalGame || screen.Session==null) throw new Exception("Formal session not initialized.");
                var session=screen.Session;
                if (!session.TryBeginClonePlacement(out error) || !session.TryCreateClone(1,0,out error) || !session.TryIterate(out error)
                    || !session.TryMove(1,0,out error) || !session.TryMove(1,0,out error) || !session.TryMove(1,0,out error)) throw new Exception(error);
                yield return WaitFor(() => flow.Progress.IsCompleted(id) && GameObject.Find("Victory Card"),"victory card");
                if (!flow.Progress.IsUnlocked(nextId) || flow.HighlightId!=nextId) throw new Exception("Next level not unlocked/highlighted.");
                var reloaded=new CampaignProgress(new[] { id,nextId },PlayerPrefs.GetString(CampaignRuntime.ProgressKey));
                if (!reloaded.IsCompleted(id)) throw new Exception("Progress was not persisted.");
                var back=UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.name=="返回人体图");
                back.onClick.Invoke();
                yield return WaitFor(() => SceneManager.GetActiveScene().name=="LevelSelect" && !flow.Loading,"victory to selection");
                passed = true;
                File.WriteAllText(ResultPath,"PASS: selection, level loading, victory card, persistence, unlock and return");
                Debug.Log("PASS: Persistent/title flow, selection, level loading, formal session, victory card, saved completion, next unlock/highlight and return to anatomy. Test content/save restored.");
            }
            finally
            {
                flow.Catalog.levels.Clear(); flow.Catalog.levels.AddRange(entries);
                progressProperty.SetValue(flow,originalProgress);
                if (hadSave) PlayerPrefs.SetString(CampaignRuntime.ProgressKey,saved); else PlayerPrefs.DeleteKey(CampaignRuntime.ProgressKey);
                PlayerPrefs.Save();
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path+".meta")) File.Delete(path+".meta");
                if (!passed) File.WriteAllText(ResultPath,"FAILED or interrupted; inspect Unity Console");
                running=false;
                flow.ReturnToTitle();
            }
        }
    }
}
#endif
