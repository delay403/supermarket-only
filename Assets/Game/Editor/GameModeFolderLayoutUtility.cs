#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Core;
using Game.Core.Content;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 保证每个玩法模式目录拥有统一的 Resources / Content / Art / Audio / Scenes 槽位。
    /// Unity 不跟踪空文件夹，因此空槽位会写入 .gitkeep 占位。
    /// </summary>
    public static class GameModeFolderLayoutUtility
    {
        const string PlaceholderFileName = ".gitkeep";

        [MenuItem("Game/Content/Ensure Standard Mode Folder Layout")]
        public static void EnsureStandardModeFolderLayoutMenu()
        {
            var report = EnsureStandardModeFolderLayout(log: true);
            EditorUtility.DisplayDialog("模式目录结构", report, "确定");
        }

        public static string EnsureStandardModeFolderLayout(bool log)
        {
            var created = 0;
            var sb = new StringBuilder();
            sb.AppendLine("=== Standard Mode Folder Layout ===");
            sb.AppendLine("每个模式统一包含：Resources / Content / Art / Audio / Scenes");
            sb.AppendLine();

            foreach (var module in GameContentPaths.EnumerateGameplayModules())
            {
                var modeName = GameContentPaths.ModuleFolderName(module);
                sb.AppendLine($"[{modeName}]");

                for (var i = 0; i < GameContentPaths.ModeStandardSlots.Length; i++)
                {
                    var slot = GameContentPaths.ModeStandardSlots[i];
                    var folder = GameContentPaths.ModeSlotRoot(module, slot);
                    if (EnsureSlotFolder(folder, ref created))
                        sb.AppendLine($"  + {folder}");
                }

                sb.AppendLine();
            }

            AssetDatabase.Refresh();
            sb.AppendLine($"新建 {created} 个槽位目录。");
            if (log)
                Debug.Log(sb.ToString());

            return sb.ToString();
        }

        static bool EnsureSlotFolder(string folderPath, ref int created)
        {
            var wasNew = !AssetDatabase.IsValidFolder(folderPath);
            EnsureFolder(folderPath);

            var placeholder = $"{folderPath}/{PlaceholderFileName}";
            if (!File.Exists(placeholder))
            {
                File.WriteAllText(placeholder, string.Empty);
                created++;
                wasNew = true;
            }

            return wasNew;
        }

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var name = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
