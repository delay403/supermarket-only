using System;

using Game.Reward;

using Game.Audio;

using Game.Core;

using Game.Economy;

using Game.Save;

using Game.UI.Framework;

using Game.Core.Config;

using Game.Features.Treadmill;

using Game.Fishing;

using Game.Maps;

using UnityEngine;

using UnityEngine.UI;



namespace Game.UI

{

    /// <summary>

    /// UGUI：免费金币/钻石领取（Inspector Kind）；或 <see cref="UiPanelId.RewardDialog"/> + <see cref="UiOpenPayload.RewardDialog"/> 通用奖励文案；

    /// 或 <see cref="UiPanelId.FreeDoubleClimbSpeed"/> / <see cref="UiPanelId.FreeDoubleSettlementCoins"/> + <see cref="OfferKind.DoubleClimbSpeed"/> / <see cref="OfferKind.DoubleSettlementCoins"/> 独立 Buff 页（推荐）。

    /// </summary>

    public sealed class FreeCurrencyOfferPanel : MonoBehaviour, IUiPanelController

    {

        public enum OfferKind

        {

            Coins = 0,

            Gems = 1,

            DoubleClimbSpeed = 2,

            DoubleSettlementCoins = 3,

            TreadmillInstantReward = 4,

            AutoFishing = 5,

            UnlockFuelProducer = 6,

        }



        [SerializeField] private UiPanelId panelId = UiPanelId.FreeCoinsOffer;

        [SerializeField] private OfferKind kind = OfferKind.Coins;

        [SerializeField] private long rewardAmount = 999;



        public GameObject root;

        public RectTransform mainPanel;

        public Button maskCloseButton;

        public Button watchButton;

        public Button closeButton;

        public Text titleText;

        public Text rewardPreviewText;



        [Header("Animation (同 ResultPanel：自上而下弹入)")]

        public float slideDuration = 0.38f;

        public float settleDuration = 0.28f;

        public float overshootDownPx = 28f;



        [SerializeField] private UiSlidingPanelHost slideHost;



        RewardDialogPayload _rewardPayload;

        TreadmillInstantRewardOpenPayload _treadmillInstantPayload;

        FuelProducerUnlockOpenPayload _fuelProducerUnlockPayload;

        bool _rewardGrantBusy;



        public UiPanelId PanelId => panelId;



        void Awake()

        {

            if (maskCloseButton != null)

                maskCloseButton.onClick.AddListener(OnDismissClicked);

            if (closeButton != null)

                closeButton.onClick.AddListener(OnDismissClicked);

            if (watchButton != null)

                watchButton.onClick.AddListener(OnWatchClicked);



            EnsureSlideHost();

            RefreshCurrencyRewardAmount();

            HideImmediate();

        }



        void OnDestroy() => slideHost?.KillTweens();



        void OnEnable() => UiCoordinator.Register(this);



        void OnDisable() => UiCoordinator.Unregister(this);



        public void Open(UiOpenPayload payload = null)

        {

            _rewardPayload = payload?.RewardDialog;

            _treadmillInstantPayload = payload?.TreadmillInstantReward;

            _fuelProducerUnlockPayload = payload?.FuelProducerUnlock;

            if (_rewardPayload == null &&
                _treadmillInstantPayload == null &&
                kind == OfferKind.Coins &&
                panelId == UiPanelId.FreeCoinsOffer)
            {
                UiRouter.OpenCoinsHub();
                return;
            }

            Show();

        }



        public void Close() => Hide();



        public void Show()

