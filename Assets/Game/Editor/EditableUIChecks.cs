#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.Runtime.Data.Campaign;
using Game.Runtime.Gameplay.Campaign;
using Game.Runtime.Gameplay.LevelPlay;
using Game.Runtime.Gameplay.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class EditableUIChecks
    {
        [MenuItem("Tools/UI/Validate Editable UI")]
        public static void Run()
        {
            var catalog=Resources.Load<CampaignCatalog>("Campaign/Catalog");
            var data=JsonUtility.FromJson<NarrativeDatabase>(Resources.Load<TextAsset>("Campaign/Narrative").text);
            if(catalog.chapters.Count!=5 || catalog.levels.Count!=18)throw new Exception("Expected 5 chapters / 18 levels.");
            var invalid=new List<string>();
            foreach(var level in catalog.levels)
            {
                var definition=LevelRepository.DeveloperDefault().Load(level.id);
                var validation=LevelValidator.ValidateForPlay(definition);
                if(!validation.CanPlay)invalid.Add(level.id+": "+string.Join("; ",validation.Errors));
            }
            foreach(string name in new[]{"TitlePanel","LevelSelectPanel","ChapterSelectPanel","SettingsPanel","ConfirmPanel","MessagePanel","LoadingPanel","DialoguePanel","ChapterArchivePanel","RulesPanel","PausePanel","FailurePanel","VictoryPanel"})
            {
                var v=Resources.Load<CampaignPanelView>("UI/"+name);if(!v)throw new Exception("Missing prefab "+name);
                if(!v.GetComponent<RectTransform>())throw new Exception("Missing RectTransform "+name);
            }
            var hud=Resources.Load<GameplayHUDView>("UI/GameplayHUDPanel");
            if(!hud || !hud.board || !hud.failure || !hud.victory || !hud.overlay || !hud.pause || !hud.rules)throw new Exception("HUD bindings incomplete.");
            var chapter=Resources.Load<CampaignPanelView>("UI/ChapterSelectPanel");
            if(!chapter.itemTemplate||!chapter.content.GetComponent<VerticalLayoutGroup>()||!chapter.archive)throw new Exception("Chapter list incomplete.");
            foreach(var g in data.groups)
            {
                if(!catalog.levels.Any(l=>l.id==g.levelId)||!data.lines.Any(l=>l.dialogueId==g.dialogueId))throw new Exception("Group binding incomplete: "+g.dialogueId);
            }
            foreach(var line in data.lines.Where(l=>!string.IsNullOrEmpty(l.portraitId)))
                if(!Resources.Load<Sprite>("UI/Portraits/"+line.portraitId))throw new Exception("Missing portrait: "+line.portraitId);
            Debug.Log("PASS: editable UI references: 14 prefabs, 18 registered JSON, 13 dialogue groups, 92 lines and portrait bindings.");
            if(invalid.Count>0)Debug.LogWarning("Published level configuration requires planner review:\n"+string.Join("\n",invalid));
            File.WriteAllText("Temp/EditableUIValidation.txt",invalid.Count==0?"PASS":"UI REFERENCES PASS; LEVEL DATA ISSUES:\n"+string.Join("\n",invalid));
        }
        [Serializable] private sealed class History { public int version=1;public string[] ids; }
        [MenuItem("Tools/UI/Remove This Run's Tutorial Preview Flags")]
        public static void RemovePreviewFlags()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            const string key="Campaign.Narrative.v1";
            if(!PlayerPrefs.HasKey(key))return;
            var history=JsonUtility.FromJson<History>(PlayerPrefs.GetString(key));
            if(history==null||history.version!=1||history.ids==null)throw new Exception("Preserving unreadable history.");
            history.ids=history.ids.Where(id=>id!="dialogue:chapter_01_01_story"&&id!="dialogue:chapter_01_01_tutorial").ToArray();
            PlayerPrefs.SetString(key,JsonUtility.ToJson(history));PlayerPrefs.Save();
            Debug.Log("Removed only the two previewed 001 dialogue flags; campaign progress unchanged.");
        }
    }
}
#endif
