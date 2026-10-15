using System;

namespace Game.Runtime.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ExcelSheetAttribute : Attribute
    {
        public readonly string sheetName;

        public ExcelSheetAttribute(string sheetName)
        {
            this.sheetName = sheetName;
        }
    }
}