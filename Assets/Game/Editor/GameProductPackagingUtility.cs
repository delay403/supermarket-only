#if UNITY_EDITOR
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 统一打包面板的本地资源配置写入：游戏模式勾选与 Build Settings 同步。
    /// </summary>
    public static class GameProductPackagingUtility
    {
        public const string ProductSettingsPath = "Assets/Game/Core/Resources/GameProductSettings.asset";

        public static GameProductSettings LoadProductSettings() =>
            GameProductFusionBuildTools.LoadProductSettingsAsset();

        public static void ApplyModeEnabledFlags(GameProductSettings product, bool[] enabledFlags)
        {
            if (product == null || product.modes == null || enabledFlags == null)
                return;

            var count = Mathf.Min(product.modes.Count, enabledFlags.Length);
            for (var i = 0; i < count; i++)
            {
                if (product.modes[i] == null)
                    continue;
                product.modes[i].enabled = enabledFlags[i];
            }

            EditorUtility.SetDirty(product);
        }

        public static void SyncBuildScenesNow(GameProductSettings product = null)
        {
            product ??= LoadProductSettings();
            if (product == null)
            {
                EditorUtility.DisplayDialog("同步 Build Scenes", "未找到 GameProductSettings.asset。", "确定");
                return;
            }

            GameProductFusionBuildTools.SyncBuildScenesFromProductSettings(
                product,
                silent: false,
                force: true,
                buildTarget: EditorUserBuildSettings.activeBuildTarget);
        }
    }
}
#endif