        {

            EnsureSlideHost();



            if (_rewardPayload != null)

            {

                if (titleText != null)

                    titleText.text = string.IsNullOrEmpty(_rewardPayload.Title) ? "立即领取" : _rewardPayload.Title;

                if (rewardPreviewText != null)

                    rewardPreviewText.text = _rewardPayload.Description ?? "";

            }

            else if (IsBuffKind(kind))

            {

                ApplyBuffTexts();

            }

            else if (IsTreadmillInstantRewardKind(kind))

            {

                ApplyTreadmillInstantRewardTexts();

            }

            else if (IsAutoFishingKind(kind))

            {

                ApplyAutoFishingTexts();

            }

            else if (IsFuelProducerUnlockKind(kind))

            {

                ApplyFuelProducerUnlockTexts();

            }

            else
            {
                RefreshCurrencyRewardAmount();

                var title = kind == OfferKind.Coins ? "免费金币" : "免费钻石";

                if (titleText != null) titleText.text = title;

                var kindLabel = kind == OfferKind.Coins ? "金币" : "钻石";

                if (rewardPreviewText != null)
                    rewardPreviewText.text = $"获得{kindLabel} x{rewardAmount}";
            }

            slideHost.PlayOpen();
        }



        static bool IsBuffKind(OfferKind k) =>

            k == OfferKind.DoubleClimbSpeed || k == OfferKind.DoubleSettlementCoins;



        static bool IsTreadmillInstantRewardKind(OfferKind k) =>

            k == OfferKind.TreadmillInstantReward;



        static bool IsAutoFishingKind(OfferKind k) => k == OfferKind.AutoFishing;



        static bool IsFuelProducerUnlockKind(OfferKind k) => k == OfferKind.UnlockFuelProducer;



        void RefreshCurrencyRewardAmount()

        {

            if (kind == OfferKind.Coins)

            {

                rewardAmount = FreeCoinsRewardService.ResolveFreeCoins();

            }

            else if (kind == OfferKind.Gems)

            {

                rewardAmount = ConfigRegistry.Balance?.rewardFreeGems ?? 100;

            }

        }



        void ApplyTreadmillInstantRewardTexts()

        {

            var tier = _treadmillInstantPayload?.Tier ?? TreadmillTier.Basic;

            var minutes = TreadmillService.GetInstantRewardMinutes();

            var bonus = TreadmillService.ComputeSessionBonus(tier, TreadmillService.GetInstantRewardDurationSeconds());

            var tierName = TreadmillService.GetTierDisplayName(tier);



            if (titleText != null)

                titleText.text = "立即领取锻炼收益";

            if (rewardPreviewText != null)

            {

                rewardPreviewText.text =

                    $"立即获得 {tierName} 锻炼 {minutes:0.#} 分钟的收益：\n" +

                    $"永久爬塔速度 +{bonus:0.###} m/s";

            }

        }



        void ApplyAutoFishingTexts()

        {

            var minutes = AutoFishingService.PassDurationSeconds / 60d;

            if (titleText != null)

                titleText.text = "自动钓鱼";

            if (rewardPreviewText != null)

                rewardPreviewText.text =

                    $"立即解锁 {minutes:0.#} 分钟自动钓鱼：\n" +

                    "在钓鱼区内自动抛竿、抽杆并循环钓获。";

        }



        void ApplyFuelProducerUnlockTexts()

        {

            var def = Game.DestroyCity.FuelProducerService.ResolveDef(_fuelProducerUnlockPayload?.ProducerId);

            var displayName = def != null ? def.displayName : "自动燃料生产机";

            if (titleText != null)

                titleText.text = "解锁自动燃料生产";

            if (rewardPreviewText != null)

                rewardPreviewText.text =

                    $"立即永久解锁：\n{displayName}";

        }



        void ApplyBuffTexts()

        {

            if (kind == OfferKind.DoubleClimbSpeed)

            {

                if (titleText != null) titleText.text = "双倍攀爬速度";

                if (rewardPreviewText != null)

                    rewardPreviewText.text =

                        $"领取后{RewardBuffService.RewardDialogDurationLine}，攀爬速度 +100%。";

            }

            else if (kind == OfferKind.DoubleSettlementCoins)

            {

                if (titleText != null) titleText.text = "双倍金币";

                if (rewardPreviewText != null)

                    rewardPreviewText.text =

                        $"领取后{RewardBuffService.RewardDialogDurationLine}，滑翔结算金币 +100%。";

            }

        }



        public void Hide()

        {

            EnsureSlideHost();

            if (!slideHost.IsShown || slideHost.IsClosing)

                return;

            slideHost.RequestClose(ClearPayloads);

        }



        void ClearPayloads()

