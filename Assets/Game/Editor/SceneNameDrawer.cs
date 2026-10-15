using Game.Runtime.Core.Attributes;
using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(SceneNameAttribute))]
public class SceneNameDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 只处理 string 类型
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Use [SceneName] with string.");
            return;
        }

        // 获取 Build Settings 中的所有场景
        var scenes = EditorBuildSettings.scenes;
        string[] sceneNames = new string[scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
        {
            string path = scenes[i].path;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            sceneNames[i] = name;
        }

        // 找到当前选中项的索引
        int currentIndex = Mathf.Max(0, System.Array.IndexOf(sceneNames, property.stringValue));

        // 绘制下拉菜单
        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, sceneNames);

        // 如果用户选择了新的场景
        if (newIndex >= 0 && newIndex < sceneNames.Length)
        {
            property.stringValue = sceneNames[newIndex];
        }
    }
}