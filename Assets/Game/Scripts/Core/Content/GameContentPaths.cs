using System.Collections.Generic;

namespace Game.Core.Content
{
    public static class GameContentPaths
    {
        public const string CoreResourcesRoot = "Assets/Game/Core/Resources";
        public const string ModesRoot = "Assets/Game/Modes";
        public const string SharedRoot = "Assets/Game/Shared";
        public const string ModeSlotResources = "Resources";
        public const string ModeSlotContent = "Content";
        public const string ModeSlotArt = "Art";
        public const string ModeSlotAudio = "Audio";
        public const string ModeSlotScenes = "Scenes";
        public const string ModeSlotConfigs = "Configs";

        public static readonly string[] ModeStandardSlots =
        {
            ModeSlotResources,
            ModeSlotContent,
            ModeSlotArt,
            ModeSlotAudio,
            ModeSlotScenes,
        };

        public static string ModeContentRoot(GameplayModule module) => ModeSlotRoot(module, ModeSlotContent);
        public static string ModeConfigRoot(GameplayModule module) => $"{ModeContentRoot(module)}/{ModeSlotConfigs}";
        public static string SupermarketConfigRoot => $"{ModeConfigRoot(GameplayModule.SupermarketOrganize)}/SupermarketOrganize";
        public static string SupermarketItemDatabaseAssetPath => $"{SupermarketConfigRoot}/SupermarketItemDatabase.asset";
        public static string SupermarketItemDefFolder => $"{SupermarketConfigRoot}/Item";
        public static string SupermarketArtRoot => ModeArtRoot(GameplayModule.SupermarketOrganize);
        public static string ModeArtRoot(GameplayModule module) => ModeSlotRoot(module, ModeSlotArt);
        public static string ModeAudioRoot(GameplayModule module) => ModeSlotRoot(module, ModeSlotAudio);
        public static string ModeScenesRoot(GameplayModule module) => ModeSlotRoot(module, ModeSlotScenes);
        public static string ModeResourcesRoot(GameplayModule module) => ModeSlotRoot(module, ModeSlotResources);
        public static string ModeSlotRoot(GameplayModule module, string slot) => $"{ModesRoot}/SupermarketOrganize/{slot}";

        public static IEnumerable<GameplayModule> EnumerateGameplayModules()
        {
            yield return GameplayModule.SupermarketOrganize;
        }

        public static string ModuleFolderName(GameplayModule module) => "SupermarketOrganize";
    }
}
