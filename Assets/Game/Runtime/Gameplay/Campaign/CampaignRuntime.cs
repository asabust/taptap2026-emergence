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
        public CampaignProgress Progress { get; private set; }
        public string Notice { get; private set; } = "";
        public string HighlightId { get; private set; }
        public bool Loading { get; private set; }
        private LevelDefinition pendingLevel;
        private bool progressWritable = true;
        private GameObject dialog;

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
            Catalog = Resources.Load<CampaignCatalog>("Campaign/Catalog");
            if (Catalog)
            {
                try
                {
                    Progress = new CampaignProgress(Catalog.levels?.Select(l => l?.id) ?? Enumerable.Empty<string>(), PlayerPrefs.GetString(ProgressKey, ""));
                    if (Progress.AllCompleted) Notice = "全部关卡已完成。可选择已完成关卡重玩。";
                }
                catch (Exception e) when (e is FormatException || e is ArgumentException)
                {
                    Notice = "进度或关卡目录无法读取：" + e.Message + "\n原进度保留，本次不会覆盖存档。";
                    progressWritable = false;
                    try { Progress = new CampaignProgress(Catalog.levels?.Select(l => l?.id) ?? Enumerable.Empty<string>()); }
                    catch (ArgumentException) { Progress = null; }
                }
            }
            SceneManager.sceneLoaded += SceneLoaded;
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (Instance == this) Instance = null;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Loading = false;
            if (scene.name == "TitleScene")
            {
                CampaignUI.EnsureInput();
                var labels = FindObjectsOfType<TextMeshProUGUI>();
                CampaignUI.Font = labels.FirstOrDefault(t => t.font)?.font;
                BindTitleButton("NewGame", BeginGame);
                BindTitleButton("Setting", ShowSettings);
                BindTitleButton("ExitGame", ConfirmQuit);
            }
            if (scene.name == "Gameplay" && pendingLevel != null)
            {
                var screen = FindObjectOfType<LevelPlayScreen>();
                if (!screen) { Notice = "Gameplay 缺少 LevelPlayScreen。"; ReturnToSelection(); return; }
                screen.loadOnStart = false;
                screen.formalGame = true;
                screen.Initialize(pendingLevel);
                screen.ExitRequested += ReturnToSelection;
                screen.VictoryReached += RecordVictory;
                pendingLevel = null;
            }
        }
        private void BindTitleButton(string name, Action action)
        {
            foreach (var button in FindObjectsOfType<Button>().Where(b => b.gameObject.scene.name == "TitleScene" && b.name == name))
            {
                button.onClick.RemoveAllListeners();
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => action());
            }
        }
        public void BeginGame()
        {
            if (Loading) return;
            if (!Catalog || Progress == null)
            { ShowMessage("关卡目录未配置", "请先执行 Tools → Levels → Configure Stage 7 Game Flow。"); return; }
            // Opening-dialogue insertion point. No story or read-state is invented here.
            HighlightId = Progress.RecommendedId;
            ChangeScene(SelectionScene, GamePhase.LevelSelect);
        }
        public void ReturnToSelection()
        {
            pendingLevel = null;
            ChangeScene(SelectionScene, GamePhase.LevelSelect);
        }
        public void ReturnToTitle() { pendingLevel = null; ChangeScene("TitleScene", GamePhase.GameTitle); }
        private void ChangeScene(string name, GamePhase phase)
        {
            if (Loading) return;
            if (!Application.CanStreamedLevelBeLoaded(name))
            { ShowMessage("无法切换场景", "构建列表缺少场景：" + name); return; }
            Loading = true;
            var manager = FindObjectOfType<GameManager>();
            if (manager) manager.SetGamePhase(phase);
            var transition = FindObjectOfType<TransitionManager>();
            if (transition) StartCoroutine(SwitchWhenReady(transition, name));
            else SceneManager.LoadScene(name);
        }
        private IEnumerator SwitchWhenReady(TransitionManager transition, string name)
        {
            while (transition && transition.IsTrasition) yield return null;
            if (transition) transition.TransitionTo(name);
            else SceneManager.LoadScene(name);
        }
        public void EnterLevel(string id, Action<string> failed)
        {
            if (Loading) return;
            if (Progress == null || !Progress.IsUnlocked(id)) { failed("该关卡尚未解锁。"); return; }
            if (!Application.CanStreamedLevelBeLoaded("Gameplay")) { failed("构建列表缺少 Gameplay 场景。"); return; }
            Loading = true;
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
            if (dialog || Loading) return;
            var root = OpenDialog("音量设置");
            AddVolume(root, "音乐", "MusicVolume", 112, value => FindObjectOfType<AudioManager>()?.SetMusicVolume(value));
            AddVolume(root, "音效", "SFXVolume", 202, value => FindObjectOfType<AudioManager>()?.SetSFXVolume(value));
            CampaignUI.Button(root, "关闭", 180, 330, 240, () => { PlayerPrefs.Save(); CloseDialog(); });
        }
        private void AddVolume(RectTransform root, string label, string key, float y, Action<float> apply)
        {
            CampaignUI.Label(root, label, 40, y, 120, 40);
            var track = CampaignUI.Box(root, label, 160, y, 380, 36, new Color(.7f, .76f, .78f));
            var handle = CampaignUI.Box(track, "Handle", 0, 0, 24, 36, new Color(.15f, .4f, .45f));
            handle.sizeDelta = new Vector2(24, 0);
            var slider = track.gameObject.AddComponent<Slider>();
            slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>(); slider.minValue = 0; slider.maxValue = 1;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(key, 1));
            slider.onValueChanged.AddListener(value => { PlayerPrefs.SetFloat(key, value); apply(value); });
        }
        public void ConfirmQuit()
        {
            if (dialog || Loading) return;
            var root = OpenDialog("退出游戏？");
            CampaignUI.Label(root, "已完成关卡的进度会保留。", 40, 130, 520, 100);
            CampaignUI.Button(root, "取消", 40, 330, 240, CloseDialog);
            CampaignUI.Button(root, "退出", 320, 330, 240, () =>
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
            if (dialog) return;
            var root = OpenDialog(title);
            CampaignUI.Label(root, message, 40, 100, 520, 220, 18);
            CampaignUI.Button(root, "关闭", 180, 330, 240, CloseDialog);
        }
        private RectTransform OpenDialog(string title)
        {
            CampaignUI.EnsureInput();
            var canvas = CampaignUI.Canvas(transform, "Campaign Dialog", 100); dialog = canvas.gameObject;
            var dim = CampaignUI.Box(canvas, "Dim", 0, 0, 0, 0, new Color(0, 0, 0, .75f)); CampaignUI.Stretch(dim);
            var window = CampaignUI.Box(dim, "Window", 0, 0, 600, 420, new Color(.9f, .94f, .95f));
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f); window.anchoredPosition = Vector2.zero;
            CampaignUI.Label(window, title, 40, 30, 520, 48, 28); return window;
        }
        private void CloseDialog() { if (dialog) { dialog.SetActive(false); Destroy(dialog); dialog = null; } }
    }
}
