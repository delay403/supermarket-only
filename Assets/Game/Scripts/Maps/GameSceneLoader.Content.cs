using Game.Core;
using Game.Core.Content;
using UnityEngine;

namespace Game.Maps
{
    public static partial class GameSceneLoader
    {
        public static GameplayModule ResolveModuleForSceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return GameplayModule.None;

            sceneName = sceneName.Trim();
            if (sceneName.IndexOf("AnimalHospital", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.AnimalHospital;
            if (sceneName.IndexOf("Supermarket", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.SupermarketOrganize;
            if (sceneName.IndexOf("Fish", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.Fishing;
            if (sceneName.IndexOf("DestroyCity", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.DestroyCity;
            if (sceneName.IndexOf("EscapeWorm", System.StringComparison.OrdinalIgnoreCase) >= 0
                || sceneName.IndexOf("Worm", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return GameplayModule.WormDodge;
            if (sceneName.StartsWith("Map", System.StringComparison.OrdinalIgnoreCase))
                return GameplayModule.Tower;

            return GameplayModule.None;
        }

        public static bool IsShellScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            sceneName = sceneName.Trim();
            return sceneName.Equals("MainHome", System.StringComparison.OrdinalIgnoreCase)
                   || sceneName.Equals("SceneTransition", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldUseAddressablesForScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || IsShellScene(sceneName))
                return false;

            if (Application.CanStreamedLevelBeLoaded(sceneName.Trim()))
                return false;

            return ResolveModuleForSceneName(sceneName) != GameplayModule.None;
        }

        public static string ResolveAddressableSceneAddress(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return string.Empty;

            sceneName = sceneName.Trim();
            var module = ResolveModuleForSceneName(sceneName);
            if (module == GameplayModule.None)
                return string.Empty;

            return $"{GameContentPaths.ModeScenesRoot(module)}/{sceneName}.unity";
        }
    }
}
