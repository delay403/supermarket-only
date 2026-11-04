using System;
using Game.Save;
using Game.UI.MainHome;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>当前正在游玩的玩法模块（融合包体下用于 HUD 分区与存档路由）。</summary>
    public static class GameplayModuleContext
    {
        public static event Action<GameplayModule, GameplayModule> ModuleChanged;

        static GameplayModule s_activeModule = GameplayModule.None;
        static bool s_explicit;
        static MainHomeGameModeEntry s_activeEntry;

        public static GameplayModule ActiveModule => ResolveActive();
        public static MainHomeGameModeEntry ActiveEntry =>
            s_activeEntry ?? GameModeRuntimeConfig.FindEntryForModule(ActiveModule);

        public static void SetActiveModule(GameplayModule module)
        {
            if (module == s_activeModule && s_explicit)
                return;

            var previous = s_activeModule;
            if (module != s_activeModule && s_activeEntry != null && s_activeEntry.RequiredModule != module)
                s_activeEntry = null;

            if (IsFusionProduct() && SaveService.Data != null)
                ModeSaveAccess.SwitchActiveModule(previous, module);

            s_activeModule = module;
            s_explicit = module != GameplayModule.None;
            ModuleChanged?.Invoke(previous, module);
        }

        public static void SetFromEntry(MainHomeGameModeEntry entry)
        {
            if (entry == null)
                return;

            s_activeEntry = entry;
            var module = entry.RequiredModule;
            if (module != GameplayModule.None)
                SetActiveModule(module);
        }

        public static void SetFromSceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            sceneName = sceneName.Trim();
            if (sceneName == "FishScene" || sceneName.Contains("Fish"))
            {
                SetActiveModule(GameplayModule.Fishing);
                return;
            }

            if (sceneName == "DestroyCityScene" || sceneName.Contains("DestroyCity"))
            {
                SetActiveModule(GameplayModule.DestroyCity);
                return;
            }

            if (sceneName == "EscapeWormScene" || sceneName == "jiechongzi" || sceneName.Contains("Worm"))
            {
                SetActiveModule(GameplayModule.WormDodge);
                return;
            }

            if (sceneName == "SupermarketOrganizeScene" || sceneName.Contains("SupermarketOrganize"))
            {
                SetActiveModule(GameplayModule.SupermarketOrganize);
                return;
            }

            if (sceneName == "AnimalHospitalScene" || sceneName.Contains("AnimalHospital"))
            {
                SetActiveModule(GameplayModule.AnimalHospital);
                return;
            }

            if (sceneName == "MainHome" || sceneName == "SceneTransition")
            {
                ClearExplicit();
                return;
            }

            SetActiveModule(GameplayModule.Tower);
        }

        public static void ApplyLoadedScene(Scene scene)
        {
            if (!scene.IsValid())
                return;

            SetFromSceneName(scene.name);
        }

        public static bool IsFusionProduct() => GameProductSettings.CountEnabledModules() > 1;

        public static bool IsModuleActive(GameplayModule module)
        {
            if (module == GameplayModule.None)
                return true;

            if (!IsFusionProduct())
                return GameProductSettings.HasModule(module);

            return ActiveModule == module;
        }

        public static void ClearExplicit()
        {
            s_explicit = false;
            s_activeModule = GameplayModule.None;
            s_activeEntry = null;
        }

        static GameplayModule ResolveActive()
        {
            if (s_explicit && s_activeModule != GameplayModule.None)
                return s_activeModule;

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                var fromScene = InferFromSceneName(scene.name);
                if (fromScene != GameplayModule.None)
                    return fromScene;
            }

            return InferFromProductSettings();
        }

        static GameplayModule InferFromSceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return GameplayModule.None;

            sceneName = sceneName.Trim();
            if (sceneName == "FishScene") return GameplayModule.Fishing;
            if (sceneName == "DestroyCityScene") return GameplayModule.DestroyCity;
            if (sceneName == "EscapeWormScene" || sceneName == "jiechongzi") return GameplayModule.WormDodge;
            if (sceneName == "SupermarketOrganizeScene") return GameplayModule.SupermarketOrganize;
            if (sceneName == "AnimalHospitalScene") return GameplayModule.AnimalHospital;
            if (sceneName == "MainHome" || sceneName == "SceneTransition")
                return GameplayModule.None;

            if (sceneName.StartsWith("Map") || sceneName.Contains("Tower"))
                return GameplayModule.Tower;

            return GameplayModule.None;
        }

        static GameplayModule InferFromProductSettings()
        {
            var modules = GameProductSettings.ResolveEnabledModules();
            if (modules == GameplayModule.WormDodge) return GameplayModule.WormDodge;
            if (modules == GameplayModule.SupermarketOrganize) return GameplayModule.SupermarketOrganize;
            if (modules == GameplayModule.AnimalHospital) return GameplayModule.AnimalHospital;
            if (modules == GameplayModule.DestroyCity) return GameplayModule.DestroyCity;
            if (modules == GameplayModule.Fishing) return GameplayModule.Fishing;
            if (modules == GameplayModule.Tower) return GameplayModule.Tower;
            return GameplayModule.Tower;
        }
    }
}
