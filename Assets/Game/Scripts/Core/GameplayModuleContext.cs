using System;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    public static class GameplayModuleContext
    {
        public static event Action<GameplayModule, GameplayModule> ModuleChanged;

        public static GameplayModule ActiveModule => GameplayModule.SupermarketOrganize;

        public static void SetActiveModule(GameplayModule module)
        {
            if (module == GameplayModule.SupermarketOrganize)
                ModuleChanged?.Invoke(GameplayModule.SupermarketOrganize, GameplayModule.SupermarketOrganize);
        }

        public static void ApplyLoadedScene(Scene scene) { }
        public static void ClearExplicit() { }
        public static bool IsFusionProduct() => false;

        public static bool IsModuleActive(GameplayModule module) =>
            module == GameplayModule.None || module == GameplayModule.SupermarketOrganize;
    }
}
