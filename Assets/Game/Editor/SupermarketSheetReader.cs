#if UNITY_EDITOR
using System;
using System.IO;

namespace Game.EditorTools
{
    /// <summary>自动识别 TXT / CSV / XLSX 并读取配表。</summary>
    public static class SupermarketSheetReader
    {
        public static SupermarketSheetTable Read(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("表格路径为空", nameof(assetPath));

            var fullPath = ResolveFullPath(assetPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("找不到表格文件", fullPath);

            var extension = Path.GetExtension(fullPath);
            if (string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
                return SimpleTabTextReader.Read(fullPath);

            var bytes = File.ReadAllBytes(fullPath);
            if (IsZipArchive(bytes))
                return SimpleXlsxReader.ReadFirstSheet(assetPath);

            return SimpleCsvReader.Read(fullPath);
        }

        public static bool IsZipArchive(byte[] bytes) =>
            bytes != null
            && bytes.Length >= 2
            && bytes[0] == 0x50
            && bytes[1] == 0x4B;

        static string ResolveFullPath(string assetPath)
        {
            if (Path.IsPathRooted(assetPath))
                return assetPath;

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
        }
    }
}
#endif