        {

            _rewardPayload = null;

            _treadmillInstantPayload = null;

            _fuelProducerUnlockPayload = null;

            _rewardGrantBusy = false;

            if (watchButton != null)

                watchButton.interactable = true;

        }



        void HideImmediate()

        {

            EnsureSlideHost();

            slideHost.HideImmediate();

            ClearPayloads();

        }



        void EnsureSlideHost()

        {

            UiSlidingPanelHost.EnsureOn(

                this,

                ref slideHost,

                root,

                mainPanel,

                UiSlidingPanelHost.SlideDirection.FromBottom,

                slideDuration,

                settleDuration,

                overshootDownPx);

        }



        void OnDismissClicked()

        {

            AudioGameplayFeedback.PlayUiButton();

            var cb = _rewardPayload?.Completion;

            Hide();

            cb?.Invoke(false);

        }



        void OnWatchClicked()

        {

            AudioGameplayFeedback.PlayUiButton();

            if (_rewardGrantBusy)
                return;



            if (_rewardPayload != null)

            {

                var cb = _rewardPayload.Completion;

                BeginRewardGrant(() =>

                {

                    cb?.Invoke(true);

                    Hide();

                });

                return;

            }



            if (kind == OfferKind.DoubleClimbSpeed)

            {

                BeginRewardGrant(() =>

                {

                    RewardBuffService.ActivateClimbBuff();

                    SaveService.Save();

                    UiCoordinator.Hud?.RefreshBuffDisplays();

                    Hide();

                });

                return;

            }



            if (kind == OfferKind.DoubleSettlementCoins)

            {

                BeginRewardGrant(() =>

                {

                    RewardBuffService.ActivateCoinsBuff();

                    SaveService.Save();

                    UiCoordinator.Hud?.RefreshBuffDisplays();

                    Hide();

                });

                return;

            }



            if (IsTreadmillInstantRewardKind(kind))

            {

                var tier = _treadmillInstantPayload?.Tier ?? TreadmillTier.Basic;

                BeginRewardGrant(() =>

                {

                    var bonus = TreadmillService.GrantInstantReward(tier);

                    if (bonus <= 0f)

                    {

                        AudioGameplayFeedback.PlayUiError();

                        return;

                    }



                    var treadmill = UnityObjectFinder.FindFirst<PlayerTreadmillController>();

                    treadmill?.RecordSessionInstantBonus(bonus);



                    SaveService.Save();

                    AudioGameplayFeedback.PlayResourceCollect();

                    Hide();

                });

                return;

            }



            if (IsAutoFishingKind(kind))

            {

                BeginRewardGrant(() =>

                {

                    AutoFishingService.ActivatePass(autoEnableMode: true);

                    UiCoordinator.Hud?.RefreshBuffDisplays();

                    Hide();

                });

                return;

            }



            if (IsFuelProducerUnlockKind(kind))

            {

                var producerId = _fuelProducerUnlockPayload?.ProducerId;

                BeginRewardGrant(() =>

                {

                    if (!string.IsNullOrWhiteSpace(producerId))

                        Game.DestroyCity.FuelProducerService.UnlockByReward(producerId);

                    Hide();

                });

                return;

            }



            var d = SaveService.Data;

            if (d == null) return;



            BeginRewardGrant(() =>

            {

                RefreshCurrencyRewardAmount();

                if (kind == OfferKind.Coins)

                    d.coins += rewardAmount;

                else

                    d.gems += rewardAmount;



                SaveService.Save();

                AudioGameplayFeedback.PlayResourceCollect();

                UiCoordinator.Hud?.RefreshEconomyDisplay();

                EconomyDisplayEvents.Raise();

                Hide();

            });

        }



        void BeginRewardGrant(Action onRewardGranted)

        {

            _rewardGrantBusy = true;

            if (watchButton != null)

                watchButton.interactable = false;



            FreeRewardGate.Grant(() =>

            {

                try

                {

                    onRewardGranted?.Invoke();

                }

                finally

                {

                    _rewardGrantBusy = false;

                    if (watchButton != null)

                        watchButton.interactable = true;

                }

            });

        }

    }

}


