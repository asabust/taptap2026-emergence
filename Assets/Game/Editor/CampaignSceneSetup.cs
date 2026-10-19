using System.IO;
using System.Linq;
using Game.Runtime.Data.Campaign;
using Game.Runtime.Gameplay.Campaign;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class CampaignSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/LevelSelect.unity";
        [MenuItem("Tools/Levels/Configure Stage 7 Game Flow")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("请先保存草稿并退出 Play Mode。"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Resources/Campaign"); AssetDatabase.Refresh();
            const string catalogPath = "Assets/Resources/Campaign/Catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(catalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<CampaignCatalog>();
                catalog.anatomy = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Arts/UI/选关界面底图上层_草.png");
                catalog.levels.Add(new CampaignLevel { id = "stage6_probe", title = "操作验证", bodyPosition = new Vector2(.31f, .77f),
                    description = "流程验证用关卡，后续可替换为正式第一关。\n先在玩家右下方放置分身，再迭代清除通道，最后移动到终点。" });
                catalog.levels.Add(new CampaignLevel { id = "001", title = "示例关卡1", bodyPosition = new Vector2(.34f, .56f), description = "策划关卡 001。观察细胞邻居，通过站位与迭代打开通路。" });
                catalog.levels.Add(new CampaignLevel { id = "002", title = "测试", bodyPosition = new Vector2(.29f, .34f), description = "策划关卡 002。使玩家到达本关终点。" });
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var screen = new GameObject("Level Selection").AddComponent<LevelSelectionScreen>();
                screen.uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Arts/Font/LXGWNeoXiHei SD Dynamic.asset");
                var camera = new GameObject("Selection Camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) { Debug.LogError("LevelSelect 保存失败。"); return; }
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            UseGameBuildScenes();
            Selection.activeObject = catalog;
            Debug.Log("阶段 7 配置完成。以 Persistent 场景启动，Title → 新游戏 → 人体选关 → 预览 → Gameplay。目录列表顺序控制解锁，bodyPosition 为人体图左下原点的归一化坐标；preview 可换正式彩图。已有场景和目录不覆盖。");
        }
        [MenuItem("Tools/Levels/Use Game Build Scenes")]
        public static void UseGameBuildScenes()
        {
            string[] paths = { "Assets/Scenes/Persistent.unity", "Assets/Scenes/TitleScene.unity", ScenePath, "Assets/Scenes/Gameplay.unity" };
            if (paths.Any(p => !File.Exists(p))) { Debug.LogWarning("缺少场景，请先 Configure Stage 7 Game Flow。"); return; }
            EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            EditorApplication.ExecuteMenuItem("File/Save Project");
        }
        [MenuItem("Tools/Levels/Use Editor Build Scenes")]
        public static void UseEditorBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(LevelEditorSceneSetup.ScenePath, true) };
            EditorApplication.ExecuteMenuItem("File/Save Project");
        }
    }
}
