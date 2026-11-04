using Game.Economy;
using Game.UI;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.DestroyCity
{
    /// <summary>燃料生产器升级面板（世界空间 UGUI Button + 等级 / 单次经验 / 升级金币）。</summary>
    [DisallowMultipleComponent]
    public sealed class FuelProducerUpgradeUi : MonoBehaviour
    {
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Text levelText;
        [SerializeField] private Text tickXpText;
        [SerializeField] private Text upgradeCostText;
        [SerializeField] private string levelFormat = "生产机等级：{0}";
        [SerializeField] private string manualTickFormat = "单次获得经验：{0}";
        [SerializeField] private string autoTickFormat = "每次产出：{0}";
        [SerializeField] private string upgradeCostFormat = "金币：{0}";
        [SerializeField] private string maxLevelCostText = "已满级";

        FuelProducerController _producer;

        public void Bind(FuelProducerController producer)
        {
            _producer = producer;
            Refresh();
        }

        void Awake()
        {
            if (upgradeButton == null)
                upgradeButton = GetComponentInChildren<Button>(true);
            if (levelText == null)
                levelText = FindTextInChildren(transform, "Level", "LevelText", "等级");
            if (tickXpText == null)
                tickXpText = FindTextInChildren(transform, "XP", "TickXp", "XpText");
            if (upgradeCostText == null && upgradeButton != null)
                upgradeCostText = upgradeButton.GetComponentInChildren<Text>(true);

            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(OnUpgradeClicked);

            EnsureCanvasInput();
            DisableDecorativeTextRaycasts();
        }

        void EnsureCanvasInput()
        {
            var canvas = GetComponentInParent<Canvas>();
            WorldSpace3DCanvas.EnsureCanvasInputReady(canvas);
        }

        void DisableDecorativeTextRaycasts()
        {
            if (levelText != null)
                levelText.raycastTarget = false;
            if (tickXpText != null)
                tickXpText.raycastTarget = false;
        }

        void OnDestroy()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        }

        void OnEnable()
        {
            FuelProducerService.ProducerUpgraded += OnProducerEvent;
            FuelProducerService.ProductionApplied += OnProductionApplied;
            FuelProducerService.ProducerUnlocked += OnProducerUnlocked;
            EconomyDisplayEvents.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            FuelProducerService.ProducerUpgraded -= OnProducerEvent;
            FuelProducerService.ProductionApplied -= OnProductionApplied;
            FuelProducerService.ProducerUnlocked -= OnProducerUnlocked;
            EconomyDisplayEvents.Changed -= Refresh;
        }

        void OnProducerEvent(string id, int _) { if (IsMine(id)) Refresh(); }
        void OnProductionApplied(string id, float _) { if (IsMine(id)) Refresh(); }
        void OnProducerUnlocked(string id) { if (IsMine(id)) Refresh(); }

        bool IsMine(string producerId) =>
            _producer != null && _producer.ProducerId == producerId;

        void OnUpgradeClicked()
        {
            if (_producer == null)
                return;

            var producerId = _producer.ProducerId;
            if (!FuelProducerService.IsUnlocked(producerId))
                return;

            var cost = FuelProducerService.GetUpgradeCoinCost(producerId);
            if (cost <= 0)
                return;

            if (!FuelProducerService.CanAffordUpgrade(producerId))
            {
                UiRouter.OpenCoinsHub();
                return;
            }

            _producer.OnUpgradeButtonPressed();
        }

        public void Refresh()
        {
            if (_producer == null)
                return;

            var producerId = _producer.ProducerId;
            var def = FuelProducerService.ResolveDef(producerId);
            var level = FuelProducerService.GetProducerLevel(producerId);
            var maxLevel = def != null ? def.GetMaxProducerLevel() : level;
            var cost = FuelProducerService.GetUpgradeCoinCost(producerId);
            var tickXp = FuelProducerService.GetDisplayTickXp(producerId);

            if (levelText != null)
                levelText.text = string.Format(levelFormat, level);

            if (tickXpText != null)
            {
                var isManual = def == null || def.isManual;
                tickXpText.text = string.Format(
                    isManual ? manualTickFormat : autoTickFormat,
                    FormatXp(tickXp));
            }

            if (upgradeCostText != null)
            {
                upgradeCostText.text = level >= maxLevel || cost <= 0
                    ? maxLevelCostText
                    : string.Format(upgradeCostFormat, FormatCoins(cost));
            }

            if (upgradeButton != null)
            {
                upgradeButton.interactable = FuelProducerService.IsUnlocked(producerId)
                                             && level < maxLevel
                                             && cost > 0;
            }
        }

        static string FormatXp(float value)
        {
            if (value >= 1_000_000f)
                return (value / 1_000_000f).ToString("0.#") + "M";
            if (value >= 10_000f)
                return (value / 1_000f).ToString("0.#") + "K";
            return Mathf.RoundToInt(value).ToString();
        }

        static string FormatCoins(long value)
        {
            if (value >= 1_000_000)
                return (value / 1_000_000f).ToString("0.#") + "M";
            if (value >= 10_000)
                return (value / 1_000f).ToString("0.#") + "K";
            return value.ToString();
        }

        static Text FindTextInChildren(Transform root, params string[] names)
        {
            if (root == null)
                return null;

            for (var i = 0; i < names.Length; i++)
            {
                var tr = root.Find(names[i]);
                if (tr == null)
                    continue;
                var text = tr.GetComponent<Text>();
                if (text != null)
                    return text;
            }

            var texts = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null)
                    continue;
                for (var n = 0; n < names.Length; n++)
                {
                    if (texts[i].name == names[n])
                        return texts[i];
                }
            }

            return null;
        }
    }
}
