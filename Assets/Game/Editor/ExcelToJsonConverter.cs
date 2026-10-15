#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Game.Runtime.Core.ExcelTableReader;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class ExcelToJsonConverter
    {
        [MenuItem("Tools/Export Database To JSON")]
        public static void ConvertDialogueExcel()
        {
            string excelPath = Application.dataPath + "/GameData/database.xlsx";
            Debug.Log($"开始转换excel: {excelPath}");

            ExcelTableContext data = ExcelReader.ReadAll(excelPath);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);

            string outputPath = Application.dataPath + "/Resources/GameData/database.json";
            File.WriteAllText(outputPath, json);
            Debug.Log($"成功导出到: {outputPath}");

            // 刷新编辑器资源
            AssetDatabase.Refresh();
        }
    }
}
#endif