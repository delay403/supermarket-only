#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game.EditorTools
{
    public static class SimpleCsvReader
    {
        public static SupermarketSheetTable Read(string path)
        {
            var fullPath = ResolveFullPath(path);
            var bytes = File.ReadAllBytes(fullPath);
            var text = DecodeText(bytes);
            var delimiter = DetectDelimiter(text);
            var table = new SupermarketSheetTable();

            using var reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                table.Rows.Add(ParseLine(line, delimiter));
            }

            NormalizeSparseRows(table);
            return table;
        }

        static string DecodeText(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes);

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes);

            var utf8 = Encoding.UTF8.GetString(bytes);
            if (!utf8.Contains('\uFFFD'))
                return utf8;

            return Encoding.GetEncoding(936).GetString(bytes);
        }

        static char DetectDelimiter(string text)
        {
            var firstLine = string.Empty;
            using (var reader = new StringReader(text))
                firstLine = reader.ReadLine() ?? string.Empty;

            var comma = CountUnquoted(firstLine, ',');
            var tab = CountUnquoted(firstLine, '\t');
            var semicolon = CountUnquoted(firstLine, ';');

            if (tab >= comma && tab >= semicolon && tab > 0)
                return '\t';

            if (semicolon > comma && semicolon > 0)
                return ';';

            return ',';
        }

        static int CountUnquoted(string line, char delimiter)
        {
            var count = 0;
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    quoted = !quoted;
                    continue;
                }

                if (!quoted && ch == delimiter)
                    count++;
            }

            return count;
        }

        static List<string> ParseLine(string line, char delimiter)
        {
            var cells = new List<string>();
            var builder = new StringBuilder();
            var quoted = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                        continue;
                    }

                    quoted = !quoted;
                    continue;
                }

                if (ch == delimiter && !quoted)
                {
                    cells.Add(builder.ToString().Trim());
                    builder.Clear();
                    continue;
                }

                builder.Append(ch);
            }

            cells.Add(builder.ToString().Trim());
            return cells;
        }

        static string ResolveFullPath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        static void NormalizeSparseRows(SupermarketSheetTable table)
        {
            var maxColumns = 0;
            for (var i = 0; i < table.Rows.Count; i++)
                maxColumns = Math.Max(maxColumns, table.Rows[i].Count);

            for (var i = 0; i < table.Rows.Count; i++)
            {
                var row = table.Rows[i];
                while (row.Count < maxColumns)
                    row.Add(string.Empty);
            }
        }
    }
}
#endif
