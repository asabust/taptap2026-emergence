using System.IO;
using Game.Runtime.Gameplay.LevelPlay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class LevelPlaySceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Gameplay.unity";
        [MenuItem("Tools/Levels/Create Gameplay Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("请先保存草稿并退出 Play Mode。"); return; }
            if (File.Exists(ScenePath))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                Debug.LogWarning("Gameplay 场景已存在，未覆盖：" + ScenePath); return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Level Play");
            var screen = root.AddComponent<LevelPlayScreen>();
            screen.uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Arts/Font/LXGWNeoXiHei SD Dynamic.asset");
            if (!screen.uiFont) screen.uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Arts/Font/LXGWNeoXiHei SDF.asset");
            if (!screen.uiFont) screen.uiFont = TMP_Settings.defaultFontAsset;
            var camera = new GameObject("Play Background Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .08f, .13f);
            Directory.CreateDirectory("Assets/Scenes"); AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            { Debug.LogError("Gameplay 保存失败，请手动保存当前场景。"); return; }
            Selection.activeGameObject = root;
            Debug.Log("Gameplay 测试场景已创建：" + ScenePath + "。Inspector 设置 Level Id，默认 002；点击 Play 测试。不会写入关卡或正式进度。");
        }
    }
}
