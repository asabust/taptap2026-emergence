#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Game.Runtime.Gameplay.Levels;
using Game.Runtime.Data.Campaign;
using Game.Runtime.Data.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class FullGameFlowSetup
    {
        [MenuItem("Tools/Levels/Refresh Full Game Artwork")]
        public static void RefreshArtwork()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("请先退出 Play Mode。"); return; }
            Directory.CreateDirectory("Assets/Resources/Campaign");
            var art = AssetDatabase.LoadAssetAtPath<CampaignArt>("Assets/Resources/Campaign/Art.asset");
            if (!art) { art = ScriptableObject.CreateInstance<CampaignArt>(); AssetDatabase.CreateAsset(art,"Assets/Resources/Campaign/Art.asset"); }
            art.title = Load("Title/第一界面底图_草.png");
            art.titleLogo = Load("Title/格命标题_透明.png");
            var logoImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(art.titleLogo));
            if (logoImporter.textureType != TextureImporterType.Sprite || !logoImporter.alphaIsTransparency || logoImporter.mipmapEnabled)
            {
                logoImporter.textureType = TextureImporterType.Sprite;
                logoImporter.alphaIsTransparency = true;
                logoImporter.mipmapEnabled = false;
                logoImporter.SaveAndReimport();
            }
            art.background = Load("Common/通用背景.png");
            art.back = Load("Common/返回图标.png");
            art.selector = Load("Common/选关界面选关器图标_草.png");
            art.anatomy = Load("LevelSelect/选关界面下层无关卡版.png");
            art.skeleton = Load("LevelSelect/选关界面上层线稿骨骼图.png");
            art.scanTexture = Load("LevelSelect/选关界面纹理底图（透明底直接叠上面）.png");
            art.victory = Load("Game/关卡通过界面底图.png");
            EditorUtility.SetDirty(art);
            var play = AssetDatabase.LoadAssetAtPath<LevelPlayArt>("Assets/Resources/LevelPlay/Presentation.asset");
            if (!play) { play = ScriptableObject.CreateInstance<LevelPlayArt>(); AssetDatabase.CreateAsset(play,"Assets/Resources/LevelPlay/Presentation.asset"); }
            play.background = Load("Game/关卡界面底图_草.png");
            play.evolution = Load("Game/关卡界面衍生图标.png");
            play.clone = Load("Game/关卡界面分裂图标.png");
            play.undo = Load("Game/关卡界面回溯图标.png");
            play.rules = Load("Game/关卡界面规则图标.png");
            play.pause = Load("Common/返回图标.png");
            play.victory = art.victory;
            EditorUtility.SetDirty(play);
            AssetDatabase.SaveAssets();
            CampaignSceneSetup.UseGameBuildScenes();
            Debug.Log("PASS: full game artwork refreshed; Persistent is the first game build scene. Catalog order and authored scenes preserved.");
        }
        [MenuItem("Tools/Levels/Configure Draft Campaign Map")]
        public static void ConfigureDraftMap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("请先退出 Play Mode。"); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>("Assets/Resources/Campaign/Catalog.asset");
            if (!catalog) throw new FileNotFoundException("Campaign catalog is missing.");
            var art = AssetDatabase.LoadAssetAtPath<CampaignArt>("Assets/Resources/Campaign/Art.asset");
            string[] names = { "足部 I", "足部 II", "小腿 I", "大腿 I", "小腿 II", "大腿 II", "手部 I", "腕部 I", "前臂 I", "上臂 I", "手部 II", "腕部 II", "前臂 II", "上臂 II", "骨盆", "腰椎", "胸椎", "颈部", "头部" };
            Vector2[] positions = { new Vector2(.45f,.11f),new Vector2(.56f,.11f),new Vector2(.43f,.26f),new Vector2(.40f,.44f),new Vector2(.56f,.28f),new Vector2(.59f,.46f),new Vector2(.23f,.48f),new Vector2(.23f,.54f),new Vector2(.27f,.62f),new Vector2(.30f,.73f),new Vector2(.83f,.50f),new Vector2(.82f,.56f),new Vector2(.77f,.64f),new Vector2(.74f,.77f),new Vector2(.52f,.56f),new Vector2(.52f,.64f),new Vector2(.52f,.76f),new Vector2(.53f,.85f),new Vector2(.53f,.94f) };
            string[] ids = { "001","002","101","102","103","104","201","202","203","204" };
            // Validate the complete replacement before touching the authored catalog.
            var levels = ids.Select(id => LevelRepository.DeveloperDefault().Load(id)).ToArray();
            foreach (var level in levels)
                if (!LevelValidator.ValidateForPlay(level).CanPlay) throw new System.InvalidOperationException("Invalid published level: " + level.id);
            catalog.locations.Clear(); catalog.levels.Clear();
            for (int i = 0; i < names.Length; i++) catalog.locations.Add(new CampaignLocation { label=names[i],position=positions[i] });
            for (int i = 0; i < levels.Length; i++)
                catalog.levels.Add(new CampaignLevel { id=ids[i],title=levels[i].name,bodyPart=names[i],bodyPosition=positions[i],description=names[i]+" / "+levels[i].name+"\n观察细胞的演化，抵达本关目标。" });
            if (art) catalog.anatomy=art.anatomy;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log("PASS: 10 published levels configured on draft nodes 01–10; nodes 11–19 reserved for later chapters.");
        }
        private static Texture2D Load(string path)
        {
            var value = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Arts/UI/" + path);
            if (!value) throw new FileNotFoundException("Missing game artwork: " + path);
            return value;
        }
    }
}
#endif
