using Game.Audio;
using Game.Core.Config;
using Game.DailySignIn;
using Game.Economy;
using Game.Features.LuckyWheel;
using Game.Features.OnlineReward;
using Game.Save;
using Game.UI;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hud
{
    /// <summary>
    /// 主页精简活动 HUD：钻石栏 + 在线奖励 / 签到 / 转盘入口。
    /// 可在 Inspector 直接引用活动库 SO；非空时会合并进 <see cref="ConfigRegistry"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeActivityHudPanel : MonoBehaviour
    {
        [Header("钻石")]
        [SerializeField] private GameObject gemsRoot;
        [SerializeField] private Text gemsText;
        [SerializeField] private Button gemsPlusButton;

        [Header("活动按钮")]
        [SerializeField] private Button btnOnlineReward;
        [SerializeField] private Button btnSignIn;
        [SerializeField] private Button btnLuckyWheel;

        [Header("可选红点")]
        [SerializeField] private GameObject onlineRewardDot;
        [SerializeField] private GameObject signInDot;
        [SerializeField] private GameObject luckyWheelDot;

        [Header("活动库 SO（主页专用引用，可覆盖全局配置）")]
        [SerializeField] private OnlineRewardDatabase onlineRewardDatabase;
        [SerializeField] private SignInDatabase signInDatabase;
        [SerializeField] private WheelDatabase wheelDatabase;

        [Header("显隐")]
        [SerializeField] private bool showGems = true;
        [SerializeField] private bool showOnlineReward = true;
        [SerializeField] private bool showSignIn = true;
        [SerializeField] private bool showLuckyWheel = true;

        float _nextDotRefresh;
        RollingNumberText _gemsRolling;

        void Awake()
        {
            TryAutoResolveBindings();
            CacheRollingTexts();
            ApplyConfigOverrides();
            EnsureTicker();
            WireButtons();
            ApplyVisibility();
            RefreshGemsFromSave(immediate: true);
        }

        void OnEnable()
        {
            ApplyConfigOverrides();
            EconomyDisplayEvents.Changed += OnEconomyDisplayChanged;
            RefreshGemsFromSave(immediate: true);
            RefreshDots();
        }

        void OnDisable()
        {
            EconomyDisplayEvents.Changed -= OnEconomyDisplayChanged;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextDotRefresh)
                return;

            _nextDotRefresh = Time.unscaledTime + 1f;
            RefreshDots();
        }

        void TryAutoResolveBindings()
        {
            if (gemsPlusButton == null)
                gemsPlusButton = FindButton("GemsPlus", "GemPlus", "BtnGemPlus", "AddGem", "gemsPlusButton");
            if (btnOnlineReward == null)
                btnOnlineReward = FindButton("BtnOnlineReward", "OnlineReward", "btnOnlineReward");
            if (btnSignIn == null)
                btnSignIn = FindButton("BtnSignIn", "SignIn", "btnSignIn");
            if (btnLuckyWheel == null)
                btnLuckyWheel = FindButton("BtnLuckyWheel", "LuckyWheel", "btnLuckyWheel");

            if (gemsText == null)
            {
                var texts = GetComponentsInChildren<Text>(true);
                for (var i = 0; i < texts.Length; i++)
                {
                    var t = texts[i];
                    if (t == null)
                        continue;
                    var n = t.gameObject.name;
                    if (n.IndexOf("Gem", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Diamond", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("钻石", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        gemsText = t;
                        break;
                    }
                }
            }
        }

        Button FindButton(params string[] names)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                if (b == null)
                    continue;
                var n = b.gameObject.name;
                for (var j = 0; j < names.Length; j++)
                {
                    if (string.Equals(n, names[j], System.StringComparison.OrdinalIgnoreCase))
                        return b;
                }
            }

            return null;
        }

        void CacheRollingTexts()
        {
            if (gemsText == null)
            {
                _gemsRolling = null;
                return;
            }

            _gemsRolling = gemsText.GetComponent<RollingNumberText>();
            if (_gemsRolling == null)
                _gemsRolling = gemsText.gameObject.AddComponent<RollingNumberText>();
        }

        void ApplyConfigOverrides()
        {
            if (onlineRewardDatabase != null)
                ConfigRegistry.SetOnlineRewardOverride(onlineRewardDatabase);
            if (signInDatabase != null)
                ConfigRegistry.SetSignInOverride(signInDatabase);
            if (wheelDatabase != null)
                ConfigRegistry.SetWheelOverride(wheelDatabase);
        }

        void EnsureTicker()
        {
            if (GetComponent<OnlineRewardTicker>() == null
                && Game.Core.UnityObjectFinder.FindFirst<OnlineRewardTicker>() == null)
                gameObject.AddComponent<OnlineRewardTicker>();
        }

        void WireButtons()
        {
            gemsPlusButton?.onClick.AddListener(OnGemsPlusClicked);
            btnOnlineReward?.onClick.AddListener(OnOnlineRewardClicked);
            btnSignIn?.onClick.AddListener(OnSignInClicked);
            btnLuckyWheel?.onClick.AddListener(OnLuckyWheelClicked);
        }

        public void ApplyVisibility()
        {
            if (gemsRoot != null)
                gemsRoot.SetActive(showGems);
            else if (gemsText != null)
                gemsText.gameObject.SetActive(showGems);

            if (gemsPlusButton != null)
                gemsPlusButton.gameObject.SetActive(showGems);

            SetVisible(btnOnlineReward, showOnlineReward);
            SetVisible(btnSignIn, showSignIn);
            SetVisible(btnLuckyWheel, showLuckyWheel);
        }

        static void SetVisible(Button button, bool visible)
        {
            if (button != null)
                button.gameObject.SetActive(visible);
        }

        void OnEconomyDisplayChanged() => RefreshGemsFromSave(immediate: false);

        public void RefreshGemsFromSave(bool immediate = false)
        {
            if (!showGems)
                return;

            var gems = SaveService.Data?.gems ?? 0L;
            CacheRollingTexts();
            if (_gemsRolling != null)
                _gemsRolling.AnimateTo(gems, immediate);
            else if (gemsText != null)
                gemsText.text = EconomyDisplayFormat.Format(gems);
        }

        public void RefreshDots()
        {
            if (onlineRewardDot != null)
                onlineRewardDot.SetActive(showOnlineReward && OnlineRewardService.HasClaimableReward());

            if (signInDot != null)
                signInDot.SetActive(showSignIn && SignInService.CanClaimToday());

            if (luckyWheelDot != null)
                luckyWheelDot.SetActive(showLuckyWheel && LuckyWheelService.FreeSpinsRemainingToday() > 0);
        }

        void OnGemsPlusClicked()
        {
            if (GameplayNavigationBlock.ShouldBlockOpeningFreeCurrencyFromHud())
            {
                GameplayNavigationBlock.ShowShopNavigationBlockedHint();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenFreeDiamondsOffer();
        }

        void OnOnlineRewardClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenOnlineReward();
        }

        void OnSignInClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenSignIn();
        }

        void OnLuckyWheelClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenLuckyWheel();
        }
    }
}
