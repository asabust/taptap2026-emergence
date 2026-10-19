using System.IO;
using Game.Runtime.Data.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class LevelPlayPresentationSetup
    {
        [MenuItem("Tools/Levels/Capture Game Preview (Play Mode)")]
        public static void CapturePreview()
        {
            if (!EditorApplication.isPlaying || !Object.FindObjectOfType<Game.Runtime.Gameplay.LevelPlay.LevelPlayScreen>())
            { Debug.LogWarning("请先在 Play Mode 打开一个游戏关卡。"); return; }
            string path = Path.Combine(Path.GetTempPath(), "taptap-gameplay-preview.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("Game preview requested: " + path);
        }
        [MenuItem("Tools/Levels/Configure Game Presentation")]
        public static void Configure()
        {
            const string path = "Assets/Resources/LevelPlay/Presentation.asset";
            var art = AssetDatabase.LoadAssetAtPath<LevelPlayArt>(path);
            if (!art)
            {
                art = ScriptableObject.CreateInstance<LevelPlayArt>();
                art.background = Texture("关卡界面底图_草.png");
                art.evolution = Texture("关卡界面衍化图标_草.png");
                art.undo = Texture("关卡界面回退图标_草.png");
                art.pause = Texture("关卡界面返回图标_草.png");
                art.rules = Texture("关卡界面规则图标.png");
                Directory.CreateDirectory("Assets/Resources/LevelPlay");
                AssetDatabase.CreateAsset(art, path); AssetDatabase.SaveAssets();
            }
            Selection.activeObject = art;
            Debug.Log("Game presentation configured. Gameplay uses artwork and flowing shaped terrain; editor playtesting keeps its diagnostic HUD. Existing art assets are referenced without modification.");
        }
        private static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Arts/UI/" + name);
    }
}
