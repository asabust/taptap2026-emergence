using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OrganicGrid.Editor
{
    public static class OrganicGridSetup
    {
        [MenuItem("Tools/Cell Fusion/Create Organic Grid Scene")]
        public static void CreateScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                {
                    Debug.LogWarning("Save your untitled scene before creating the Organic Grid demo.");
                    return;
                }
            Shader shader = Shader.Find("CellFusion/OrganicGrid");
            if (!shader) throw new InvalidOperationException("OrganicGrid shader missing.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Organic Grid Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.transform.position = new Vector3(-1.9f,0,-10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.92f,0.95f,0.92f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Organic Grid Board";
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.localScale = new Vector3(8,8,1);
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath("Assets/OrganicGridDemo/OrganicGrid.mat"));
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            quad.AddComponent<OrganicGridDemo>();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/OrganicGridDemo/OrganicGridDemo.unity");
            EditorSceneManager.SaveScene(scene,path);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = quad;
            Debug.Log("Organic Grid scene saved to " + path);
        }
        public static void ValidateBatch()
        {
            ValidateRules();
            var baseline = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.SaveScene(baseline,"Assets/OrganicGridDemo/ValidationBase.unity");
            CreateScene();
            var demo = UnityEngine.Object.FindObjectOfType<OrganicGridDemo>();
            demo.SendMessage("OnEnable");
            demo.SendMessage("Update");
            Camera camera = Camera.main;
            var rt = new RenderTexture(1280,720,24);
            camera.targetTexture = rt;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(1280,720,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1280,720),0,0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath,"../OrganicGridPreview.png"),texture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(texture);
            foreach (var item in ShaderUtil.GetShaderMessages(Shader.Find("CellFusion/OrganicGrid")))
                if (item.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    throw new InvalidOperationException(item.message);
            Debug.Log("ORGANIC_GRID_VALIDATION_OK");
        }
        private static void Require(bool condition,string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void ValidateRules()
        {
            var board = new bool[49];
            board[GridLife.Index(2,4)] = board[GridLife.Index(3,4)] = true;
            board[GridLife.Index(2,5)] = board[GridLife.Index(3,5)] = true;
            var distant = new Vector2Int(5,2);
            var stable = GridLife.Next(board,distant,true);
            for (int i = 0; i < 49; i++) Require(stable[i] == board[i],"Stable block changed without intervention.");
            var helper = new Vector2Int(4,2);
            Require(GridLife.Neighbours(board,helper,3,3)==3,"Player did not supply third neighbour.");
            var next = GridLife.Next(board,helper,true);
            Require(next[GridLife.Index(3,3)],"Target should be born.");
            Require(!next[GridLife.Index(helper.x,helper.y)],"Player square must stay empty.");
            Require(!GridLife.Next(board,helper,false)[GridLife.Index(3,3)],"Birth toggle ignored.");
            Require(!board[GridLife.Index(3,3)],"Evolution mutated old board.");
            var blinker = new bool[49];
            blinker[GridLife.Index(2,3)] = blinker[GridLife.Index(3,3)] = blinker[GridLife.Index(4,3)] = true;
            var rotated = GridLife.Next(blinker,new Vector2Int(0,0),true);
            Require(rotated[GridLife.Index(3,2)] && rotated[GridLife.Index(3,3)] && rotated[GridLife.Index(3,4)] && !rotated[GridLife.Index(2,3)] && !rotated[GridLife.Index(4,3)],"Synchronous blinker evolution failed.");
            Require(GridLife.Neighbours(board,new Vector2Int(2,4),3,4)==3,"Player overlap counted twice.");
            Debug.Log("ORGANIC_GRID_RULES_OK");
        }
    }
}
