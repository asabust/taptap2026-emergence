using System.Collections.Generic;
using Game.Runtime.Core.ExcelTableReader;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Runtime.Core
{
    // 一次性序列化/反序列化

    /// <summary>
    /// 读取数据表
    /// Web平台等对IO有限制，不能直接读取excel文件，改用读取Json
    /// 需要用到 Newtonsoft.Json
    /// 打开 Package Manager
    /// 点击加号选择 Add package from git url
    /// 添加地址：com.unity.nuget.newtonsoft-json
    /// </summary>
    public class DataLoader : Singleton<DataLoader>
    {
        /// <summary>
        /// 所有表格数据包
        /// </summary>
        public ExcelTableContext gameData;

        protected override void Awake()
        {
            base.Awake();
            TextAsset jsonText = Resources.Load<TextAsset>("GameData/database");
            if (jsonText == null)
            {
                Debug.LogError("Cannot find database.json in Resources!");
                return;
            }

            gameData = JsonConvert.DeserializeObject<ExcelTableContext>(jsonText.text);
        }
    }
}