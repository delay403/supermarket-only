using System;
using UnityEngine;
using UnityEngine.UI;
using Game.UI.ViewModels;

namespace Game.UI
{
    public sealed class SignInSlotView : MonoBehaviour
    {
        public Text dayTitleText;
        public Image iconImage;
        public Text rewardText;

        [Header("点击领取")]
        [Tooltip("留空则自动取本物体上的 Button。")]
        [SerializeField] private Button slotButton;

        [Header("状态物体（互斥）")]
        [Tooltip("今日可领取时显示：高亮边框、闪光等。")]
        public GameObject highlightAvailableRoot;

        [Tooltip("明日可领取时显示：如「明天可领取」气泡/标签。")]
        public GameObject tomorrowOverlay;

        [Tooltip("已领取时显示：印章、灰显遮罩等。")]
        public GameObject claimedOverlay;

        [Header("可选")]
        [Tooltip("签到大奖格（如 DAY7）：额外底图/光效等。")]
        public GameObject grandPrizeChromeRoot;

        SignInSlotViewModel _bound;
        Action _onClaimClicked;
        bool _clickWired;

        void Awake() => EnsureButtonWired();

        public void BindClaim(Action onClaimClicked)
        {
            _onClaimClicked = onClaimClicked;
            EnsureButtonWired();
        }

        void EnsureButtonWired()
        {
            if (slotButton == null)
                TryGetComponent(out slotButton);

            if (slotButton == null || _clickWired)
                return;

            slotButton.onClick.AddListener(OnSlotClicked);
            _clickWired = true;
        }

        public void Apply(in SignInSlotViewModel vm)
        {
            _bound = vm;
            EnsureButtonWired();

            if (dayTitleText != null)
                dayTitleText.text = $"第 {vm.DayNumber} 天";

            var spr = vm.RewardIconOverride != null
                ? vm.RewardIconOverride
                : RewardPresentation.TryResolveIcon(vm.Reward);

            if (iconImage != null)
            {
                iconImage.sprite = spr;
                iconImage.enabled = spr != null;
            }

            if (rewardText != null)
                rewardText.text = RewardPresentation.FormatRewardLine(vm.Reward);

            var showClaimed = vm.State == SignInSlotState.Claimed;
            var showTomorrow = vm.State == SignInSlotState.Tomorrow;
            var showToday = vm.State == SignInSlotState.AvailableToday;

            if (claimedOverlay != null)
                claimedOverlay.SetActive(showClaimed);
            if (tomorrowOverlay != null)
                tomorrowOverlay.SetActive(showTomorrow);
            if (highlightAvailableRoot != null)
                highlightAvailableRoot.SetActive(showToday);

            if (grandPrizeChromeRoot != null)
                grandPrizeChromeRoot.SetActive(vm.IsGrandPrize);

            if (slotButton != null)
                slotButton.interactable = showToday;
        }

        void OnSlotClicked()
        {
            if (_bound == null || _bound.State != SignInSlotState.AvailableToday)
                return;

            _onClaimClicked?.Invoke();
        }

        void OnDestroy()
        {
            if (slotButton != null && _clickWired)
                slotButton.onClick.RemoveListener(OnSlotClicked);
        }
    }
}
