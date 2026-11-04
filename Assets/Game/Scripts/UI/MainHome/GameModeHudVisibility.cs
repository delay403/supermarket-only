using System;

namespace Game.UI.MainHome
{
    /// <summary>单模式 HUD 右侧按钮显隐（在 GameProductSettings 各模式项中配置）。</summary>
    [Serializable]
    public struct GameModeHudVisibility
    {
        public bool petHatch;
        public bool petBackpack;
        public bool signIn;
        public bool character;
        public bool luckyWheel;
        public bool onlineReward;
        public bool trail;
        public bool wing;
        public bool rodShop;
        public bool fishCodex;

        public static GameModeHudVisibility AllVisible => new GameModeHudVisibility
        {
            petHatch = true,
            petBackpack = true,
            signIn = true,
            character = true,
            luckyWheel = true,
            onlineReward = true,
            trail = true,
            wing = true,
            rodShop = true,
            fishCodex = true,
        };

        public static GameModeHudVisibility SharedExceptFishingOnly => new GameModeHudVisibility
        {
            petHatch = true,
            petBackpack = true,
            signIn = true,
            character = true,
            luckyWheel = true,
            onlineReward = true,
            trail = true,
            wing = true,
            rodShop = false,
            fishCodex = false,
        };

        public static GameModeHudVisibility ForGameplayKind(MainHomeGameplayKind kind) =>
            kind == MainHomeGameplayKind.Fishing ? AllVisible : SharedExceptFishingOnly;
    }
}
