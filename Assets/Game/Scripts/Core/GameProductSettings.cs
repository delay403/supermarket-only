namespace Game.Core
{
    public static class GameProductSettings
    {
        public static GameProductMode ActiveMode => GameProductMode.SupermarketOrganizeOnly;
        public static bool IsSupermarketOrganizeContentEnabled => true;
        public static GameplayModule ResolveEnabledModules() => GameplayModule.SupermarketOrganize;
        public static bool HasModule(GameplayModule module) => module == GameplayModule.SupermarketOrganize;
        public static int CountEnabledModules() => 1;
    }
}
