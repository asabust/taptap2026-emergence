using System;
using System.Collections;
using System.Linq;
using Game.Runtime.Core;
using Game.Runtime.Data.Campaign;
using Game.Runtime.Data.Levels;
using Game.Runtime.Gameplay.LevelPlay;
using Game.Runtime.Gameplay.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.Campaign
{
    /// <summary>Only the formal game subscribes to victory; editor and direct Gameplay tests stay isolated.</summary>
    public sealed class CampaignRuntime : MonoBehaviour
    {
        public const string SelectionScene = "LevelSelect";
        public const string ProgressKey = "Campaign.Progress.v1";
        public static CampaignRuntime Instance { get; private set; }
        public CampaignCatalog Catalog { get; private set; }
        public CampaignArt Art { get; private set; }
        public CampaignProgress Progress { get; private set; }
        public string Notice { get; private set; } = "";
        public string HighlightId { get; private set; }
        public bool Loading { get; private set; }
        public bool HasSave => progressWritable && Progress != null && PlayerPrefs.HasKey(ProgressKey);
        private LevelDefinition pendingLevel;
        private bool progressWritable = true;
        public CampaignInterface UI { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // Also supports Enter Play Mode with domain reload disabled.
            if (Instance) { Destroy(Instance.gameObject); Instance = null; }
            new GameObject("Campaign Runtime").AddComponent<CampaignRuntime>();
        }
        private void Awake()
        {
            Instance = this; DontDestroyOnLoad(gameObject);
            Art = Resources.Load<CampaignArt>("Campaign/Art");
            Catalog = Resources.Load<CampaignCatalog>("Campaign/Catalog");
            if (Catalog)
            {
                try
                {
                    Progress = new CampaignProgress(Catalog.levels?.Select(l => l?.id) ?? Enumerable.Empty<string>(), PlayerPrefs.GetString(ProgressKey, ""));
                    if (Catalog.chapters.Count > 0) Progress.ConfigureChapters(Catalog.chapters.Select(c => c.levelIds.AsEnumerable()));
                    if (Progress.AllCompleted) Notice = "全部关卡已完成。可选择已完成关卡重玩。";
                }
                catch (Exception e) when (e is FormatException || e is ArgumentException)
                {
                    Notice = "进度或关卡目录无法读取：" + e.Message + "\n原进度保留，本次不会覆盖存档。";
                    progressWritable = false;
                    try { Progress = new CampaignProgress(Catalog.levels?.Select(l => l?.id) ?? Enumerable.Empty<string>());
                        if (Catalog.chapters.Count > 0) Progress.ConfigureChapters(Catalog.chapters.Select(c => c.levelIds.AsEnumerable())); }
                    catch (ArgumentException) { Progress = null; }
                }
            }
            SceneManager.sceneLoaded += SceneLoaded;
            UI = gameObject.AddComponent<CampaignInterface>(); UI.Initialize(this);
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (Instance == this) Instance = null;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CampaignUI.EnsureInput();
            Loading = false;
            UI.SetLoading(false);
            if (scene.name == "TitleScene")
            {
                foreach (var canvas in FindObjectsOfType<Canvas>().Where(c => c.gameObject.scene == scene)) canvas.gameObject.SetActive(false);
                UI.ShowTitle();
                return;
            }
            UI.ClearMain();
            if (scene.name == "Gameplay" && pendingLevel != null)
            {
                var screen = FindObjectOfType<LevelPlayScreen>();
                if (!screen) { Notice = "Gameplay 缺少 LevelPlayScreen。"; ReturnToSelection(); return; }
                screen.loadOnStart = false;
                screen.formalGame = true;
                string levelId = pendingLevel.id;
                screen.Initialize(pendingLevel);
                screen.ExitRequested += ReturnToSelection;
                screen.VictoryReached += RecordVictory;
                pendingLevel = null;
                UI.BeforeLevelWhenReady(levelId, screen);
            }
        }
        public void BeginGame()
        {
            if (Loading) return;
            if (!Catalog || Progress == null)
            { ShowMessage("关卡目录未配置", "请先执行 Tools → Levels → Configure Stage 7 Game Flow。"); return; }
            // Opening-dialogue insertion point. No story or read-state is invented here.
            if (progressWritable) { PlayerPrefs.SetString(ProgressKey, Progress.Serialize()); PlayerPrefs.Save(); }
            HighlightId = Progress.RecommendedId;
            UI.SelectedChapter = null;
            ChangeScene(SelectionScene, GamePhase.LevelSelect);
        }
        public void NewGame()
        {
            if (Loading || !Catalog) return;
            Action start = () =>
            {
                var fresh = new CampaignProgress(Catalog.levels.Select(l => l.id));
                if (Catalog.chapters.Count > 0) fresh.ConfigureChapters(Catalog.chapters.Select(c => c.levelIds.AsEnumerable()));
                Progress = fresh; progressWritable = true; Notice = ""; pendingLevel = null;
                UI.ResetNarrativeHistory(); BeginGame();
            };
            if (PlayerPrefs.HasKey(ProgressKey)) UI.Confirm("开始新游戏？", "现有通关进度、对话阅读记录和章节档案解锁将重置。音量设置会保留。", start);
            else start();
        }
        public void ReturnToSelection()
        {
            pendingLevel = null;
            ChangeScene(SelectionScene, GamePhase.LevelSelect);
        }
        public void ReturnToTitle() { pendingLevel = null; Loading = false; ChangeScene("TitleScene", GamePhase.GameTitle); }
        private void ChangeScene(string name, GamePhase phase)
        {
            if (Loading) return;
            if (!Application.CanStreamedLevelBeLoaded(name))
            { ShowMessage("无法切换场景", "构建列表缺少场景：" + name); return; }
            var alreadyLoaded = SceneManager.GetSceneByName(name);
            if (alreadyLoaded.IsValid() && alreadyLoaded.isLoaded)
            {
                SceneManager.SetActiveScene(alreadyLoaded);
                FindObjectOfType<GameManager>()?.SetGamePhase(phase);
                return;
            }
            Loading = true;
            UI.SetLoading(true);
            var manager = FindObjectOfType<GameManager>();
            if (manager) manager.SetGamePhase(phase);
            var transition = FindObjectOfType<TransitionManager>();
            if (transition) StartCoroutine(SwitchWhenReady(transition, name));
            else SceneManager.LoadScene(name);
        }
        private IEnumerator SwitchWhenReady(TransitionManager transition, string name)
        {
            while (transition && transition.IsTrasition) yield return null;
            if (transition)
            {
                string current = SceneManager.GetActiveScene().name;
                transition.Transition(current == "Persistent" ? "" : current, name);
            }
            else SceneManager.LoadScene(name);
        }
        public void EnterLevel(string id, Action<string> failed)
        {
            if (Loading) return;
            if (Progress == null || !Progress.IsUnlocked(id)) { failed("该关卡尚未解锁。"); return; }
            if (!Application.CanStreamedLevelBeLoaded("Gameplay")) { failed("构建列表缺少 Gameplay 场景。"); return; }
            Loading = true;
            UI.SetLoading(true);
            StartCoroutine(LoadLevel(id, failed));
        }
        private IEnumerator LoadLevel(string id, Action<string> failed)
        {
            string json = null, error = null;
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                using (var request = UnityWebRequest.Get(Application.streamingAssetsPath.TrimEnd('/') + "/Levels/" + Uri.EscapeDataString(id) + ".json"))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) error = request.error;
                    else json = request.downloadHandler.text;
                }
            }
            else
            {
                try { json = System.IO.File.ReadAllText(LevelRepository.DeveloperDefault().PathFor(id)); }
                catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { error = e.Message; }
            }
            if (error == null)
            {
                try
                {
                    var level = LevelJson.Deserialize(json);
                    if (level.id != id) throw new FormatException("Level ID differs from filename.");
                    var validation = LevelValidator.ValidateForPlay(level);
                    if (!validation.CanPlay) throw new FormatException(string.Join("\n", validation.Errors));
                    pendingLevel = level;
                }
                catch (Exception e) when (e is FormatException || e is ArgumentException) { error = e.Message; }
            }
            Loading = false;
            UI.SetLoading(false);
            if (error != null) { failed("无法读取关卡 " + id + "：" + error); yield break; }
            HighlightId = id;
            ChangeScene("Gameplay", GamePhase.Gameplay);
        }
        private void RecordVictory(string id)
        {
            if (Progress == null || !Progress.IsUnlocked(id)) return;
            Progress.Complete(id);
            HighlightId = Progress.NextId(id) ?? id;
            Notice = Progress.AllCompleted ? "全部关卡已完成。可选择已完成关卡重玩。" : "关卡已完成，后续关卡已解锁。";
            if (!progressWritable) { Notice += "\n原存档异常，本次进度仅在内存中保留。"; return; }
            try { PlayerPrefs.SetString(ProgressKey, Progress.Serialize()); PlayerPrefs.Save(); }
            catch (Exception e) { Notice += "\n保存失败，本次进度仅在内存中保留：" + e.Message; }
        }
        public void ShowSettings()
        {
            UI.ShowSettings();
        }
        public void ConfirmQuit()
        {
            UI.Confirm("退出游戏？", SceneManager.GetActiveScene().name == "Gameplay" ? "退出本局状态不会保存" : "已完成关卡的进度会保留。", () =>
            {
                PlayerPrefs.Save();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
        private void ShowMessage(string title, string message)
        {
            UI.Message(title + "\n" + message);
        }
    }
}
