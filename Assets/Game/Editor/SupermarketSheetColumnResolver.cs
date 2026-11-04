#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game.EditorTools
{
    public static class SupermarketSheetColumnResolver
    {
        public delegate bool CellMatcher(string value);

        public static bool TryResolve(
            SupermarketSheetTable table,
            string nameHeader,
            string shelfHeader,
            CellMatcher looksLikeShelfId,
            Func<string, bool> isNameHeader,
            Func<string, bool> isShelfHeader,
            out int nameColumn,
            out int shelfColumn,
            out int dataStartRow,
            out string error)
        {
            nameColumn = -1;
            shelfColumn = -1;
            dataStartRow = 0;
            error = null;

            if (table == null || table.RowCount == 0)
            {
                error = "表格为空。";
                return false;
            }

            var maxColumns = GetMaxColumnCount(table);
            if (maxColumns < 2)
            {
                error = $"表格列数不足：检测到 {maxColumns} 列、{table.RowCount} 行。";
                return false;
            }

            if (TryResolveByHeader(table, isNameHeader, isShelfHeader, looksLikeShelfId,
                    out nameColumn, out shelfColumn, out dataStartRow))
                return true;

            if (TryResolveByContent(table, looksLikeShelfId, out nameColumn, out shelfColumn, out dataStartRow, out error))
                return true;

            error = BuildResolveFailureMessage(table, maxColumns, error);
            return false;
        }

        static bool TryResolveByHeader(
            SupermarketSheetTable table,
            Func<string, bool> isNameHeader,
            Func<string, bool> isShelfHeader,
            CellMatcher looksLikeShelfId,
            out int nameColumn,
            out int shelfColumn,
            out int dataStartRow)
        {
            nameColumn = -1;
            shelfColumn = -1;
            dataStartRow = 0;

            var maxScan = Math.Min(5, table.RowCount);
            for (var rowIndex = 0; rowIndex < maxScan; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                var candidateName = -1;
                var candidateShelf = -1;

                for (var col = 0; col < row.Count; col++)
                {
                    var cell = NormalizeCell(row[col]);
                    if (string.IsNullOrEmpty(cell))
                        continue;

                    if (isNameHeader(cell))
                        candidateName = col;
                    else if (isShelfHeader(cell))
                        candidateShelf = col;
                }

                if (candidateName < 0 || candidateShelf < 0 || candidateName == candidateShelf)
                    continue;

                if (!HasShelfData(table, candidateShelf, rowIndex + 1, looksLikeShelfId))
                    continue;

                nameColumn = candidateName;
                shelfColumn = candidateShelf;
                dataStartRow = rowIndex + 1;
                return true;
            }

            return false;
        }

        static bool HasShelfData(
            SupermarketSheetTable table,
            int shelfColumn,
            int startRow,
            CellMatcher looksLikeShelfId)
        {
            var endRow = Math.Min(startRow + 8, table.RowCount);
            for (var rowIndex = startRow; rowIndex < endRow; rowIndex++)
            {
                if (!table.TryGetCell(rowIndex, shelfColumn, out var value))
                    continue;

                if (looksLikeShelfId(NormalizeCell(value)))
                    return true;
            }

            return false;
        }

        static bool TryResolveByContent(
            SupermarketSheetTable table,
            CellMatcher looksLikeShelfId,
            out int nameColumn,
            out int shelfColumn,
            out int dataStartRow,
            out string error)
        {
            nameColumn = -1;
            shelfColumn = -1;
            dataStartRow = 0;
            error = null;

            var maxColumns = GetMaxColumnCount(table);
            var shelfScores = new int[maxColumns];
            for (var rowIndex = 0; rowIndex < table.RowCount; rowIndex++)
            {
                for (var col = 0; col < maxColumns; col++)
                {
                    if (!table.TryGetCell(rowIndex, col, out var value))
                        continue;

                    if (looksLikeShelfId(NormalizeCell(value)))
                        shelfScores[col]++;
                }
            }

            shelfColumn = -1;
            var bestShelfScore = 0;
            for (var col = 0; col < maxColumns; col++)
            {
                if (shelfScores[col] <= bestShelfScore)
                    continue;

                bestShelfScore = shelfScores[col];
                shelfColumn = col;
            }

            if (shelfColumn < 0 || bestShelfScore <= 0)
            {
                error = $"未检测到货架列（形如 1-A）；共 {table.RowCount} 行 × {maxColumns} 列。";
                return false;
            }

            nameColumn = -1;
            for (var col = 0; col < maxColumns; col++)
            {
                if (col == shelfColumn)
                    continue;

                nameColumn = col;
                break;
            }

            if (nameColumn < 0)
            {
                error = "未检测到名称列。";
                return false;
            }

            dataStartRow = DetectDataStartRow(table, nameColumn, shelfColumn, looksLikeShelfId);
            return true;
        }

        static int DetectDataStartRow(
            SupermarketSheetTable table,
            int nameColumn,
            int shelfColumn,
            CellMatcher looksLikeShelfId)
        {
            for (var rowIndex = 0; rowIndex < table.RowCount; rowIndex++)
            {
                table.TryGetCell(rowIndex, shelfColumn, out var shelf);
                shelf = NormalizeCell(shelf);
                if (looksLikeShelfId(shelf))
                    return rowIndex;
            }

            return 0;
        }

        static int GetMaxColumnCount(SupermarketSheetTable table)
        {
            var maxColumns = 0;
            for (var i = 0; i < table.RowCount; i++)
                maxColumns = Math.Max(maxColumns, table.Rows[i].Count);

            return maxColumns;
        }

        public static string NormalizeCell(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value = value.Trim();
            if (value.Length > 0 && value[0] == '\ufeff')
                value = value.Substring(1);

            return NormalizeShelfToken(value.Trim());
        }

        public static string NormalizeShelfToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ch >= '０' && ch <= '９')
                    builder.Append((char)('0' + (ch - '０')));
                else if (ch == '－' || ch == '—' || ch == '–' || ch == '﹣' || ch == '−')
                    builder.Append('-');
                else if (ch >= 'ａ' && ch <= 'ｚ')
                    builder.Append((char)('a' + (ch - 'ａ')));
                else if (ch >= 'Ａ' && ch <= 'Ｚ')
                    builder.Append((char)('A' + (ch - 'Ａ')));
                else
                    builder.Append(ch);
            }

            return builder.ToString().Trim();
        }

        static string BuildResolveFailureMessage(SupermarketSheetTable table, int maxColumns, string error)
        {
            var sample = new List<string>();
            for (var rowIndex = 0; rowIndex < table.RowCount && sample.Count < 3; rowIndex++)
            {
                var parts = new List<string>();
                for (var col = 0; col < maxColumns; col++)
                {
                    table.TryGetCell(rowIndex, col, out var value);
                    parts.Add($"\"{NormalizeCell(value)}\"");
                }

                if (parts.Count > 0)
                    sample.Add($"第{rowIndex + 1}行: {string.Join(", ", parts)}");
            }

            var sampleText = sample.Count > 0 ? $"\n样例：{string.Join("；", sample)}" : string.Empty;
            return (error ?? $"无法识别名称列与货架列（{table.RowCount} 行 × {maxColumns} 列）。") + sampleText;
        }
    }
}
#endif
