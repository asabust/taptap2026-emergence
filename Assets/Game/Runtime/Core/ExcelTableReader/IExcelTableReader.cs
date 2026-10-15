using System.Data;

namespace Game.Runtime.Core.ExcelTableReader
{
    public interface IExcelTableReader
    {
        void Read(DataTable table, ExcelTableContext context);
    }
}