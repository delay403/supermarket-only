using Game.Core.Config;
using Game.Features.StrengthTraining;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>钓鱼 HUD 激励入口：加力量等。</summary>
    public static class FishingStrengthRewardService
    {
        public static float TryGrantStrength()
        {
            var delta = FishingWorldAreaRewardService.ResolveStrengthBonus();
            if (delta <= 0f)
                return 0f;

            StrengthTrainingService.AddBaseStrength(delta);
            return delta;
        }
    }
}
