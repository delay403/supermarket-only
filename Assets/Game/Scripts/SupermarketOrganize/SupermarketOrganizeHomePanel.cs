using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Game.Audio;
using Game.SupermarketOrganize.Config;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>超市收纳模式内主页：体力条、场景难度按钮、进场动画。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeHomePanel : MonoBehaviour, IUiPanelController
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private SupermarketOrganizeHomeConfig homeConfig;
        [SerializeField] private Text staminaText;
        [SerializeField] private Text staminaRegenText;
        [SerializeField] private Button staminaAddButton;
        [SerializeField] private SupermarketOrganizeHomeDifficultyButton[] difficultyButtons;
        [SerializeField] private SupermarketOrganizeHomeDetailPopup detailPopup;
        [SerializeField] private Button detailMaskButton;
        [SerializeField] private GameObject detailMaskRoot;
        [SerializeField] private RectTransform entryCharacterRect;
        [Tooltip("进场动画默认时长；条目未配置时使用。")]
        [SerializeField] private float defaultEntryAnimationSeconds = 3f;

        readonly List<SupermarketOrganizeHomeDifficultyButton> _buttons = new List<SupermarketOrganizeHomeDifficultyButton>();
        SupermarketOrganizeHomeDifficultyButton _selectedButton;
        bool _entryPlaying;
        float _nextStaminaTick;
        Vector2 _characterStartAnchoredPosition;
        bool _characterStartCaptured;

        public UiPanelId PanelId => UiPanelId.SupermarketOrganizeHome;

        public bool IsEntryPlaying => _entryPlaying;

        void Awake()
        {
            ResolveEntryCharacterRect();
            ResolveStaminaUiRefs();
            detailMaskButton?.onClick.AddListener(CloseDetailPopup);
            detailPopup?.Bind(OnStartGameClicked, CloseDetailPopup);
            staminaAddButton?.onClick.AddListener(OnStaminaAddClicked);
            if (root != null)
                root.SetActive(true);
        }

        void ResolveStaminaUiRefs()
        {
            if (staminaAddButton == null)
            {
                var staminaRoot = transform.Find("Root/MainPanel/Stamina")
                                  ?? transform.Find("MainPanel/Stamina")
                                  ?? transform.Find("Stamina");
                if (staminaRoot != null)
                {
                    var add = staminaRoot.Find("Add");
                    if (add != null)
                        staminaAddButton = add.GetComponent<Button>();
                }
            }

            if (staminaRegenText == null)
            {
                var staminaRoot = transform.Find("Root/MainPanel/Stamina")
                                  ?? transform.Find("MainPanel/Stamina")
                                  ?? transform.Find("Stamina");
                if (staminaRoot != null)
                {
                    var regen = staminaRoot.Find("RegenLabel")
                                ?? staminaRoot.Find("RegenText")
                                ?? staminaRoot.Find("TimerLabel");
                    if (regen != null)
                        staminaRegenText = regen.GetComponent<Text>();
                }
            }

            if (staminaAddButton != null)
            {
                staminaAddButton.onClick.RemoveListener(OnStaminaAddClicked);
                staminaAddButton.onClick.AddListener(OnStaminaAddClicked);
            }
        }

        void ResolveEntryCharacterRect()
        {
            if (entryCharacterRect != null)
                return;

            if (detailPopup != null)
            {
                var popupRoot = detailPopup.transform.Find("Root");
                if (popupRoot != null)
                {
                    var character = popupRoot.Find("Character");
                    if (character != null)
                        entryCharacterRect = character as RectTransform;
                }
            }

            if (entryCharacterRect == null)
            {
                var character = transform.Find("Root/MainPanel/Character")
                                ?? transform.Find("MainPanel/Character")
                                ?? transform.Find("Character");
                if (character != null)
                    entryCharacterRect = character as RectTransform;
            }
        }

        void OnEnable()
        {
            UiCoordinator.Register(this);
            SupermarketOrganizeStaminaService.Changed += OnStaminaChanged;
            BindSceneButtons();
            RefreshStaminaDisplay();
            CloseDetailPopup();
            SetHomeInteractable(true);
        }

        void OnDisable()
        {
            SupermarketOrganizeStaminaService.Changed -= OnStaminaChanged;
            UiCoordinator.Unregister(this);
            RestoreAllButtonLayers();
        }

        void Update()
        {
            if (!gameObject.activeInHierarchy || Time.unscaledTime < _nextStaminaTick)
                return;

            _nextStaminaTick = Time.unscaledTime + 0.5f;
            RefreshStaminaDisplay();
        }

        public void Open(UiOpenPayload payload = null)
        {
            if (root != null)
                root.SetActive(true);

            BindSceneButtons();
            RefreshStaminaDisplay();
            CloseDetailPopup();
            SetHomeInteractable(true);
            CaptureCharacterStartPosition();
            if (entryCharacterRect != null)
                entryCharacterRect.gameObject.SetActive(false);
        }

        public void Close()
        {
            if (root != null)
                root.SetActive(false);
        }

        public void ShowOnSceneStart() => Open();

        void OnStaminaChanged()
        {
            RefreshStaminaDisplay();
            detailPopup?.Refresh();
        }

        void OnStaminaAddClicked()
        {
            if (_entryPlaying)
                return;

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenSupermarketOrganizeStamina();
        }

        void BindSceneButtons()
        {
            homeConfig ??= SupermarketOrganizeHomeConfig.ResolveRuntime();
            _buttons.Clear();

            if (difficultyButtons != null && difficultyButtons.Length > 0)
            {
                for (var i = 0; i < difficultyButtons.Length; i++)
                {
                    if (difficultyButtons[i] != null)
                        _buttons.Add(difficultyButtons[i]);
                }
            }
            else
            {
                GetComponentsInChildren(true, _buttons);
            }

            for (var i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                if (button == null)
                    continue;

                button.CaptureLayerState();
                if (homeConfig.TryGetEntry(button.EntryId, out var entry))
                    button.Bind(entry, OnDifficultyClicked);
                else
                    Debug.LogWarning(
                        $"[SupermarketOrganizeHomePanel] 未找到难度配置：{button.EntryId}",
                        button);
            }
        }

        void OnDifficultyClicked(SupermarketOrganizeHomeDifficultyButton button)
        {
            if (_entryPlaying || button?.Entry == null)
                return;

            if (_selectedButton == button && detailPopup != null && detailPopup.IsVisible)
            {
                CloseDetailPopup();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            _selectedButton = button;

            for (var i = 0; i < _buttons.Count; i++)
                _buttons[i]?.SetSelected(_buttons[i] == button);

            RestoreAllButtonLayers();
            button.RaiseAboveMask(entryCharacterRect);

            var difficultyManager = SupermarketOrganizeDifficultyManager.ResolveRuntime();
            difficultyManager?.ApplyDifficulty(button.EntryId);

            var shelfCount = 0;
            var itemCount = 0;
            if (!SupermarketOrganizeDifficultyManager.TryGetStats(button.EntryId, out shelfCount, out itemCount))
            {
                Debug.LogWarning(
                    $"[SupermarketOrganizeHomePanel] 未找到难度货架配置：{button.EntryId}",
                    button);
            }

            if (detailPopup != null)
            {
                detailPopup.Show(
                    button.Entry,
                    button.RectTransform,
                    button.DetailPopupOffset,
                    shelfCount,
                    itemCount);
            }

            if (detailMaskRoot != null)
                detailMaskRoot.SetActive(true);
        }

        void CloseDetailPopup(bool clearSelection = true)
        {
            detailPopup?.Hide();
            if (detailMaskRoot != null)
                detailMaskRoot.SetActive(false);

            if (clearSelection)
                SupermarketOrganizeDifficultyManager.ResolveRuntime()?.HideAll();

            RestoreAllButtonLayers();

            for (var i = 0; i < _buttons.Count; i++)
                _buttons[i]?.SetSelected(false);

            if (clearSelection)
                _selectedButton = null;
        }

        void CloseDetailPopup() => CloseDetailPopup(true);

        void CloseDetailPopupForEntryAnimation()
        {
            detailPopup?.HideOverlayKeepRoot();
            if (detailMaskRoot != null)
                detailMaskRoot.SetActive(false);

            RestoreAllButtonLayers();
        }

        void RestoreAllButtonLayers()
        {
            for (var i = 0; i < _buttons.Count; i++)
                _buttons[i]?.RestoreLayer();
        }

        void OnStartGameClicked(SupermarketOrganizeDifficultyEntryDef entry)
        {
            if (_entryPlaying || entry == null)
                return;

            var cost = Mathf.Max(1, entry.staminaCost);
            if (!SupermarketOrganizeStaminaService.TryConsumeForRun(cost))
            {
                AudioGameplayFeedback.PlayUiError();
                detailPopup?.Refresh();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            var selectedButton = _selectedButton;
            CloseDetailPopupForEntryAnimation();
            StartCoroutine(PlayEntryAndBeginGame(entry, selectedButton));
        }

        IEnumerator PlayEntryAndBeginGame(
            SupermarketOrganizeDifficultyEntryDef entry,
            SupermarketOrganizeHomeDifficultyButton selectedButton)
        {
            _entryPlaying = true;
            SetHomeInteractable(false);

            var duration = entry.entryAnimationSeconds > 0f
                ? entry.entryAnimationSeconds
                : defaultEntryAnimationSeconds;

            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            director?.BeginDeferredGameplay(entry);

            if (entryCharacterRect != null && selectedButton != null)
            {
                EnsureCharacterHierarchyActive();
                CaptureCharacterStartPosition();

                entryCharacterRect.gameObject.SetActive(true);
                entryCharacterRect.anchoredPosition = _characterStartCaptured
                    ? _characterStartAnchoredPosition
                    : entryCharacterRect.anchoredPosition;

                var end = selectedButton.ResolveEntryAnimationTarget(
                    entryCharacterRect.parent as RectTransform);

                yield return entryCharacterRect
                    .DOAnchorPos(end, Mathf.Max(0.5f, duration))
                    .SetEase(Ease.InOutQuad)
                    .SetUpdate(true)
                    .WaitForCompletion();
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Max(0.5f, duration));
            }

            _entryPlaying = false;
            _selectedButton = null;
            if (entryCharacterRect != null)
                entryCharacterRect.gameObject.SetActive(false);
            Close();
        }

        void CaptureCharacterStartPosition()
        {
            if (entryCharacterRect == null)
                return;

            _characterStartAnchoredPosition = entryCharacterRect.anchoredPosition;
            _characterStartCaptured = true;
        }

        void EnsureCharacterHierarchyActive()
        {
            if (entryCharacterRect == null)
                return;

            var current = entryCharacterRect.transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    current.gameObject.SetActive(true);
                current = current.parent;
            }
        }

        void RefreshStaminaDisplay()
        {
            ResolveStaminaUiRefs();
            ApplyStaminaUiVisibility();

            if (!SupermarketOrganizeStaminaService.IsEnabled)
                return;

            SupermarketOrganizeStaminaService.RefreshRegen();

            if (staminaText != null)
            {
                homeConfig ??= SupermarketOrganizeHomeConfig.ResolveRuntime();
                var current = SupermarketOrganizeStaminaService.Current;
                var max = SupermarketOrganizeStaminaService.MaxStamina;
                var format = string.IsNullOrWhiteSpace(homeConfig.staminaTextFormat)
                    ? "{0}"
                    : homeConfig.staminaTextFormat;

                try
                {
                    staminaText.text = string.Format(format, current, max);
                }
                catch (FormatException)
                {
                    staminaText.text = current.ToString();
                }
            }

            if (staminaRegenText != null)
            {
                if (SupermarketOrganizeStaminaService.IsFull)
                    staminaRegenText.text = "体力已满";
                else
                {
                    var wait = SupermarketOrganizeStaminaService.TimeUntilNextRegen;
                    staminaRegenText.text = $"{wait.Minutes:D2}:{wait.Seconds:D2}";
                }
            }

            if (staminaAddButton != null)
                staminaAddButton.interactable = !SupermarketOrganizeStaminaService.IsFull && !_entryPlaying;
        }

        void ApplyStaminaUiVisibility()
        {
            var enabled = SupermarketOrganizeStaminaService.IsEnabled;
            if (staminaText != null)
                staminaText.transform.parent?.gameObject.SetActive(enabled);

            if (staminaRegenText != null)
                staminaRegenText.gameObject.SetActive(enabled);

            if (staminaAddButton != null)
                staminaAddButton.gameObject.SetActive(enabled);
        }

        void SetHomeInteractable(bool interactable)
        {
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.interactable = interactable;
                rootCanvasGroup.blocksRaycasts = interactable;
            }
        }
    }
}
