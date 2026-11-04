using Game.DestroyCity.Config;
using Game.Economy;
using Game.UI;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.DestroyCity
{
    /// <summary>锁定态燃料生产器：世界空间 UGUI 解锁按钮 + 解锁条件文案。</summary>
    [DisallowMultipleComponent]
    public sealed class FuelProducerUnlockUi : MonoBehaviour
    {
        [SerializeField] private Button unlockButton;
        [SerializeField] private Text conditionText;
        [SerializeField] private Text buttonLabelText;
        [SerializeField] private string unlockButtonLabel = "解锁";
        [SerializeField] private string unlockedButtonLabel = "已解锁";

        FuelProducerController _producer;

        public void Bind(FuelProducerController producer)
        {
            _producer = producer;
            Refresh();
        }

        void Awake()
        {
            if (unlockButton == null)
                unlockButton = GetComponentInChildren<Button>(true);
            if (conditionText == null)
                conditionText = FindTextInChildren(transform, "Condition", "UnlockCondition", "条件");
            if (buttonLabelText == null && unlockButton != null)
                buttonLabelText = unlockButton.GetComponentInChildren<Text>(true);

            if (unlockButton != null)
            {
                unlockButton.onClick.AddListener(OnUnlockClicked);
                unlockButton.interactable = true;
            }

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
            if (conditionText != null)
                conditionText.raycastTarget = false;
        }

        void OnDestroy()
        {
            if (unlockButton != null)
                unlockButton.onClick.RemoveListener(OnUnlockClicked);
        }

        void OnEnable()
        {
            FuelProducerService.ProducerUnlocked += OnProducerUnlocked;
            EconomyDisplayEvents.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            FuelProducerService.ProducerUnlocked -= OnProducerUnlocked;
            EconomyDisplayEvents.Changed -= Refresh;
        }

        void OnProducerUnlocked(string id)
        {
            if (IsMine(id))
                Refresh();
        }

        bool IsMine(string producerId) =>
            _producer != null && _producer.ProducerId == producerId;

        void OnUnlockClicked()
        {
            if (_producer == null)
                return;

            var producerId = _producer.ProducerId;
            if (FuelProducerService.IsUnlocked(producerId))
                return;

            var def = FuelProducerService.ResolveDef(producerId);
            if (def == null)
                return;

            switch (def.unlockPolicy)
            {
                case FuelProducerUnlockPolicy.CoinOnce:
                    if (!FuelProducerService.CanAffordUnlock(producerId))
                    {
                        UiRouter.OpenCoinsHub();
                        return;
                    }
                    break;
                case FuelProducerUnlockPolicy.GemOnce:
                    if (!FuelProducerService.CanAffordUnlock(producerId))
                    {
                        UiRouter.OpenCoinsHub();
                        return;
                    }
                    break;
                case FuelProducerUnlockPolicy.FreeUnlockOnce:
                    UiRouter.OpenFuelProducerUnlockOffer(producerId);
                    return;
            }

            FuelProducerService.TryUnlock(producerId, _ => Refresh());
        }

        public void TryUnlockFromPrompt()
        {
            if (_producer == null || unlockButton == null)
                return;

            OnUnlockClicked();
        }

        public void Refresh()
        {
            if (_producer == null)
                return;

            var producerId = _producer.ProducerId;
            var unlocked = FuelProducerService.IsUnlocked(producerId);
            gameObject.SetActive(!unlocked);

            if (unlocked)
                return;

            if (conditionText != null)
                conditionText.text = FuelProducerService.GetUnlockConditionText(producerId);

            if (unlockButton != null)
                unlockButton.interactable = true;

            if (buttonLabelText != null)
                buttonLabelText.text = ResolveButtonLabel(producerId);
        }

        string ResolveButtonLabel(string producerId)
        {
            var def = FuelProducerService.ResolveDef(producerId);
            if (def == null)
                return unlockButtonLabel;

            switch (def.unlockPolicy)
            {
                case FuelProducerUnlockPolicy.CoinOnce:
                    return $"金币: {FormatUnlockCost(def.unlockCoinCost)}";
                case FuelProducerUnlockPolicy.GemOnce:
                    return $"钻石: {FormatUnlockCost(def.unlockGemCost)}";
                case FuelProducerUnlockPolicy.FreeUnlockOnce:
                    return unlockButtonLabel;
                default:
                    return unlockButtonLabel;
            }
        }

        static string FormatUnlockCost(long value)
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
