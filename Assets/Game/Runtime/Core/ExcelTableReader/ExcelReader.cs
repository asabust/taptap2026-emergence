using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ExcelDataReader;
using Game.Runtime.Core.Attributes;
using UnityEngine;

namespace Game.Runtime.Core.ExcelTableReader
{
    /// <summary>
    ///     读取Excel文件
    /// </summary>
    public static class ExcelReader
    {
        public static ExcelTableContext ReadAll(string excelPath)
        {
            var context = new ExcelTableContext();

            Encoding.RegisterProvider(
                CodePagesEncodingProvider.Instance);

            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var dataSet = reader.AsDataSet();

                var readers = CreateAllReaders();

                foreach (DataTable table in dataSet.Tables)
                    if (readers.TryGetValue(table.TableName, out var tableReader))
                        tableReader.Read(table, context);
                    else
                        Debug.LogWarning($"No reader for sheet: {table.TableName}");
            }

            return context;
        }

        private static Dictionary<string, IExcelTableReader> CreateAllReaders()
        {
            var dict = new Dictionary<string, IExcelTableReader>();

            // 从 Game.Runtime.Core 程序集里找表
            var types = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t =>
                    typeof(IExcelTableReader).IsAssignableFrom(t) &&
                    !t.IsInterface &&
                    !t.IsAbstract);

            foreach (var type in types)
            {
                // 从属性中获取读表的名字
                var attr = type.GetCustomAttribute<ExcelSheetAttribute>();
                if (attr == null)
                    continue;

                var reader = (IExcelTableReader)Activator.CreateInstance(type);
                dict[attr.sheetName] = reader;
            }

            return dict;
        }
    }
}