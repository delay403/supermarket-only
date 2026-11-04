using Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>准星提示 + 拾取/放置统一按钮 + 切换手持物品按钮。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeInteractionHud : MonoBehaviour
    {
        [SerializeField] private SupermarketCrosshairUi crosshairUi;
        [SerializeField] private SupermarketAimHintUi aimHintUi;
        [SerializeField] private SupermarketShelfFeedbackUi shelfFeedbackUi;
        [SerializeField] private Button interactButton;
        [SerializeField] private Text interactButtonLabel;
        [SerializeField] private Button switchItemButton;
        [SerializeField] private Text currentHeldItemNameText;

        SupermarketCrosshairInteractor _interactor;
        SupermarketOrganizeService _service;

        void Awake()
        {
            interactButton?.onClick.AddListener(OnInteractClicked);
            switchItemButton?.onClick.AddListener(OnSwitchItemClicked);
        }

        void OnEnable() => BindService();

        void OnDisable() => UnbindService();

        void OnDestroy()
        {
            interactButton?.onClick.RemoveListener(OnInteractClicked);
            switchItemButton?.onClick.RemoveListener(OnSwitchItemClicked);
            UnbindInteractor();
            UnbindService();
        }

        void BindService()
        {
            UnbindService();
            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            _service = director?.Service;
            if (_service == null)
                return;

            _service.HandleCountChanged += OnHandleCountChanged;
            _service.CurrentItemChanged += OnCurrentItemChanged;
            RefreshSwitchButton();
            RefreshHeldItemName(_service.CurrentItem);
        }

        void UnbindService()
        {
            if (_service == null)
                return;

            _service.HandleCountChanged -= OnHandleCountChanged;
            _service.CurrentItemChanged -= OnCurrentItemChanged;
            _service = null;
        }

        void OnHandleCountChanged(int _, int __)
        {
            RefreshSwitchButton();
            RefreshHeldItemName(_service?.CurrentItem);
        }

        void OnCurrentItemChanged(SupermarketItem item)
        {
            RefreshSwitchButton();
            RefreshHeldItemName(item);
        }

        public void BindInteractor(SupermarketCrosshairInteractor interactor)
        {
            UnbindInteractor();
            _interactor = interactor;
            if (_interactor != null)
                _interactor.AimStateChanged += OnAimStateChanged;

            RefreshFromInteractor();
        }

        void UnbindInteractor()
        {
            if (_interactor != null)
                _interactor.AimStateChanged -= OnAimStateChanged;
            _interactor = null;
        }

        void Update()
        {
            if (_service == null)
                BindService();

            if (_interactor != null)
                return;

            _interactor = FindObjectOfType<SupermarketCrosshairInteractor>();
            if (_interactor != null)
                BindInteractor(_interactor);
        }

        void OnInteractClicked()
        {
            if (_interactor == null)
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiError);
                return;
            }

            var state = _interactor.CurrentAimState;
            if (state.canPlace)
            {
                if (!_interactor.TryPlace())
                    AudioManager.Instance?.PlaySfx(SfxId.UiError);
                return;
            }

            if (state.canPickUp)
            {
                if (!_interactor.TryPickUp())
                    AudioManager.Instance?.PlaySfx(SfxId.UiError);
                return;
            }

            AudioManager.Instance?.PlaySfx(SfxId.UiError);
        }

        void OnSwitchItemClicked()
        {
            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            var service = director?.Service;
            if (service == null || !service.TryCycleCurrentHeldItem())
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiError);
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            RefreshSwitchButton();
            RefreshHeldItemName(director?.Service?.CurrentItem);
        }

        void OnAimStateChanged(SupermarketAimState state) => ApplyState(state);

        void RefreshFromInteractor()
        {
            if (_interactor == null)
            {
                ApplyState(default);
                return;
            }

            ApplyState(_interactor.CurrentAimState);
            RefreshSwitchButton();
        }

        void ApplyState(SupermarketAimState state)
        {
            crosshairUi?.ApplyAimState(state);
            aimHintUi?.ApplyAimState(state);

            var showInteract = state.canPickUp || state.canPlace;
            if (interactButton != null)
                interactButton.gameObject.SetActive(showInteract);

            if (interactButtonLabel != null)
            {
                if (state.canPlace)
                    interactButtonLabel.text = string.IsNullOrWhiteSpace(state.placeLabel) ? "放置" : state.placeLabel;
                else if (state.canPickUp)
                    interactButtonLabel.text = string.IsNullOrWhiteSpace(state.pickUpLabel) ? "拾取" : state.pickUpLabel;
            }

            if (shelfFeedbackUi != null)
            {
                if (state.showShelfFeedback)
                    shelfFeedbackUi.ShowShelfMatch(state.shelfIsCorrect, state.showShelfExtraTip);
                else
                    shelfFeedbackUi.Hide();
            }

            RefreshSwitchButton();
            RefreshHeldItemName(_service?.CurrentItem);
        }

        void RefreshSwitchButton()
        {
            if (switchItemButton == null)
                return;

            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            var count = director?.Service?.HandleZone?.Count ?? 0;
            switchItemButton.gameObject.SetActive(count > 1);
            switchItemButton.interactable = count > 1;
        }

        void RefreshHeldItemName(SupermarketItem item)
        {
            if (currentHeldItemNameText == null)
                return;

            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            var service = director?.Service ?? _service;
            var handleZone = service?.HandleZone;
            var current = handleZone != null && handleZone.Count > 0
                ? handleZone.CurrentItem ?? item
                : null;

            if (current == null)
            {
                currentHeldItemNameText.gameObject.SetActive(false);
                currentHeldItemNameText.text = string.Empty;
                return;
            }

            currentHeldItemNameText.gameObject.SetActive(true);
            currentHeldItemNameText.text =
                $"当前：{SupermarketItemDisplayNames.Resolve(current)}";
        }
    }
}
