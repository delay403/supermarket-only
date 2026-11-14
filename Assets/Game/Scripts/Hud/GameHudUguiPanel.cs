using FairyGUI;
using Game.Economy;
using Game.Save;
using Game.UI;
using Game.UI.Effects;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hud
{
    public sealed class GameHudUguiPanel : MonoBehaviour
    {
        [SerializeField] UIPanel suppressFguiGameWnd;

        [Header("Shared")]
        [SerializeField] Button btnSetting;
        [SerializeField] Text coinsText;
        [SerializeField] RectTransform coinFlyTarget;
        [SerializeField] Text gemsText;
        [SerializeField] Button coinsPlusButton;
        [SerializeField] Button gemsPlusButton;
        [SerializeField] Button btnInteract;
        [SerializeField] UnityEngine.UI.Image btnInteractIcon;

        RollingNumberText _coinsRolling;
        RollingNumberText _gemsRolling;
        Sprite _defaultInteractIcon;

        public bool HasBindings => coinsText != null || gemsText != null;
        public Button InteractButton => btnInteract;
        public UnityEngine.UI.Image InteractButtonIcon => btnInteractIcon;

        void Awake()
        {
            _coinsRolling = EnsureRollingNumber(coinsText);
            _gemsRolling = EnsureRollingNumber(gemsText);
            if (btnInteractIcon != null)
                _defaultInteractIcon = btnInteractIcon.sprite;

            btnSetting?.onClick.AddListener(OnSettingsClicked);
            coinsPlusButton?.onClick.AddListener(OnCoinsHubClicked);
            gemsPlusButton?.onClick.AddListener(OnCoinsHubClicked);
            if (suppressFguiGameWnd != null)
                suppressFguiGameWnd.gameObject.SetActive(false);

            if (coinFlyTarget != null)
                UICoinFlyTarget.Register(coinFlyTarget, 10);
        }

        void Start()
        {
            ApplyProductModeVisibility();
            RefreshEconomyFromSave(true);
        }

        void OnDestroy()
        {
            btnSetting?.onClick.RemoveListener(OnSettingsClicked);
            coinsPlusButton?.onClick.RemoveListener(OnCoinsHubClicked);
            gemsPlusButton?.onClick.RemoveListener(OnCoinsHubClicked);
        }

        public void SetInteractPrompt(Sprite icon, bool visible)
        {
            if (btnInteract != null)
                btnInteract.gameObject.SetActive(visible);
            if (btnInteractIcon == null)
                return;
            if (visible && icon != null)
                btnInteractIcon.sprite = icon;
            else if (!visible && _defaultInteractIcon != null)
                btnInteractIcon.sprite = _defaultInteractIcon;
        }

        public void SetInteractButtonVisible(bool visible) => SetInteractPrompt(null, visible);

        public void SetEconomyDisplay(long coins, long gems, long medals = 0L, bool immediate = false)
        {
            SetNumber(_coinsRolling, coinsText, coins, immediate);
            SetNumber(_gemsRolling, gemsText, gems, immediate);
        }

        public void RefreshEconomyFromSave(bool immediate = false)
        {
            var data = SaveService.Data;
            if (data != null)
                SetEconomyDisplay(data.coins, data.gems, 0L, immediate);
        }

        public void AddCoinsToDisplay(long delta, bool immediate = true)
        {
            if (SaveService.Data != null)
                SetNumber(_coinsRolling, coinsText, SaveService.Data.coins, immediate);
        }

        public void ApplyProductModeVisibility()
        {
            var section = GetComponentInChildren<SupermarketOrganize.SupermarketOrganizeHudSection>(true);
            if (section != null)
                section.gameObject.SetActive(true);
            var skills = GetComponentInChildren<SupermarketOrganize.SupermarketOrganizeSkillHud>(true);
            if (skills != null)
                skills.gameObject.SetActive(true);
        }

        static RollingNumberText EnsureRollingNumber(Text label)
        {
            if (label == null)
                return null;
            var rolling = label.GetComponent<RollingNumberText>();
            return rolling != null ? rolling : label.gameObject.AddComponent<RollingNumberText>();
        }

        static void SetNumber(RollingNumberText rolling, Text label, long value, bool immediate)
        {
            if (rolling != null)
                rolling.AnimateTo(value, immediate);
            else if (label != null)
                label.text = EconomyDisplayFormat.Format(value);
        }

        static void OnSettingsClicked() => UiRouter.OpenSettings();
        static void OnCoinsHubClicked() => UiRouter.OpenCoinsHub();
    }
}
