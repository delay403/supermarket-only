using Game.Audio;
using Game.Core;
using Game.Glide;
using Game.Tower;
using UnityEngine;

namespace Game.UI.Framework
{
    /// <summary>
    /// 攀爬中：仍可打开免费奖励/双倍 Buff/金币钻石等面板；拦截地图与各装备商店。
    /// 滑翔、短落、空翻或落地结算至回出生点前：拦截地图/装备商店等；免费金币/钻石弹窗仅在<strong>正在滑翔</strong>（<see cref="GlideController.isGliding"/>）
    /// 且尚未进入结算锁（<see cref="GlideController.BlockShopNavigationUntilRespawn"/>）时可打开，其余激励/Buff 类仍拦截（结算 <see cref="UiPanelId.Result"/>、设置除外，由路由保证）。
    /// </summary>
    public static class GameplayNavigationBlock
    {
        public static bool IsClimbing()
        {
            var climb = UnityObjectFinder.FindFirst<ClimbController>();
            return climb != null && climb.State != ClimbController.ClimbState.Inactive;
        }

        /// <summary>滑翔/短落/空翻进行中，或已落地结算且尚未重生。</summary>
        public static bool IsAerialOrBlockedUntilRespawn()
        {
            var glide = UnityObjectFinder.FindFirst<GlideController>();
            if (glide == null)
                return false;
            return glide.IsAerialSequenceActive || glide.BlockShopNavigationUntilRespawn;
        }

        /// <summary>地图、翅膀/拖尾/角色/宠物等 — 攀爬与空中/结算锁定期均不可打开。</summary>
        public static bool BlocksMapAndEquipmentShops() =>
            IsClimbing() || IsAerialOrBlockedUntilRespawn();

        /// <summary>双倍 Buff、免费金币/钻石、转盘、签到、在线奖励等 — 仅空中/结算锁定期拦截，攀爬中允许。</summary>
        public static bool BlocksOffersBuffsAndRewardDialogsUntilRespawn() =>
            IsAerialOrBlockedUntilRespawn();

        /// <summary>HUD 上点金币/钻石入口：与 <see cref="ShouldDenyOpeningPanel"/> 一致，正在滑翔时可打开补币/钻弹窗。</summary>
        public static bool ShouldBlockOpeningFreeCurrencyFromHud() =>
            IsAerialOrBlockedUntilRespawn() && !AllowsFreeCurrencyOfferDuringActiveGlide(UiPanelId.CoinsHub);

        /// <summary>若当前应拦截 <paramref name="id"/> 的打开，返回 true（调用方勿再发放奖励/开面板）。</summary>
        public static bool ShouldDenyOpeningPanel(UiPanelId id)
        {
            if (!IsRestrictedGameplayPanel(id))
                return false;

            if (IsAerialOrBlockedUntilRespawn())
            {
                if (AllowsFreeCurrencyOfferDuringActiveGlide(id))
                    return false;
                return true;
            }

            if (IsClimbing() && IsMapOrEquipmentShopPanel(id))
                return true;

            return false;
        }

        /// <summary>仅空中滑行（非短落/空翻/落地后结算锁）时允许补金币/钻石的免费领取弹窗。</summary>
        static bool AllowsFreeCurrencyOfferDuringActiveGlide(UiPanelId id)
        {
            if (id != UiPanelId.CoinsHub && id != UiPanelId.FreeDiamondsOffer)
                return false;
            var glide = UnityObjectFinder.FindFirst<GlideController>();
            return glide != null && glide.isGliding && !glide.BlockShopNavigationUntilRespawn;
        }

        static bool IsRestrictedGameplayPanel(UiPanelId id) =>
            IsMapOrEquipmentShopPanel(id) || IsOfferBuffOrRewardPanel(id);

        static bool IsMapOrEquipmentShopPanel(UiPanelId id)
        {
            switch (id)
            {
                case UiPanelId.WingShop:
                case UiPanelId.TrailShop:
                case UiPanelId.CharacterShop:
                case UiPanelId.Pet:
                case UiPanelId.PetHatch:
                case UiPanelId.MapSelect:
                    return true;
                default:
                    return false;
            }
        }

        static bool IsOfferBuffOrRewardPanel(UiPanelId id)
        {
            switch (id)
            {
                case UiPanelId.SignIn:
                case UiPanelId.LuckyWheel:
                case UiPanelId.CoinsHub:
                case UiPanelId.FreeDiamondsOffer:
                case UiPanelId.OnlineReward:
                case UiPanelId.FreeDoubleClimbSpeed:
                case UiPanelId.FreeDoubleSettlementCoins:
                case UiPanelId.FreeTreadmillInstantReward:
                case UiPanelId.FreeAutoFishing:
                // case UiPanelId.FreeUnlockFuelProducer:
                case UiPanelId.DiscountOffer:
                case UiPanelId.RewardDialog:
                    return true;
                default:
                    return false;
            }
        }

        public static void ShowShopNavigationBlockedHint()
        {
            AudioGameplayFeedback.PlayUiError();
            HUDMgr.ShowHUD(new HUDMessage
            {
                Title = "提示",
                Content = "滑翔、坠落或结算期间无法打开，请返回出生点后再试。",
                BtnShow = true,
                MiddleBtnShow = true,
                ConfirmText = "确认",
                CloseOnConfirm = true,
            });
        }
    }
}
