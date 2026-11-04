using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// 集中一行式调用，避免业务脚本重复 null 检查。
    /// 面板音效约定：<see cref="SfxId.UiPanelOpen"/> — 设置等由 <see cref="Game.UI.Framework.UiCoordinator.Open"/> 在打开后播放；
    /// 滑行结算 <see cref="Game.UI.Framework.UiPanelId.Result"/> 不走 Coordinator 的通用面板音，由 <see cref="Game.UI.ResultPanel"/> /
    /// <see cref="Game.UI.FairyGUI.Mediators.ResultFguiMediator"/> 各自调用 <see cref="PlayPanelOpen"/>。
    /// 滑动条松手：<see cref="SfxId.UiSliderChange"/>（设置面板 Slider 上通过 EventTrigger PointerUp 触发）。
    /// </summary>
    public static class AudioGameplayFeedback
    {
        public static void PlayUiButton() => AudioManager.Instance?.PlaySfx(SfxId.UiButtonClick);

        public static void PlayPanelOpen() => AudioManager.Instance?.PlaySfx(SfxId.UiPanelOpen);

        public static void PlayUiSelect() => AudioManager.Instance?.PlaySfx(SfxId.UiSelect);

        public static void PlayUiError() => AudioManager.Instance?.PlaySfx(SfxId.UiError);

        public static void PlayResourceCollect(float volumeMultiplier = 1f) =>
            AudioManager.Instance?.PlaySfx(SfxId.ResourceCollect, volumeMultiplier);

        public static void PlayResourceDeduct(float volumeMultiplier = 1f) =>
            AudioManager.Instance?.PlaySfx(SfxId.ResourceDeduct, volumeMultiplier);

        public static void PlayInteract() => AudioManager.Instance?.PlaySfx(SfxId.Interact);

        public static void PlayMove(float volumeMultiplier = 1f) =>
            AudioManager.Instance?.PlaySfx(SfxId.Move, volumeMultiplier);

        public static void PlayJump() => AudioManager.Instance?.PlaySfx(SfxId.Jump);

        public static void PlayUiSliderChange() => AudioManager.Instance?.PlaySfx(SfxId.UiSliderChange);

        public static void PlayGlideDistanceMilestone(float volumeMultiplier = 1f) =>
            AudioManager.Instance?.PlaySfx(SfxId.GlideDistanceMilestone, volumeMultiplier);

        public static void PlayFishingWaterSplash(Vector3 worldPosition) =>
            AudioManager.Instance?.PlaySfx3D(SfxId.FishingWaterSplash, worldPosition);

        public static void PlayFishingCatchSettle(Vector3 worldPosition) =>
            PlayCatchSettleSound(SfxId.FishingCatchSettle, worldPosition);

        public static void PlayCatchSettleSound(SfxId sfxId, Vector3? worldPosition = null)
        {
            if (sfxId == SfxId.None)
                sfxId = SfxId.FishingCatchSettle;

            if (worldPosition.HasValue)
                AudioManager.Instance?.PlaySfx3D(sfxId, worldPosition.Value);
            else
                AudioManager.Instance?.PlaySfx(sfxId);
        }

        public static void PlayUiFloatPopup(bool merged = false)
        {
            var volume = merged ? 0.82f : 1f;
            AudioManager.Instance?.PlaySfx(SfxId.UiFloatPopup, volume);
        }

        public static void PlayFishingProgressGain() =>
            AudioManager.Instance?.PlaySfx(SfxId.FishingProgressGain);

        public static void PlayFishingReelPressRandom() =>
            AudioManager.Instance?.PlayFishingReelPressRandom();

        public static void PlayAreaUnlock() => AudioManager.Instance?.PlaySfx(SfxId.AreaUnlock);

        public static void PlayFishingCatchCelebrate(Vector3 worldPosition, SfxId sfxId = SfxId.None)
        {
            PlayCatchSettleSound(
                sfxId != SfxId.None ? sfxId : SfxId.FishingCatchCelebrate1,
                worldPosition);
        }
    }
}
