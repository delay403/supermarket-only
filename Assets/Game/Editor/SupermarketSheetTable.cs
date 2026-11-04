#if UNITY_EDITOR
using System.Collections.Generic;

namespace Game.EditorTools
{
    /// <summary>超市配表通用表格结构（CSV / XLSX 共用）。</summary>
    public sealed class SupermarketSheetTable
    {
        public readonly List<List<string>> Rows = new List<List<string>>();

        public int RowCount => Rows.Count;

        public bool TryGetCell(int rowIndex, int columnIndex, out string value)
        {
            value = string.Empty;
            if (rowIndex < 0 || rowIndex >= Rows.Count)
                return false;

            var row = Rows[rowIndex];
            if (columnIndex < 0 || columnIndex >= row.Count)
                return false;

            value = row[columnIndex] ?? string.Empty;
            return true;
        }
    }
}
#endif
