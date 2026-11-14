using Game.Audio;
using Game.Economy;
using Game.Economy.Config;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 钻石兑换金币格子：BG/Icon 档位图标、Value 获得金币、Cost 消耗钻石；点击 BG 兑换。
    /// </summary>
    public sealed class DiamondCoinsExchangeTierItemView : MonoBehaviour
    {
        [SerializeField] private Image coinIconImage;
        [SerializeField] private Text coinsValueText;
        [FormerlySerializedAs("gemCostText")]
        [SerializeField] private Text gemCostText;
        [SerializeField] private Button exchangeButton;

        CoinsHubPanel _hub;
        int _tierId;

        void Awake()
        {
            ResolveBindings();
            exchangeButton?.onClick.AddListener(OnExchangeClicked);
        }

        void ResolveBindings()
        {
            if (coinIconImage == null)
            {
                var icon = transform.Find("BG/Icon");
                if (icon != null)
                    coinIconImage = icon.GetComponent<Image>();
            }

            if (coinsValueText == null)
            {
                var value = transform.Find("Value");
                if (value != null)
                    coinsValueText = value.GetComponent<Text>();
            }

            if (gemCostText == null)
            {
                var cost = transform.Find("Cost");
                if (cost != null)
                    gemCostText = cost.GetComponent<Text>();
            }

            if (exchangeButton == null)
            {
                var bg = transform.Find("BG");
                if (bg != null)
                    exchangeButton = bg.GetComponent<Button>();
            }
        }

        public void Bind(CoinsHubPanel hub, DiamondCoinsExchangeTierDef tier)
        {
            _hub = hub;
            _tierId = tier.tierId;
            var coins = DiamondCoinsExchangeService.ResolveCoinsForTier(tier);

            if (coinsValueText != null)
                coinsValueText.text = $"{EconomyDisplayFormat.Format(coins)}";

            if (gemCostText != null)
                gemCostText.text = $"{EconomyDisplayFormat.Format(tier.gemCost)}";

            if (coinIconImage != null && tier.coinIcon != null)
                coinIconImage.sprite = tier.coinIcon;
        }

        void OnExchangeClicked()
        {
            if (_hub != null)
            {
                _hub.RequestExchangeTier(_tierId);
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            if (DiamondCoinsExchangeService.TryExchangeTier(_tierId, out _, out var insufficientGems))
            {
                AudioGameplayFeedback.PlayResourceCollect();
                UiCoordinator.Hud?.RefreshEconomyDisplay();
                return;
            }

            AudioGameplayFeedback.PlayUiError();
        }
    }
}
