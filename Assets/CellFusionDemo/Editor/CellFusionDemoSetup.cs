using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CellFusion.Editor
{
    public static class CellFusionDemoSetup
    {
        public static void ValidateBatch()
        {
            var baseline = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(baseline, "Assets/CellFusionDemo/ValidationBase.unity");
            CreateDemoScene();
            var shader = Shader.Find("CellFusion/SoftCells");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    throw new InvalidOperationException(message.message);
            }
            if (!shader.isSupported) throw new InvalidOperationException("Shader unsupported on this graphics device.");
            Debug.Log("CELL_FUSION_VALIDATION_OK");
        }

        [MenuItem("Tools/Cell Fusion/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                {
                    Debug.LogWarning("Save your untitled scene first, then use Tools > Cell Fusion > Create Demo Scene.");
                    return;
                }
            }
            var shader = Shader.Find("CellFusion/SoftCells");
            if (!shader) throw new InvalidOperationException("CellFusion shader has not imported yet.");
            // Additive creation leaves the user's existing scene intact.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var cameraObject = new GameObject("Cell Fusion Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.15f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.91f, 0.94f, 0.91f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30;
            string materialPath = AssetDatabase.GenerateUniqueAssetPath("Assets/CellFusionDemo/SoftCells.mat");
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
            CreatePanel("Translucent Cells", -1.85f, CellFusionDemo.Look.Translucent, material);
            CreatePanel("White Color Rim Cells", 1.85f, CellFusionDemo.Look.WhiteWithColorRim, material);
            string scenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/CellFusionDemo/CellFusionDemo.unity");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = scene.GetRootGameObjects()[1];
            Debug.Log("Cell Fusion demo saved: " + scenePath + ". Open it on its own and press Play.");
        }

        private static void CreatePanel(string name, float x, CellFusionDemo.Look look, Material material)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = name;
            UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.position = new Vector3(x, 0, 0);
            panel.transform.localScale = new Vector3(3.5f, 3.5f, 1);
            panel.GetComponent<MeshRenderer>().sharedMaterial = material;
            panel.AddComponent<CellFusionDemo>().look = look;
        }
    }
}
