using System;
using Game.Audio;
using Game.Collectibles.Characters;
using Game.Core;
using Game.Core.Config;
using Game.Economy;
using Game.Save;
using Game.UI.Framework;
using Game.UI.Preview;
using Game.UI.ViewModels;
using Game.WormDodge;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 躲避虫子模式内主页：角色选择（预览/解锁/使用）+ 开始游戏。
    /// 角色逻辑与 <see cref="CharacterPanel"/> 同源，但不改动原面板。
    /// </summary>
    public sealed class WormDodgeHomePanel : MonoBehaviour, IUiPanelController
    {
        public GameObject root;
        public RectTransform mainPanel;
        public RawImage characterPreviewImage;
        public Button prevCharacterButton;
        public Button nextCharacterButton;
        public Button unlockButton;
        public Text unlockButtonLabel;
        public Button useButton;
        public Text useButtonLabel;
        public Button startButton;
        public GameObject equippedBadgeRoot;
        public GameObject lockedIconRoot;
        public Text characterNameText;
        public Text unlockGateHint;
        public WormDodgeCharacterUnlockOverlay unlockOverlay;

        [Header("Animation")]
        public float slideDuration = 0.38f;
        public float settleDuration = 0.28f;
        public float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        CharacterShopScreenViewModel _vm;
        int _selectedIndex = -1;
        RawImageCamera _previewCameraBinder;

        public UiPanelId PanelId => UiPanelId.WormDodgeHome;

        void Awake()
        {
            TryAutoWireMissingReferences();
            prevCharacterButton?.onClick.AddListener(OnPrevCharacter);
            nextCharacterButton?.onClick.AddListener(OnNextCharacter);
            unlockButton?.onClick.AddListener(OnUnlockClicked);
            useButton?.onClick.AddListener(OnUseClicked);
            startButton?.onClick.AddListener(OnStartClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable()
        {
            UiCoordinator.Register(this);
            EconomyDisplayEvents.Changed += OnEconomyDisplayChanged;
        }

        void OnDisable()
        {
            EconomyDisplayEvents.Changed -= OnEconomyDisplayChanged;
            UiCoordinator.Unregister(this);
            CosmeticPreviewController.Instance?.LeaveShop();
        }

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null) => Show();

        public void Close() => Hide();

        void OnEconomyDisplayChanged()
        {
            if (slideHost == null || !slideHost.IsShown)
                return;

            RefreshActionButtons();
        }

        void OnPrevCharacter()
        {
            if (_vm == null || _vm.Rows.Count == 0)
                return;

            AudioGameplayFeedback.PlayUiButton();
            SelectIndex(WrapIndex(_selectedIndex - 1), userPulse: true);
        }

        void OnNextCharacter()
        {
            if (_vm == null || _vm.Rows.Count == 0)
                return;

            AudioGameplayFeedback.PlayUiButton();
            SelectIndex(WrapIndex(_selectedIndex + 1), userPulse: true);
        }

        void OnUnlockClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
                return;

            var row = _vm.Rows[_selectedIndex];
            if (row.Owned)
                return;

            if (!row.UnlockedForPurchase)
            {
                AudioGameplayFeedback.PlayUiError();
                return;
            }

            var canAfford = CanAfford(row);
            if (!canAfford)
            {
                if (row.PriceCurrency == CharacterCurrencyType.Gems)
                    UiRouter.OpenFreeDiamondsOffer();
                else
                    UiRouter.OpenFreeCoinsOffer();
                return;
            }

            if (unlockOverlay == null)
            {
                TryBuySelectedCharacter();
                return;
            }

            unlockOverlay.Show(row, true, TryBuySelectedCharacter);
        }

        void OnUseClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
                return;

            var row = _vm.Rows[_selectedIndex];
            if (!row.Owned)
            {
                AudioGameplayFeedback.PlayUiError();
                return;
            }

            if (string.Equals(_vm.EquippedId, row.Id, StringComparison.Ordinal))
                return;

            CharacterService.Equip(row.Id);
            SaveService.Save();
            UiCoordinator.RefreshEquipmentVisuals();
            RebuildViewModelAndRefresh(selectEquipped: true);
        }

        void OnStartClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (WormDodgeDirector.Instance == null)
                return;

            if (!WormDodgeStaminaService.CanStartRun())
            {
                AudioGameplayFeedback.PlayUiError();
                return;
            }

            if (WormDodgeDirector.Instance.TryStartRunFromHome())
                Hide();
        }

        void TryBuySelectedCharacter()
        {
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
                return;

            var row = _vm.Rows[_selectedIndex];
            if (!CharacterService.TryBuy(row.Id))
            {
                AudioGameplayFeedback.PlayUiError();
                return;
            }

            SaveService.Save();
            RebuildViewModelAndRefresh(selectEquipped: false);
            if (UiCoordinator.Hud != null)
                UiCoordinator.Hud.RefreshEconomyDisplay();
        }

        void Show()
        {
            EnsureSlideHost();
            slideHost.PlayOpen(PopulateAfterOpen);
        }

        void PopulateAfterOpen()
        {
            EnsurePreviewBinding();
            CosmeticPreviewController.Instance?.EnterCharacterHomePreview();
            RebuildViewModelAndRefresh(selectEquipped: true);
        }

        void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;

            slideHost.RequestClose(() => CosmeticPreviewController.Instance?.LeaveShop());
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
            CosmeticPreviewController.Instance?.LeaveShop();
        }

        void RebuildViewModelAndRefresh(bool selectEquipped)
        {
            _vm = CharacterShopScreenViewModel.Build();
            if (_vm.Rows.Count == 0)
            {
                _selectedIndex = -1;
                RefreshAllUi();
                return;
            }

            var idx = selectEquipped ? ResolveEquippedIndex() : Mathf.Clamp(_selectedIndex, 0, _vm.Rows.Count - 1);
            SelectIndex(idx, false);
        }

        void SelectIndex(int index, bool userPulse)
        {
            if (_vm == null || index < 0 || index >= _vm.Rows.Count)
                return;

            var changed = _selectedIndex != index;
            _selectedIndex = index;
            if (userPulse && changed)
                AudioGameplayFeedback.PlayUiSelect();

            var row = _vm.Rows[_selectedIndex];
            CosmeticPreviewController.Instance?.SetCharacterHomePreview(row.Id);
            RefreshAllUi();
        }

        void RefreshAllUi()
        {
            RefreshCharacterName();
            RefreshEquippedBadge();
            RefreshLockedIcon();
            RefreshActionButtons();
        }

        void RefreshCharacterName()
        {
            if (characterNameText == null)
                return;

            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
            {
                characterNameText.text = string.Empty;
                return;
            }

            characterNameText.text = _vm.Rows[_selectedIndex].DisplayName ?? string.Empty;
        }

        void RefreshEquippedBadge()
        {
            if (equippedBadgeRoot == null)
                return;

            var show = _vm != null && _selectedIndex >= 0 && _selectedIndex < _vm.Rows.Count &&
                       _vm.Rows[_selectedIndex].Owned &&
                       string.Equals(_vm.EquippedId, _vm.Rows[_selectedIndex].Id, StringComparison.Ordinal);
            equippedBadgeRoot.SetActive(show);
        }

        void RefreshLockedIcon()
        {
            if (lockedIconRoot == null)
                return;

            var show = _vm != null && _selectedIndex >= 0 && _selectedIndex < _vm.Rows.Count &&
                       !_vm.Rows[_selectedIndex].Owned;
            lockedIconRoot.SetActive(show);
        }

        void RefreshActionButtons()
        {
            if (_vm == null || _selectedIndex < 0 || _selectedIndex >= _vm.Rows.Count)
            {
                SetButtonState(unlockButton, unlockButtonLabel, false, string.Empty);
                SetButtonState(useButton, useButtonLabel, false, string.Empty);
                if (unlockGateHint != null)
                    unlockGateHint.gameObject.SetActive(false);
                if (lockedIconRoot != null)
                    lockedIconRoot.SetActive(false);
                return;
            }

            var row = _vm.Rows[_selectedIndex];
            var owned = row.Owned;
            var equipped = owned && string.Equals(_vm.EquippedId, row.Id, StringComparison.Ordinal);
            var canBuy = !owned && row.UnlockedForPurchase;
            var gated = !owned && !row.UnlockedForPurchase && row.RequiredUnlockedLevel > 0;

            if (unlockGateHint != null)
            {
                unlockGateHint.gameObject.SetActive(gated);
                if (gated)
                    unlockGateHint.text = $"需已拥有等级 ≥ {row.RequiredUnlockedLevel} 的角色";
            }

            if (!owned)
            {
                SetButtonState(unlockButton, unlockButtonLabel, canBuy, canBuy ? FormatUnlockPrice(row) : "未解锁");
                SetButtonState(useButton, useButtonLabel, false, "使用");
            }
            else if (equipped)
            {
                SetButtonState(unlockButton, unlockButtonLabel, false, string.Empty);
                SetButtonState(useButton, useButtonLabel, false, "使用中");
            }
            else
            {
                SetButtonState(unlockButton, unlockButtonLabel, false, string.Empty);
                SetButtonState(useButton, useButtonLabel, true, "使用");
            }

            RefreshEquippedBadge();
        }

        static void SetButtonState(Button button, Text label, bool interactable, string text)
        {
            if (button != null)
                button.interactable = interactable;
            if (label != null)
                label.text = text ?? string.Empty;
        }

        int ResolveEquippedIndex()
        {
            if (_vm == null || _vm.Rows.Count == 0)
                return 0;

            var eq = _vm.EquippedId;
            if (string.IsNullOrEmpty(eq))
                return 0;

            for (var i = 0; i < _vm.Rows.Count; i++)
            {
                if (string.Equals(_vm.Rows[i].Id, eq, StringComparison.Ordinal))
                    return i;
            }

            return 0;
        }

        int WrapIndex(int index)
        {
            var count = _vm.Rows.Count;
            if (count <= 0)
                return 0;

            index %= count;
            if (index < 0)
                index += count;
            return index;
        }

        static bool CanAfford(CharacterShopRowViewModel row)
        {
            if (SaveService.Data == null)
                return false;

            return row.PriceCurrency == CharacterCurrencyType.Gems
                ? SaveService.Data.gems >= row.PriceAmount
                : SaveService.Data.coins >= row.PriceAmount;
        }

        static string FormatUnlockPrice(CharacterShopRowViewModel row)
        {
            if (row == null)
                return "解锁";

            var currency = row.PriceCurrency == CharacterCurrencyType.Gems ? "钻石" : "金币";
            return $"解锁 {currency} x{row.PriceAmount}";
        }

        void EnsurePreviewBinding()
        {
            if (characterPreviewImage == null)
                return;

            _previewCameraBinder = characterPreviewImage.GetComponent<RawImageCamera>();
            if (_previewCameraBinder == null)
                _previewCameraBinder = characterPreviewImage.gameObject.AddComponent<RawImageCamera>();
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

        void TryAutoWireMissingReferences()
        {
            if (root == null)
            {
                var found = transform.Find("Root");
                if (found != null)
                    root = found.gameObject;
            }

            if (mainPanel == null && root != null)
            {
                var found = root.transform.Find("MainPanel");
                if (found != null)
                    mainPanel = found as RectTransform;
            }

            var searchRoot = mainPanel != null ? mainPanel : transform;
            if (characterPreviewImage == null)
            {
                characterPreviewImage = FindComponent<RawImage>(searchRoot, "CharacterPreview");
                if (characterPreviewImage == null)
                    characterPreviewImage = FindComponent<RawImage>(searchRoot, "RawImage");
            }

            if (prevCharacterButton == null)
            {
                prevCharacterButton = FindComponent<Button>(searchRoot, "PrevCharacterButton");
                if (prevCharacterButton == null)
                    prevCharacterButton = FindComponent<Button>(searchRoot, "Prev");
            }

            if (nextCharacterButton == null)
            {
                nextCharacterButton = FindComponent<Button>(searchRoot, "NextCharacterButton");
                if (nextCharacterButton == null)
                    nextCharacterButton = FindComponent<Button>(searchRoot, "Next");
            }

            if (unlockButton == null)
            {
                unlockButton = FindComponent<Button>(searchRoot, "UnlockButton");
                if (unlockButton == null)
                    unlockButton = FindComponent<Button>(searchRoot, "Unlock");
            }
            if (unlockButtonLabel == null && unlockButton != null)
                unlockButtonLabel = unlockButton.GetComponentInChildren<Text>(true);
            if (useButton == null)
            {
                useButton = FindComponent<Button>(searchRoot, "UseButton");
                if (useButton == null)
                    useButton = FindComponent<Button>(searchRoot, "Use");
            }
            if (useButtonLabel == null && useButton != null)
                useButtonLabel = useButton.GetComponentInChildren<Text>(true);
            if (startButton == null)
                startButton = FindComponent<Button>(searchRoot, "StartButton");
            if (characterNameText == null)
                characterNameText = FindComponent<Text>(searchRoot, "CharacterNameText");
            if (unlockGateHint == null)
                unlockGateHint = FindComponent<Text>(searchRoot, "UnlockGateHint");
            if (equippedBadgeRoot == null)
            {
                var badge = searchRoot.Find("EquippedBadge");
                if (badge != null)
                    equippedBadgeRoot = badge.gameObject;
            }

            if (lockedIconRoot == null)
            {
                var locked = searchRoot.Find("Locked");
                if (locked != null)
                    lockedIconRoot = locked.gameObject;
            }

            if (unlockOverlay == null)
                unlockOverlay = GetComponentInChildren<WormDodgeCharacterUnlockOverlay>(true);

            unlockOverlay?.TryAutoWire(transform);
        }

        static T FindComponent<T>(Transform parent, string childName) where T : Component
        {
            var child = parent.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
        }
    }
}
