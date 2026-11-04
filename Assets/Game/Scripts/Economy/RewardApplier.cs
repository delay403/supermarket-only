using Game.Save;
using Game.Core.Config;
using Game.Audio;
using Game.Maps;

namespace Game.Economy
{
    public static class RewardApplier
    {
        public static void Apply(Rewards rewards)
        {
            if (SaveService.Data == null) return;
            SaveService.Data.coins += rewards.coins;
            SaveService.Data.gems += rewards.gems;

            if (rewards.medalCount > 0)
            {
                SaveService.Data.AddMedals(rewards.medalCount);
                MapRuntime.RaiseChanged();
            }

            if (rewards.coins > 0 || rewards.gems > 0 || rewards.medalCount > 0)
                AudioGameplayFeedback.PlayResourceCollect();
        }
    }
}
