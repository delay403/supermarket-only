using System;
using Game.Economy;
using Game.Economy.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>HUD 限时优惠入口按钮（含倒计时）。</summary>
    public sealed class DiscountOfferHudEntryView : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private Text titleText;
        [SerializeField] private Text timerText;

        int _offerId;

        public int OfferId => _offerId;

        public event Action<int> OpenRequested;

        public void ConfigureRuntime(Button button, Text title, Text timer)
        {
            openButton = button;
            titleText = title;
            timerText = timer;
        }

        void Awake()
        {
            openButton?.onClick.AddListener(OnOpenClicked);
        }

        public void Bind(int offerId, DiscountOfferDef def)
        {
            _offerId = offerId;
            if (titleText != null)
                titleText.text = string.IsNullOrWhiteSpace(def.title) ? "限时优惠" : def.title;
            RefreshTimer();
        }

        public void RefreshTimer()
        {
            if (timerText == null)
                return;

            var seconds = DiscountOfferService.GetHudEntryRemainingSeconds(_offerId);
            if (seconds <= 0d)
            {
                timerText.text = "00:00";
                return;
            }

            var total = (int)Math.Floor(seconds);
            var m = total / 60;
            var s = total % 60;
            timerText.text = $"{m:00}:{s:00}";
        }

        void OnOpenClicked()
        {
            if (_offerId <= 0)
                return;

            OpenRequested?.Invoke(_offerId);
        }
    }
}
