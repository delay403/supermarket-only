using Game.UI.ViewModels;

namespace Game.UI.Framework
{
    /// <summary>
    /// Optional data passed when opening a panel. Extend with new properties as needed.
    /// FGUI layer can read the same payload to bind GComponent.
    /// </summary>
    public sealed class UiOpenPayload
    {
        public SettlementViewModel Settlement;

        /// <summary>非空时：<see cref="FreeCurrencyOfferPanel"/> 进入通用激励模式（不发免费金币/钻石）。</summary>
        public RewardDialogPayload RewardDialog;

        /// <summary>非空时：<see cref="Game.UI.PetHatchPanel"/> 打开后选中对应卡池。</summary>
        public PetHatchOpenPayload PetHatch;

        public TreadmillUnlockOpenPayload TreadmillUnlock;

        public StrengthTrainingUnlockOpenPayload StrengthTrainingUnlock;

        public TreadmillInstantRewardOpenPayload TreadmillInstantReward;

        public DiscountOfferOpenPayload DiscountOffer;

        public CoinsHubOpenPayload CoinsHub;

        public FuelProducerUnlockOpenPayload FuelProducerUnlock;

        public ViewModels.TreadmillResultViewModel TreadmillResult;

        public ViewModels.WormDodgeResultViewModel WormDodgeResult;

        public ViewModels.WormDodgeFloorRewardViewModel WormDodgeFloorReward;

        public ViewModels.SupermarketOrganizeResultViewModel SupermarketOrganizeResult;

        public ViewModels.AnimalHospitalResultViewModel AnimalHospitalResult;
    }
}
