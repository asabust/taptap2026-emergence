using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Runtime.Data.Campaign;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Runtime.Gameplay.Campaign
{
    public sealed class CampaignInterface : MonoBehaviour
    {
        public static CampaignInterface Instance { get; private set; }
        public static bool BlocksGameplay => Instance && (Instance.modals.Count > 0 || Instance.loading);
        public static int ReleasedFrame { get; private set; }
        private CampaignRuntime flow;
        private RectTransform root;
        private CampaignPanelView main, dialogue;
        private readonly List<CampaignPanelView> modals = new List<CampaignPanelView>();
        private readonly HashSet<string> read = new HashSet<string>(StringComparer.Ordinal);
        private bool historyWritable = true, loading, reveal, advance, confirmingSkip;
        private int dialogueInputFrame = -1;
        private NarrativeDatabase database;
        public string SelectedChapter { get; set; }
        [Serializable] private sealed class ReadState { public int version = 1; public string[] ids; }
        private const string ReadKey = "Campaign.Narrative.v1";

        public void Initialize(CampaignRuntime runtime)
        {
            Instance = this; flow = runtime;
            root = CampaignUI.Canvas(transform, "Campaign UI", 80);
            root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            var source = Resources.Load<TextAsset>("Campaign/Narrative");
            database = source ? JsonUtility.FromJson<NarrativeDatabase>(source.text) : new NarrativeDatabase();
            try
            {
                string saved = PlayerPrefs.GetString(ReadKey, "");
                if (!string.IsNullOrEmpty(saved))
                {
                    var history = JsonUtility.FromJson<ReadState>(saved);
                    if (history == null || history.version != 1 || history.ids == null) throw new FormatException("Unknown narrative history.");
                    foreach (string id in history.ids) if (!string.IsNullOrEmpty(id)) read.Add(id);
                }
            }
            catch (Exception e) { historyWritable = false; Debug.LogWarning("叙事阅读记录无法读取，原数据保留：" + e.Message); }
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private CampaignPanelView Create(string name, bool modal = false)
        {
            var prefab = Resources.Load<CampaignPanelView>("UI/" + name);
            if (!prefab) throw new InvalidOperationException("UI Prefab 缺失：" + name + "。请执行 Tools/UI/Create Editable UI。");
            var view = Instantiate(prefab, root, false);
            view.name = name; view.gameObject.SetActive(true);
            if (modal) modals.Add(view);
            return view;
        }
        private static void Bind(Button button, Action action)
        {
            if (!button) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { if (UnityEngine.EventSystems.EventSystem.current) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null); action(); });
        }
        public void ClearMain()
        {
            if (main) { main.gameObject.SetActive(false); Destroy(main.gameObject); main = null; }
        }
        private void Close(CampaignPanelView view)
        {
            if (!view) return;
            modals.Remove(view); view.gameObject.SetActive(false); Destroy(view.gameObject);
            ReleasedFrame = Time.frameCount;
        }
        public void SetLoading(bool value)
        {
            loading = value;
            var panel = modals.FirstOrDefault(m => m && m.name == "LoadingPanel");
            if (value && !panel) Create("LoadingPanel", true);
            if (!value && panel) Close(panel);
        }
        public void ShowTitle()
        {
            ClearMain(); SelectedChapter = null; main = Create("TitlePanel");
            main.primary.GetComponentInChildren<TextMeshProUGUI>().text = flow.Progress != null && flow.Catalog.levels.Any(l => flow.Progress.IsCompleted(l.id)) ? "继续游戏" : "开始游戏";
            Bind(main.primary, flow.BeginGame); Bind(main.settings, ShowSettings); Bind(main.back, flow.ConfirmQuit);
#if UNITY_WEBGL && !UNITY_EDITOR
            main.back.gameObject.SetActive(false);
#endif
        }
        public void ShowSelection()
        {
            if (!string.IsNullOrEmpty(SelectedChapter)) { ShowChapter(SelectedChapter); return; }
            ClearMain(); main = Create("LevelSelectPanel"); Bind(main.back, flow.ReturnToTitle);
            if (!flow.Catalog || flow.Progress == null) { main.detail.text = "关卡目录暂不可用。"; return; }
            main.detail.text = flow.Notice;
            main.progress.text = "已完成 " + flow.Catalog.levels.Count(l => flow.Progress.IsCompleted(l.id)) + " / " + flow.Catalog.levels.Count;
            for (int i = 0; i < main.chapterButtons.Length; i++)
            {
                bool exists = i < flow.Catalog.chapters.Count; main.chapterButtons[i].gameObject.SetActive(exists); if (!exists) continue;
                var chapter = flow.Catalog.chapters[i]; int index = i;
                bool unlocked = flow.Progress.IsChapterUnlocked(index);
                int completed = chapter.levelIds.Count(flow.Progress.IsCompleted);
                main.chapterButtons[i].interactable = unlocked;
                main.chapterLabels[i].text = (i == 4 ? "最终章" : "第" + (i + 1) + "章") + " · " + chapter.bodyPart + "\n" + (unlocked ? completed + " / " + chapter.levelIds.Count : "通关上一章解锁");
                Bind(main.chapterButtons[i], () => ShowChapter(chapter.id));
            }
        }
        public void ShowChapter(string id)
        {
            var chapter = flow.Catalog.chapters.FirstOrDefault(c => c.id == id);
            if (chapter == null) { SelectedChapter = null; ShowSelection(); return; }
            int index = flow.Catalog.chapters.IndexOf(chapter);
            if (!flow.Progress.IsChapterUnlocked(index)) { SelectedChapter = null; ShowSelection(); return; }
            SelectedChapter = id; ClearMain(); main = Create("ChapterSelectPanel");
            main.heading.text = chapter.title; main.detail.text = chapter.bodyPart;
            main.progress.text = chapter.levelIds.Count(flow.Progress.IsCompleted) + " / " + chapter.levelIds.Count;
            if (chapter.artwork) { main.artwork.sprite = chapter.artwork; main.artwork.color = Color.white; }
            Bind(main.back, () => { SelectedChapter = null; ShowSelection(); });
            main.itemTemplate.gameObject.SetActive(false);
            for (int i = 0; i < chapter.levelIds.Count; i++)
            {
                string levelId = chapter.levelIds[i]; var entry = flow.Catalog.levels.First(l => l.id == levelId);
                var item = Instantiate(main.itemTemplate, main.content); item.gameObject.SetActive(true); item.name = "Level " + levelId;
                bool unlocked = flow.Progress.IsUnlocked(levelId);
                item.GetComponentInChildren<TextMeshProUGUI>().text = (index + 1) + "-" + (i + 1) + "   " + entry.title + (flow.Progress.IsCompleted(levelId) ? "  ✓" : unlocked ? "" : "  · 未解锁");
                item.interactable = unlocked;
                Bind(item, () => flow.EnterLevel(levelId, Message));
            }
            var archive = database.archives.FirstOrDefault(a => a.archiveId == chapter.archiveId);
            main.archive.gameObject.SetActive(archive != null);
            if (archive != null)
            {
                bool unlocked = chapter.levelIds.All(flow.Progress.IsCompleted); main.archive.interactable = unlocked;
                main.archive.GetComponentInChildren<TextMeshProUGUI>().text = !unlocked ? "章节档案 · 通关本章后解锁" : read.Contains("archive:" + archive.archiveId) ? "查看章节档案" : "章节档案 · 新";
                Bind(main.archive, () => ShowArchive(archive, () => ShowChapter(id)));
            }
        }
        public void Message(string text)
        {
            var v = Create("MessagePanel", true); v.body.text = text; Bind(v.primary, () => Close(v));
        }
        public void ShowRules(string text, Action closed)
        {
            var v = Create("RulesPanel", true); v.body.text = text;
            Bind(v.primary, () => { Close(v); closed(); });
        }
        public void Confirm(string title, string text, Action confirmed)
        {
            var v = Create("ConfirmPanel", true); v.heading.text = title; v.body.text = text;
            Bind(v.back, () => Close(v)); Bind(v.primary, () => { Close(v); confirmed(); });
        }
        public void ShowSettings()
        {
            if (modals.Any(m => m && m.name == "SettingsPanel")) return;
            var v = Create("SettingsPanel", true);
            v.music.SetValueWithoutNotify(PlayerPrefs.GetFloat("MusicVolume", 1));
            v.sfx.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 1));
            v.music.onValueChanged.AddListener(x => { PlayerPrefs.SetFloat("MusicVolume", x); FindObjectOfType<AudioManager>()?.SetMusicVolume(x); });
            v.sfx.onValueChanged.AddListener(x => { PlayerPrefs.SetFloat("SFXVolume", x); FindObjectOfType<AudioManager>()?.SetSFXVolume(x); });
            Bind(v.primary, () => { PlayerPrefs.Save(); Close(v); });
        }
        private void SaveRead(string id)
        {
            read.Add(id); if (!historyWritable) return;
            try { PlayerPrefs.SetString(ReadKey, JsonUtility.ToJson(new ReadState { ids = read.OrderBy(x => x).ToArray() })); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("阅读记录本次仅保留在内存：" + e.Message); }
        }
        public void BeforeLevel(string id, Action finished) { StartCoroutine(PlayGroups(id, "BeforeLevel", finished)); }
        public void AfterLevel(string id, Action finished)
        {
            StartCoroutine(PlayGroups(id, "AfterWin", () =>
            {
                var chapter = flow.Catalog.chapters.FirstOrDefault(c => c.levelIds.Contains(id));
                var archive = chapter == null || !chapter.levelIds.All(flow.Progress.IsCompleted) ? null : database.archives.FirstOrDefault(a => a.archiveId == chapter.archiveId);
                if (chapter != null && chapter.levelIds.All(flow.Progress.IsCompleted)) SelectedChapter = null;
                if (archive != null && !read.Contains("archive:" + archive.archiveId)) ShowArchive(archive, finished);
                else finished();
            }));
        }
        private IEnumerator PlayGroups(string levelId, string timing, Action finished)
        {
            foreach (var group in database.groups.Where(g => g.levelId == levelId && g.timing == timing).OrderBy(g => g.order))
            {
                string key = "dialogue:" + group.dialogueId; if (read.Contains(key)) continue;
                var lines = database.lines.Where(l => l.dialogueId == group.dialogueId).OrderBy(l => l.order).ToArray();
                if (lines.Length == 0) continue;
                dialogue = Create("DialoguePanel", true); bool skip = false;
                Bind(dialogue.primary, AdvanceDialogue);
                Bind(dialogue.secondary, () =>
                {
                    if (confirmingSkip) return; confirmingSkip = true;
                    var confirm = Create("ConfirmPanel", true); confirm.heading.text = "跳过这段对话？"; confirm.body.text = "剧情与教程均可跳过。";
                    Bind(confirm.back, () => { Close(confirm); confirmingSkip = false; });
                    Bind(confirm.primary, () => { Close(confirm); confirmingSkip = false; skip = true; });
                });
                foreach (var line in lines)
                {
                    advance = false; reveal = false;
                    dialogue.speaker.text = line.speakerName ?? ""; dialogue.speaker.gameObject.SetActive(!string.IsNullOrEmpty(line.speakerName));
                    var sprite = string.IsNullOrEmpty(line.portraitId) ? null : Resources.Load<Sprite>("UI/Portraits/" + line.portraitId);
                    dialogue.portrait.gameObject.SetActive(sprite); dialogue.portrait.sprite = sprite;
                    dialogue.body.text = line.text; dialogue.body.maxVisibleCharacters = 0; dialogue.body.ForceMeshUpdate();
                    int count = dialogue.body.textInfo.characterCount; float visible = 0;
                    while (!advance && !skip)
                    {
                        if (Application.isFocused && !confirmingSkip)
                        {
                            visible = reveal ? count : Mathf.Min(count, visible + Time.unscaledDeltaTime * 30);
                            dialogue.body.maxVisibleCharacters = (int)visible;
                        }
                        yield return null;
                    }
                    if (skip) break;
                }
                Close(dialogue); dialogue = null; SaveRead(key);
                yield return null;
            }
            ReleasedFrame = Time.frameCount; finished?.Invoke();
        }
        private void AdvanceDialogue()
        {
            if (!dialogue || confirmingSkip || dialogueInputFrame == Time.frameCount) return;
            dialogueInputFrame = Time.frameCount;
            if (dialogue.body.maxVisibleCharacters < dialogue.body.textInfo.characterCount) reveal = true;
            else advance = true;
        }
        private void ShowArchive(ChapterArchive archive, Action finished)
        {
            var v = Create("ChapterArchivePanel", true); v.heading.text = string.IsNullOrEmpty(archive.title) ? archive.archiveId : archive.title;
            v.body.text = archive.body; v.detail.text = archive.caption ?? "";
            v.artwork.sprite = string.IsNullOrEmpty(archive.illustrationId) ? null : Resources.Load<Sprite>("UI/ArchiveImages/" + archive.illustrationId);
            v.artwork.gameObject.SetActive(v.artwork.sprite);
            Bind(v.primary, () => { SaveRead("archive:" + archive.archiveId); Close(v); finished?.Invoke(); });
        }
        private void Update()
        {
            var keyboard = Keyboard.current; if (keyboard == null || !Application.isFocused || loading) return;
            if (dialogue && !confirmingSkip && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) AdvanceDialogue();
            if (!keyboard.escapeKey.wasPressedThisFrame) return;
            var top = modals.LastOrDefault();
            if (top)
            {
                if (top.name == "SettingsPanel") { PlayerPrefs.Save(); Close(top); }
                else if (top.name == "ConfirmPanel" && top.back) top.back.onClick.Invoke();
                else if (top.name == "MessagePanel") Close(top);
                else if (top.name == "RulesPanel") top.primary.onClick.Invoke();
                return;
            }
            if (main && main.name == "ChapterSelectPanel") { SelectedChapter = null; ShowSelection(); ReleasedFrame = Time.frameCount; }
        }
    }
}
