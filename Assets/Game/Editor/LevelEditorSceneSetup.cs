using System.IO;
using Game.Runtime.Gameplay.LevelEditor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class LevelEditorSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/LevelEditor.unity";

        [MenuItem("Tools/Levels/Create Level Editor Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("请退出 Play Mode 后创建场景。"); return; }
            if (File.Exists(ScenePath))
            {
                Debug.LogWarning("LevelEditor 场景已存在，未覆盖：" + ScenePath + "。请在 Project 中双击打开。");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Level Editor");
            var screen = root.AddComponent<LevelEditorScreen>();
            screen.uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Arts/Font/LXGWNeoXiHei SDF.asset");
            if (!screen.uiFont) screen.uiFont = TMP_Settings.defaultFontAsset;
            var camera = new GameObject("Editor Background Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .08f, .13f);
            camera.orthographic = true;
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            { Debug.LogError("场景保存失败，未保存的场景仍保留，请手动保存。"); return; }
            Selection.activeGameObject = root;
            Debug.Log("LevelEditor 场景已创建：" + ScenePath + "。点击 Play 开始编辑；界面会在运行时构建。已支持配置、保存 JSON 和左下方“试玩当前草稿”，编辑与试玩保持同一场景。");
        }
    }
}
