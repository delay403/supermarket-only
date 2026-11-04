using Game.Audio;
using Game.Economy;
using Game.UI.Effects;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>摧毁城市：房屋摧毁后播放金币飞入 HUD 特效，并逐枚同步入账。</summary>
    public static class DestroyCityCoinFlyPresenter
    {
        const float HouseCoinFlyDuration = 0.48f;
        const float HouseCoinCollectSfxProgress = 0.92f;

        public static void TryPlayFromHouseDestroyed(Vector3 worldPosition, long coinsAwarded, Camera worldCamera = null)
        {
            if (coinsAwarded <= 0)
                return;

            var view = NukeLaunchViewModeController.Instance;
            if (view == null || !view.IsMissileMode)
            {
                EconomyCoinFlyGrant.GrantChunk(coinsAwarded);
                AudioGameplayFeedback.PlayResourceCollect();
                return;
            }

            var effect = UICoinFlyEffect.EnsureInstance();
            if (effect == null)
            {
                EconomyCoinFlyGrant.GrantChunk(coinsAwarded);
                AudioGameplayFeedback.PlayResourceCollect();
                return;
            }

            effect.Play(new UICoinFlyPlayParams
            {
                WorldPosition = worldPosition,
                WorldCamera = worldCamera != null ? worldCamera : Camera.main,
                CoinAmount = coinsAwarded,
                MoveDuration = HouseCoinFlyDuration,
                MoveDurationJitter = 0f,
                GrantCoinsOnArrival = true,
                PlayCollectSfx = true,
                PlayCollectSfxOnce = true,
                CollectSfxMoveProgress = HouseCoinCollectSfxProgress,
            });
        }
    }
}
