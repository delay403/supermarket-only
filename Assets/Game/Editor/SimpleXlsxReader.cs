#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>轻量 xlsx 读取（选择行数最多的工作表），无第三方依赖。</summary>
    public static class SimpleXlsxReader
    {
        const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        public static SupermarketSheetTable ReadFirstSheet(string xlsxAssetPath)
        {
            if (string.IsNullOrWhiteSpace(xlsxAssetPath))
                throw new ArgumentException("xlsx 路径为空", nameof(xlsxAssetPath));

            var fullPath = ResolveFullPath(xlsxAssetPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("找不到 xlsx 文件", fullPath);

            using var archive = ZipFile.OpenRead(fullPath);
            var sharedStrings = ReadSharedStrings(archive);
            var sheetEntry = FindBestWorksheetEntry(archive)
                ?? throw new InvalidDataException("xlsx 中未找到工作表");

            using var stream = sheetEntry.Open();
            var document = new XmlDocument();
            document.Load(stream);
            return ParseSheet(document, sharedStrings);
        }

        static string ResolveFullPath(string assetPath)
        {
            if (Path.IsPathRooted(assetPath))
                return assetPath;

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
        }

        static ZipArchiveEntry FindBestWorksheetEntry(ZipArchive archive)
        {
            ZipArchiveEntry best = null;
            var bestScore = -1;

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                    || !entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    continue;

                var score = EstimateSheetRowCount(entry);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = entry;
                }
            }

            return best;
        }

        static int EstimateSheetRowCount(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            var document = new XmlDocument();
            document.Load(stream);
            var rows = document.GetElementsByTagName("row");
            return rows?.Count ?? 0;
        }

        static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var result = new List<string>();
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return result;

            using var stream = entry.Open();
            var document = new XmlDocument();
            document.Load(stream);

            var nodes = document.GetElementsByTagName("si");
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is not XmlElement si)
                    continue;

                result.Add(ReadTextFromSi(si));
            }

            return result;
        }

        static string ReadTextFromSi(XmlElement si)
        {
            var builder = new System.Text.StringBuilder();
            CollectTextNodes(si, builder);
            return builder.ToString();
        }

        static void CollectTextNodes(XmlElement element, System.Text.StringBuilder builder)
        {
            if (element.Name == "t" && element.ParentNode != null)
            {
                builder.Append(element.InnerText);
                return;
            }

            for (var i = 0; i < element.ChildNodes.Count; i++)
            {
                if (element.ChildNodes[i] is XmlElement child)
                    CollectTextNodes(child, builder);
            }
        }

        static SupermarketSheetTable ParseSheet(XmlDocument document, List<string> sharedStrings)
        {
            var table = new SupermarketSheetTable();
            var rows = document.GetElementsByTagName("row");
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i] is not XmlElement rowElement)
                    continue;

                var row = new List<string>();
                table.Rows.Add(row);

                var cells = rowElement.GetElementsByTagName("c");
                for (var c = 0; c < cells.Count; c++)
                {
                    if (cells[c] is not XmlElement cellElement)
                        continue;

                    var columnIndex = ParseColumnIndex(cellElement.GetAttribute("r"));
                    var value = ReadCellValue(cellElement, sharedStrings);
                    while (row.Count <= columnIndex)
                        row.Add(string.Empty);

                    row[columnIndex] = value;
                }
            }

            NormalizeSparseRows(table);
            return table;
        }

        static int ParseColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
                return 0;

            var columnLetters = 0;
            for (var i = 0; i < cellReference.Length; i++)
            {
                var ch = cellReference[i];
                if (ch >= 'A' && ch <= 'Z')
                    columnLetters = columnLetters * 26 + (ch - 'A' + 1);
                else if (ch >= 'a' && ch <= 'z')
                    columnLetters = columnLetters * 26 + (ch - 'a' + 1);
                else
                    break;
            }

            return columnLetters > 0 ? columnLetters - 1 : 0;
        }

        static string ReadCellValue(XmlElement cellElement, List<string> sharedStrings)
        {
            var cellType = cellElement.GetAttribute("t");
            if (cellType == "inlineStr")
            {
                var inline = cellElement.GetElementsByTagName("t");
                if (inline.Count > 0)
                    return inline[0]?.InnerText ?? string.Empty;

                return string.Empty;
            }

            var valueNodes = cellElement.GetElementsByTagName("v");
            if (valueNodes.Count == 0)
                return string.Empty;

            var raw = valueNodes[0]?.InnerText ?? string.Empty;
            if (cellType == "s" && int.TryParse(raw, out var sharedIndex)
                && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                return sharedStrings[sharedIndex];

            return raw;
        }

        static void NormalizeSparseRows(SupermarketSheetTable table)
        {
            var maxColumns = 0;
            for (var i = 0; i < table.RowCount; i++)
                maxColumns = Mathf.Max(maxColumns, table.Rows[i].Count);

            for (var i = 0; i < table.RowCount; i++)
            {
                var row = table.Rows[i];
                while (row.Count < maxColumns)
                    row.Add(string.Empty);
            }
        }
    }
}
#endif
