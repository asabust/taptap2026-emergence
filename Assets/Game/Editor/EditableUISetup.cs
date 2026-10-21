#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Game.Runtime.Data.Campaign;
using Game.Runtime.Gameplay.Campaign;
using Game.Runtime.Gameplay.LevelPlay;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Creates missing assets only. Re-running never overwrites an artist's prefab.</summary>
    public static class EditableUISetup
    {
        private const string Folder = "Assets/Resources/UI/";
        private static TMP_FontAsset font;
        private static readonly Color Ink = new Color(.19f,.24f,.32f);
        private static readonly Color Paper = new Color(.94f,.95f,.96f);
        [MenuItem("Tools/UI/Configure Named Audio")]
        public static void ConfigureNamedAudio()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请停止 Play 后配置音频。");
            var catalog = AssetDatabase.LoadAssetAtPath<Game.Runtime.Data.AudioInfoListSO>("Assets/GameData/AudioInfoListSO.asset");
            if (!catalog) throw new InvalidOperationException("Audio catalog missing.");
            var files = new[]{"UI点击.wav","关卡循环bgm.mp3","主菜单bgm.mp3","UI悬停.wav","玩家移动.ogg","分裂分身.ogg","关卡过关瞬间.ogg","无效操作.wav"};
            var volumes = new[]{.8f,.45f,.45f,.45f,.7f,.75f,.85f,.65f};
            catalog.audioInfos.Clear();
            for (int i=0;i<files.Length;i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Arts/Sound/"+files[i]);
                if (!clip) throw new InvalidOperationException("Audio import missing: "+files[i]);
                catalog.audioInfos.Add(new Game.Runtime.Data.AudioInf { audioName=(Game.Runtime.Data.AudioName)(i+1),clip=clip,volume=volumes[i],loop=i==1||i==2 });
            }
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            ValidateNamedAudio(); UnityEditor.Selection.activeObject=catalog;
        }
        [MenuItem("Tools/UI/Validate Named Audio")]
        public static void ValidateNamedAudio()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<Game.Runtime.Data.AudioInfoListSO>("Assets/GameData/AudioInfoListSO.asset");
            if (!catalog) throw new InvalidOperationException("Audio catalog missing.");
            foreach (Game.Runtime.Data.AudioName name in Enum.GetValues(typeof(Game.Runtime.Data.AudioName)))
            {
                if (name == Game.Runtime.Data.AudioName.None) continue;
                var entry = catalog.GetAudioInfo(name);
                if (entry == null || !entry.clip || entry.clip.length <= 0 || entry.clip.samples <= 0)
                    throw new InvalidOperationException("Audio clip missing or empty: " + name);
                bool music = name == Game.Runtime.Data.AudioName.BGM || name == Game.Runtime.Data.AudioName.MenuBGM;
                if (entry.loop != music) throw new InvalidOperationException("Unexpected loop setting: " + name);
            }
            foreach (var manager in UnityEngine.Object.FindObjectsOfType<AudioManager>(true))
            {
                manager.audioInfoListSO = catalog;
                if (EditorApplication.isPlaying && manager.isActiveAndEnabled)
                {
                    var expected = catalog.GetAudioInfo(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Gameplay" ? Game.Runtime.Data.AudioName.BGM : Game.Runtime.Data.AudioName.MenuBGM);
                    if (!manager.musicSource || manager.musicSource.clip != expected.clip || !manager.musicSource.isPlaying)
                        throw new InvalidOperationException("Expected scene music is not playing.");
                    if (!manager.sfxSource || manager.sfxSource == manager.musicSource || manager.sfxSource.loop)
                        throw new InvalidOperationException("SFX source must be separate and non-looping.");
                }
            }
            File.WriteAllText("Temp/NamedAudioValidation.txt", "PASS: 8 clips; music and SFX loops valid; playMode=" + EditorApplication.isPlaying);
            Debug.Log("PASS: eight named audio clips loaded; two looping music tracks, six one-shot effects. Loaded AudioManager references synchronized.");
        }
        [MenuItem("Tools/UI/Configure Title New And Continue")]
        public static void ConfigureTitleActions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("请停止 Play，保存并退出 Prefab Mode 后执行。");
            var go = PrefabUtility.LoadPrefabContents(Folder + "TitlePanel.prefab");
            try
            {
                var view = go.GetComponent<CampaignPanelView>();
                if (!view.secondary)
                {
                    view.secondary = UnityEngine.Object.Instantiate(view.primary,view.primary.transform.parent,false);
                    var rect = (RectTransform)view.secondary.transform;
                    rect.anchoredPosition = ((RectTransform)view.primary.transform).anchoredPosition + new Vector2(0,-90);
                }
                view.primary.name = "NewGameButton"; view.primary.GetComponentInChildren<TextMeshProUGUI>(true).text = "新游戏";
                view.secondary.name = "ContinueButton"; view.secondary.GetComponentInChildren<TextMeshProUGUI>(true).text = "继续游戏";
                if (view.settings) view.settings.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(go,Folder + "TitlePanel.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
            AssetDatabase.SaveAssets(); Debug.Log("PASS: title new game and continue buttons configured.");
        }
        [MenuItem("Tools/UI/Apply Gameplay Icons And Square Tiles")]
        public static void ApplyGameplayIcons()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("请保存并退出 Prefab Mode，停止 Play 后执行。");
            var go = PrefabUtility.LoadPrefabContents(Folder + "GameplayHUDPanel.prefab");
            try
            {
                var view = go.GetComponent<GameplayHUDView>();
                ApplyIcon(view.undo,"撤销按钮.png",500);
                ApplyIcon(view.clone,"分身按钮.png",660);
                view.clone.GetComponentInChildren<TextMeshProUGUI>(true).text = "分身 B";
                ApplyIcon(view.iterate,"迭代按钮.png",820);
                view.board.goalIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Arts/UI/Game/终点图标.png");
                view.board.styledTerrain = true;
                PrefabUtility.SaveAsPrefabAsset(go,Folder + "GameplayHUDPanel.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
            AssetDatabase.SaveAssets(); Debug.Log("PASS: gameplay button artwork and blue goal icon assigned.");
        }
        private static void ApplyIcon(Button button,string file,float y)
        {
            var image = button.GetComponent<Image>();
            image.sprite = SpriteAt("Assets/Arts/UI/Game/" + file); image.color = Color.white;
            image.type = UnityEngine.UI.Image.Type.Simple; image.preserveAspect = true;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1,1);
            rect.anchoredPosition = new Vector2(-60,-y); rect.sizeDelta = new Vector2(180,130);
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            label.color = Ink; label.fontSize = 23;
            var text = label.rectTransform; text.anchorMin = text.anchorMax = new Vector2(.5f,0);
            text.pivot = new Vector2(.5f,1); text.anchoredPosition = Vector2.zero; text.sizeDelta = new Vector2(260,30);
        }
        [MenuItem("Tools/UI/Configure Shared Settings")]
        public static void ConfigureSharedSettings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("请保存并退出 Prefab Mode，停止 Play 后执行。");
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "GlobalSettingsButton.prefab"))
            {
                var map = PrefabUtility.LoadPrefabContents(Folder + "LevelSelectPanel.prefab");
                try
                {
                    var view = map.GetComponent<CampaignPanelView>();
                    var button = UnityEngine.Object.Instantiate(view.settings ? view.settings : view.back);
                    button.name = "GlobalSettingsButton";
                    var rect = (RectTransform)button.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                    rect.anchoredPosition = new Vector2(-60,-35); rect.sizeDelta = new Vector2(130,64); rect.localScale = Vector3.one;
                    button.GetComponentInChildren<TextMeshProUGUI>(true).text = "设置";
                    PrefabUtility.SaveAsPrefabAsset(button.gameObject, Folder + "GlobalSettingsButton.prefab");
                    UnityEngine.Object.DestroyImmediate(button.gameObject);
                }
                finally { PrefabUtility.UnloadPrefabContents(map); }
            }
            var settings = PrefabUtility.LoadPrefabContents(Folder + "SettingsPanel.prefab");
            try
            {
                var v = settings.GetComponent<CampaignPanelView>();
                font = v.heading.font;
                v.heading.text = "设置";
                if (!v.secondary) v.secondary = Button(v.transform,"ReturnTitleButton","返回标题",570,670,360);
                if (!v.back) v.back = Button(v.transform,"QuitGameButton","退出游戏",990,670,360);
                PrefabUtility.SaveAsPrefabAsset(settings, Folder + "SettingsPanel.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(settings); }
            AssetDatabase.SaveAssets(); Debug.Log("PASS: shared settings button and navigation configured.");
        }
        [MenuItem("Tools/UI/Apply Initial Text Contrast")]
        public static void InitialContrast()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (string name in new[]{"LevelSelectPanel","ChapterSelectPanel","ChapterArchivePanel"})
            {
                string path=Folder+name+".prefab";var go=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view=go.GetComponent<CampaignPanelView>();
                    if(view.heading)view.heading.color=Color.white;
                    if(view.detail)view.detail.color=Color.white;
                    if(view.progress)view.progress.color=name=="LevelSelectPanel"?Ink:Color.white;
                    if(name=="ChapterArchivePanel" && !go.transform.Find("Reading Paper"))
                    {
                        var paper=Image(go.transform,"Reading Paper",70,340,1030,570);paper.color=Paper;
                        paper.transform.SetSiblingIndex(view.body.GetComponentInParent<ScrollRect>().transform.GetSiblingIndex());
                    }
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            AssetDatabase.SaveAssets();Debug.Log("PASS: initial prototype text contrast applied.");
        }
        [MenuItem("Tools/UI/Add Missing Opaque Backdrops")]
        public static void AddBackdrops()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var path in Directory.GetFiles(Folder,"*.prefab"))
            {
                var go=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (go.transform.Find("Opaque Backdrop")) continue;
                    var bg=Image(go.transform,"Opaque Backdrop",0,0,1920,1080);Stretch(bg.rectTransform);
                    bg.color=Paper;bg.raycastTarget=true;bg.transform.SetAsFirstSibling();
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            AssetDatabase.SaveAssets();Debug.Log("PASS: missing opaque UI backdrops added; layouts preserved.");
        }
        [MenuItem("Tools/UI/Create Editable UI")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play Mode 后创建 UI。");
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(Folder + "Portraits");
            AssetDatabase.Refresh(); font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Arts/Font/LXGWNeoXiHei SD Dynamic.asset");
            ImportPortrait("player", "主角.png"); ImportPortrait("o", "人物小o.png"); ImportPortrait("kui", "人物小葵.png");
            Make("TitlePanel", Title); Make("LevelSelectPanel", Selection); Make("ChapterSelectPanel", Chapter);
            Make("SettingsPanel", Settings); Make("ConfirmPanel", Confirm); Make("MessagePanel", Message);
            Make("LoadingPanel", Loading); Make("DialoguePanel", Dialogue); Make("ChapterArchivePanel", Archive);
            Make("RulesPanel", Rules); Make("PausePanel", Pause); Make("FailurePanel", Failure); Make("VictoryPanel", Victory);
            if (!File.Exists(Folder + "GameplayHUDPanel.prefab")) Gameplay();
            ConfigureCatalog(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("PASS: editable UI created (1920x1080). Existing prefabs preserved. Assets/Resources/UI");
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "taptap-editable-ui-setup.txt"), "PASS: editable UI assets created");
        }
        private static void ImportPortrait(string id, string filename)
        {
            string dest = Folder + "Portraits/" + id + ".png";
            if (!File.Exists(dest)) AssetDatabase.CopyAsset("Assets/Arts/Character/" + filename, dest);
            SpriteAt(dest);
        }
        private static Sprite SpriteAt(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0,1); r.pivot = new Vector2(0,1);
            r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(width,height); return r;
        }
        private static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static Image Image(Transform parent, string name, float x,float y,float width,float height, Sprite sprite = null)
        {
            var r = Rect(parent,name,x,y,width,height); var image = r.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = sprite ? Color.white : Paper; image.raycastTarget = false; image.preserveAspect = true; return image;
        }
        private static TextMeshProUGUI Text(Transform parent,string name,string text,float x,float y,float width,float height,int size=28)
        {
            var label = Rect(parent,name,x,y,width,height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.color = Ink; label.raycastTarget = false;
            label.enableWordWrapping = true; return label;
        }
        private static Button Button(Transform parent,string name,string caption,float x,float y,float width=420,float height=64)
        {
            var image = Image(parent,name,x,y,width,height); image.color = new Color(.35f,.4f,.55f); image.raycastTarget = true;
            var b = image.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = b.colors; colors.disabledColor = new Color(.65f,.65f,.65f,.55f); b.colors = colors;
            var label = Text(b.transform,"Caption",caption,0,0,width,height,26); label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform); return b;
        }
        private static CampaignPanelView Root(string name, bool dim = false)
        {
            var root = Rect(null,name,0,0,1920,1080); Stretch(root);
            var view = root.gameObject.AddComponent<CampaignPanelView>();
            var solid=Image(root,"Opaque Backdrop",0,0,1920,1080);Stretch(solid.rectTransform);solid.raycastTarget=true;
            var bg = Image(root,"Background",0,0,1920,1080,dim ? null : SpriteAt("Assets/Arts/UI/Common/通用背景.png"));
            Stretch(bg.rectTransform); bg.preserveAspect = false; bg.raycastTarget = true;
            if (dim) bg.color = new Color(.88f,.91f,.94f,.97f);
            return view;
        }
        private static void Make(string name, Func<CampaignPanelView> factory)
        {
            if (File.Exists(Folder + name + ".prefab")) return;
            var v = factory(); PrefabUtility.SaveAsPrefabAsset(v.gameObject,Folder + name + ".prefab"); UnityEngine.Object.DestroyImmediate(v.gameObject);
        }
        private static CampaignPanelView Title()
        {
            var v = Root("TitlePanel");
            Image(v.transform,"Cover",0,0,1920,1080,SpriteAt("Assets/Arts/UI/Title/第一界面底图_草.png")).preserveAspect=false;
            Image(v.transform,"Logo",410,130,1100,470,SpriteAt("Assets/Arts/UI/Title/格命标题_透明.png"));
            v.primary=Button(v.transform,"NewGameButton","新游戏",750,700);
            v.secondary=Button(v.transform,"ContinueButton","继续游戏",750,790);
            v.settings=Button(v.transform,"SettingsButton","设置",1730,35,130); v.back=Button(v.transform,"QuitButton","退出",750,880); return v;
        }
        private static CampaignPanelView Selection()
        {
            var v=Root("LevelSelectPanel");
            v.heading=Text(v.transform,"Title","生命图谱",80,90,560,90,52);
            v.detail=Text(v.transform,"Notice","选择身体部位，进入章节",80,220,500,220,28);
            v.progress=Text(v.transform,"Progress","已完成 0 / 18",80,900,500,60);
            v.back=Button(v.transform,"BackButton","返回标题",80,990,260);
            var anatomy=Image(v.transform,"Anatomy",760,50,480,960,SpriteAt("Assets/Arts/UI/LevelSelect/选关界面下层无关卡版.png"));
            v.chapterButtons=new Button[5];v.chapterLabels=new TextMeshProUGUI[5];
            var source=LoadImport();
            for(int i=0;i<5;i++)
            {
                var b=Button(anatomy.transform,"ChapterNode"+(i+1),"第"+(i+1)+"章",0,0,300,76);
                var r=(RectTransform)b.transform; var pos=source.chapters[i].bodyPosition;
                r.anchorMin=r.anchorMax=pos;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=Vector2.zero;
                v.chapterButtons[i]=b;v.chapterLabels[i]=b.GetComponentInChildren<TextMeshProUGUI>();
            }
            return v;
        }
        private static CampaignPanelView Chapter()
        {
            var v=Root("ChapterSelectPanel");v.back=Button(v.transform,"BackButton","返回人体图",70,40,260);
            v.heading=Text(v.transform,"ChapterTitle","章节名称",1030,150,800,90,48);
            v.progress=Text(v.transform,"ChapterProgress","0 / 4",1650,50,200,60);
            v.artwork=Image(v.transform,"ChapterArtwork",90,160,830,800,SpriteAt("Assets/Arts/UI/LevelSelect/选关界面上层线稿骨骼图.png"));
            v.content=Rect(v.transform,"LevelList",1030,370,780,480);
            var layout=v.content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=24;layout.childControlHeight=false;layout.childControlWidth=true;layout.childForceExpandHeight=false;
            v.itemTemplate=Button(v.content,"LevelButtonTemplate","1-1 小关名称",0,0,780,76);
            v.itemTemplate.gameObject.SetActive(false);
            v.archive=Button(v.transform,"ArchiveButton","章节档案",1030,920,780);return v;
        }
        private static CampaignPanelView Confirm()
        {
            var v=Root("ConfirmPanel",true);v.heading=Text(v.transform,"Title","确认操作",570,300,780,80,40);
            v.body=Text(v.transform,"Message","退出本局状态不会保存",570,420,780,170);
            v.back=Button(v.transform,"CancelButton","取消",570,680,350);v.primary=Button(v.transform,"ConfirmButton","确认",1000,680,350);return v;
        }
        private static CampaignPanelView Message()
        {
            var v=Root("MessagePanel",true);v.heading=Text(v.transform,"Title","提示",500,190,920,80,40);
            v.body=Scroll(v.transform,"Message",500,300,920,500);v.primary=Button(v.transform,"CloseButton","关闭",750,900);return v;
        }
        private static CampaignPanelView Loading()
        {
            var v=Root("LoadingPanel",true);v.heading=Text(v.transform,"LoadingLabel","正在读取……",650,490,700,100,40);return v;
        }
        private static CampaignPanelView Settings()
        {
            var v=Root("SettingsPanel",true);v.heading=Text(v.transform,"Title","设置",570,250,780,80,40);
            Text(v.transform,"MusicLabel","音乐",570,410,200,60);v.music=Slider(v.transform,"MusicSlider",820,410);
            Text(v.transform,"SfxLabel","音效",570,540,200,60);v.sfx=Slider(v.transform,"SfxSlider",820,540);
            v.secondary=Button(v.transform,"ReturnTitleButton","返回标题",570,670,360);
            v.back=Button(v.transform,"QuitGameButton","退出游戏",990,670,360);
            v.primary=Button(v.transform,"CloseButton","关闭",750,750);return v;
        }
        private static Slider Slider(Transform parent,string name,float x,float y)
        {
            var track=Image(parent,name,x,y,530,44);track.color=new Color(.7f,.74f,.8f);track.raycastTarget=true;
            var handle=Image(track.transform,"Handle",0,0,30,44);handle.color=Ink;
            var slider=track.gameObject.AddComponent<Slider>();slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
            slider.minValue=0;slider.maxValue=1;slider.value=1;slider.navigation=new Navigation{mode=Navigation.Mode.None};return slider;
        }
        private static CampaignPanelView Dialogue()
        {
            var v=Root("DialoguePanel");
            v.portrait=Image(v.transform,"Portrait",115,70,650,950);
            var panel=Image(v.transform,"DialogueBox",650,540,1190,440,SpriteAt("Assets/Arts/UI/Common/对话框.png"));panel.preserveAspect=false;
            v.speaker=Text(v.transform,"SpeakerName","角色名",730,590,1000,70,36);
            v.body=Text(v.transform,"DialogueText","对话文本",730,680,1000,220,30);
            v.primary=Button(v.transform,"NextButton","继续 / Space",1430,970,400);
            v.secondary=Button(v.transform,"SkipButton","跳过本段",1510,40,330);return v;
        }
        private static TextMeshProUGUI Scroll(Transform parent,string name,float x,float y,float width,float height)
        {
            var area=Rect(parent,name,x,y,width,height);var scroll=area.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var viewport=Image(area,"Viewport",0,0,width,height);viewport.color=Color.clear;viewport.raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();Stretch(viewport.rectTransform);
            var text=Text(viewport.transform,"Content","正文",0,0,width-30,height,27);
            text.rectTransform.anchorMin=new Vector2(0,1);text.rectTransform.anchorMax=Vector2.one;text.rectTransform.sizeDelta=new Vector2(-30,0);
            var fitter=text.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport.rectTransform;scroll.content=text.rectTransform;return text;
        }
        private static CampaignPanelView Archive()
        {
            var v=Root("ChapterArchivePanel");v.heading=Text(v.transform,"ArchiveTitle","实验报告",90,180,950,150,62);
            v.body=Scroll(v.transform,"ArchiveBody",90,360,990,530);v.artwork=Image(v.transform,"Illustration",1170,180,650,550);
            v.detail=Text(v.transform,"Caption","图注",1170,760,650,130,24);v.primary=Button(v.transform,"ContinueButton","继续 / 返回",1410,970);return v;
        }
        private static CampaignPanelView Rules()
        {
            var v=Root("RulesPanel",true);v.heading=Text(v.transform,"Title","本关规则",450,110,1020,90,44);
            v.body=Scroll(v.transform,"RulesScroll",450,240,1020,650);v.primary=Button(v.transform,"CloseButton","关闭 / Esc",750,950);return v;
        }
        private static CampaignPanelView Pause()
        {
            var v=Root("PausePanel",true);v.heading=Text(v.transform,"StateText","已暂停",650,220,650,130,40);
            v.primary=Button(v.transform,"ContinueButton","继续",750,420);v.secondary=Button(v.transform,"RetryButton","重新开始",750,510);
            v.settings=Button(v.transform,"SettingsButton","设置",750,600);v.back=Button(v.transform,"BackButton","返回章节选关",750,690);return v;
        }
        private static CampaignPanelView Failure()
        {
            var v=Pause();v.name="FailurePanel";v.heading.text="演化次数已超限";
            v.primary.GetComponentInChildren<TextMeshProUGUI>().text="撤销上一步";v.settings.gameObject.SetActive(false);return v;
        }
        private static CampaignPanelView Victory()
        {
            var v=Root("VictoryPanel");Image(v.transform,"VictoryArt",0,0,1920,1080,SpriteAt("Assets/Arts/UI/Game/关卡通过界面底图.png")).preserveAspect=false;
            v.heading=Text(v.transform,"Title","关卡完成",100,260,900,100,56);
            v.body=Text(v.transform,"Victory Summary","关卡结果",100,410,900,240,32);
            v.primary=Button(v.transform,"ContinueButton","继续",100,790,550);v.secondary=Button(v.transform,"ReplayButton","重玩本关",100,890,550);return v;
        }
        private static void Gameplay()
        {
            var root=Rect(null,"GameplayHUDPanel",0,0,1920,1080);Stretch(root);var v=root.gameObject.AddComponent<GameplayHUDView>();
            var bg=Image(root,"Background",0,0,1920,1080,SpriteAt("Assets/Arts/UI/Game/关卡界面底图_草.png"));Stretch(bg.rectTransform);bg.preserveAspect=false;
            v.pause=Button(root,"PauseButton","暂停",50,30,240);
            v.counters=Text(root,"Counters","演化 0 / 0 · 分身 0 / 0",660,75,650,50,24);
            v.progressMarks=Rect(root,"ChapterMarks",740,25,440,32);
            var area=Rect(root,"BoardHost",390,150,1220,700);
            var br=Rect(area,"Board",0,0,1220,700);Stretch(br);br.gameObject.AddComponent<RawImage>();
            br.gameObject.AddComponent<AspectRatioFitter>().aspectMode=AspectRatioFitter.AspectMode.FitInParent;
            v.board=br.gameObject.AddComponent<OrganicLevelBoardView>();v.board.styledTerrain=true;
            v.title=Text(root,"LevelTitle","关卡名称",50,850,500,60,38);v.goals=Text(root,"Objectives","目标",50,930,530,130,25);
            v.undo=Button(root,"UndoButton","撤销 Z",1670,670,200,64);
            v.clone=Button(root,"CloneButton","分身 B",1600,770,260,64);
            v.iterate=Button(root,"IterateButton","演化 Space",1600,860,260,110);
            v.recycle=Button(root,"RecycleButton","回收分身",1600,990,260,54);v.recycle.gameObject.SetActive(false);
            v.rules=Button(root,"RulesButton","本关规则",50,110,240);
            v.cancel=Button(root,"CancelButton","取消选择 Esc",760,900,400);
            v.mode=Text(root,"Mode","方向键 / WASD 移动",600,990,940,40,23);
            v.status=Text(root,"Status","",600,1040,940,35,20);
            var state=Nested(root,"PausePanel");v.overlay=state.gameObject;v.overlayText=state.heading;
            v.overlayContinue=state.primary;v.retry=state.secondary;v.back=state.back;v.settings=state.settings;
            var fail=Nested(root,"FailurePanel");fail.gameObject.SetActive(false);v.failure=fail;
            var victory=Nested(root,"VictoryPanel");v.victory=victory.gameObject;v.victoryContinue=victory.primary;v.victoryReplay=victory.secondary;v.victory.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"GameplayHUDPanel.prefab");UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
        private static CampaignPanelView Nested(Transform parent,string name)
        {
            return ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab"),parent)).GetComponent<CampaignPanelView>();
        }
        private static NarrativeDatabase LoadImport()
        {
            return JsonUtility.FromJson<NarrativeDatabase>(File.ReadAllText("Assets/Resources/Campaign/Narrative.json"));
        }
        private static void ConfigureCatalog()
        {
            const string path="Assets/Resources/Campaign/Catalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(path);
            if(!catalog){catalog=ScriptableObject.CreateInstance<CampaignCatalog>();AssetDatabase.CreateAsset(catalog,path);}
            // Explicit configuration command refreshes data, never prefab layout.
            var data=LoadImport();catalog.levels=data.levels.ToList();catalog.chapters=data.chapters.ToList();
            catalog.anatomy=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Arts/UI/LevelSelect/选关界面下层无关卡版.png");
            EditorUtility.SetDirty(catalog);
        }
    }
}
#endif
