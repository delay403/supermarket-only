using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.DestroyCity;
using Game.Hud;
using Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Interaction
{
    /// <summary>
    /// 摧毁城市等玩法：进入独立交互区域后显示对应图标的 HUD 按钮，点击后再执行交互。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(90)]
    public sealed class WorldInteractionPromptController : MonoBehaviour
    {
        public static WorldInteractionPromptController Instance { get; private set; }

        [Tooltip("为 true 时 3D 可交互物改由 HUD 按钮触发；世界空间 UGUI 仍走射线点击。")]
        [SerializeField] private bool suppressWorldInteractableRaycast = true;
        [SerializeField] private Transform interactorRoot;
        [SerializeField] private Button interactButton;
        [SerializeField] private Image interactButtonIcon;

        struct ActiveArea
        {
            public IWorldInteractionArea Area;
            public Transform Root;
        }

        readonly List<ActiveArea> _activeAreas = new List<ActiveArea>(8);
        IWorldInteractable _selectedInteractable;
        IWorldInteractionZoneHandler _selectedHandler;
        Transform _selectedInteractorRoot;
        Sprite _selectedIcon;
        WorldRaycastInteractor _raycastInteractor;
        float _nextRefreshUnscaled;

        public static bool UseButtonPromptMode =>
            Instance != null && Instance.isActiveAndEnabled;

        public static bool ShouldSuppressWorldInteractableRaycast =>
            UseButtonPromptMode && Instance.suppressWorldInteractableRaycast;

        public static void NotifyAreaOffersChanged(IWorldInteractionArea area)
        {
            if (Instance == null || area == null)
                return;

            for (var i = 0; i < Instance._activeAreas.Count; i++)
            {
                if (Instance._activeAreas[i].Area != area)
                    continue;

                Instance.RefreshSelection();
                return;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (interactorRoot == null)
                interactorRoot = transform;

            ResolveInteractButton();
            SetInteractPrompt(null, false);
            ApplyRaycastInteractorMode();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            ResolveInteractButton();
            ApplyRaycastInteractorMode();
            RefreshSelection();
        }

        void Update()
        {
            if (_activeAreas.Count == 0 || Time.unscaledTime < _nextRefreshUnscaled)
                return;

            _nextRefreshUnscaled = Time.unscaledTime + 0.15f;
            RefreshSelection();
        }

        void OnDisable()
        {
            _activeAreas.Clear();
            ClearSelection();
            SetInteractPrompt(null, false);
        }

        public void BindInteractButton(Button button, Image icon = null)
        {
            if (interactButton != button)
            {
                if (interactButton != null)
                    interactButton.onClick.RemoveListener(OnInteractButtonClicked);

                interactButton = button;
                if (interactButton != null)
                    interactButton.onClick.AddListener(OnInteractButtonClicked);
            }

            if (icon != null)
                interactButtonIcon = icon;

            ResolveInteractIcon();
            SetInteractPrompt(null, false);
        }

        public void RegisterArea(IWorldInteractionArea area, Transform root)
        {
            if (area == null || root == null)
                return;

            for (var i = 0; i < _activeAreas.Count; i++)
            {
                if (_activeAreas[i].Area == area && _activeAreas[i].Root == root)
                    return;
            }

            _activeAreas.Add(new ActiveArea { Area = area, Root = root });
            RefreshSelection();
        }

        public void UnregisterArea(IWorldInteractionArea area, Transform root)
        {
            if (area == null || root == null)
                return;

            for (var i = _activeAreas.Count - 1; i >= 0; i--)
            {
                if (_activeAreas[i].Area != area || _activeAreas[i].Root != root)
                    continue;

                _activeAreas.RemoveAt(i);
            }

            RefreshSelection();
        }

        void RefreshSelection()
        {
            _selectedInteractable = null;
            _selectedHandler = null;
            _selectedInteractorRoot = null;
            _selectedIcon = null;

            IWorldInteractable bestInteractable = null;
            IWorldInteractionZoneHandler bestHandler = null;
            Transform bestRoot = null;
            Sprite bestIcon = null;
            var bestScore = float.MinValue;

            for (var i = 0; i < _activeAreas.Count; i++)
            {
                var entry = _activeAreas[i];
                if (entry.Area == null || entry.Root == null)
                    continue;

                if (!entry.Area.TrySelectInteraction(
                        entry.Root,
                        out var interactable,
                        out var handler,
                        out var icon,
                        out var priority))
                    continue;

                var areaPos = entry.Area.InteractionTransform != null
                    ? entry.Area.InteractionTransform.position
                    : entry.Root.position;
                var distance = Vector3.Distance(entry.Root.position, areaPos);
                var score = priority * 10000f - distance;

                if (score < bestScore)
                    continue;

                bestScore = score;
                bestInteractable = interactable;
                bestHandler = handler;
                bestRoot = entry.Root;
                bestIcon = icon;
            }

            _selectedInteractable = bestInteractable;
            _selectedHandler = bestHandler;
            _selectedInteractorRoot = bestRoot;
            _selectedIcon = bestIcon;

            var visible = _selectedInteractable != null || _selectedHandler != null;
            SetInteractPrompt(_selectedIcon, visible);
        }

        void OnInteractButtonClicked()
        {
            if (_selectedInteractorRoot == null)
                return;

            if (_selectedInteractable != null)
            {
                var camera = Camera.main;
                var ctx = WorldInteractionContext.CreateForInteractor(
                    camera,
                    _selectedInteractorRoot,
                    _selectedInteractable);
                if (_selectedInteractable.CanInteract(ctx))
                {
                    AudioGameplayFeedback.PlayInteract();
                    _selectedInteractable.Interact(ctx);
                }
            }
            else if (_selectedHandler != null && _selectedHandler.CanInteract(_selectedInteractorRoot))
            {
                AudioGameplayFeedback.PlayInteract();
                _selectedHandler.Interact(_selectedInteractorRoot);
            }

            RefreshSelection();
        }

        void SetInteractPrompt(Sprite icon, bool visible)
        {
            if (interactButton == null)
                ResolveInteractButton();

            var hud = FindObjectOfType<GameHudUguiPanel>(true);
            if (hud != null)
            {
                hud.SetInteractPrompt(icon, visible);
                return;
            }

            if (interactButton != null)
                interactButton.gameObject.SetActive(visible);

            if (interactButtonIcon != null && icon != null)
                interactButtonIcon.sprite = icon;
        }

        void ResolveInteractButton()
        {
            if (interactButton != null)
            {
                interactButton.onClick.RemoveListener(OnInteractButtonClicked);
                interactButton.onClick.AddListener(OnInteractButtonClicked);
                ResolveInteractIcon();
                return;
            }

            var hud = FindObjectOfType<GameHudUguiPanel>(true);
            if (hud != null && hud.InteractButton != null)
                BindInteractButton(hud.InteractButton, hud.InteractButtonIcon);
        }

        void ResolveInteractIcon()
        {
            if (interactButtonIcon != null || interactButton == null)
                return;

            interactButtonIcon = interactButton.targetGraphic as Image;
            if (interactButtonIcon == null)
                interactButtonIcon = interactButton.GetComponent<Image>();
        }

        public static WorldInteractionPromptController EnsureForPlayer()
        {
            if (Instance != null)
                return Instance;

            var motor = FindObjectOfType<PlayerMotor>();
            if (motor == null)
                return null;

            var controller = motor.GetComponent<WorldInteractionPromptController>();
            if (controller == null)
                controller = motor.gameObject.AddComponent<WorldInteractionPromptController>();

            return controller;
        }

        void ApplyRaycastInteractorMode()
        {
            if (!GameProductSettings.IsDestroyCityContentEnabled)
                return;

            _raycastInteractor ??= GetComponent<WorldRaycastInteractor>();
            if (_raycastInteractor == null)
                _raycastInteractor = GetComponentInParent<WorldRaycastInteractor>();

            if (_raycastInteractor != null && !_raycastInteractor.enabled)
                _raycastInteractor.enabled = true;
        }

        public static bool IsPlayerCollider(Collider other)
        {
            if (other == null)
                return false;

            if (other.CompareTag("Player"))
                return true;

            return other.GetComponentInParent<PlayerMotor>() != null
                   || other.GetComponentInParent<PlayerDestroyCityController>() != null
                   || other.GetComponentInParent<PlayerFuelCarryController>() != null;
        }

        public static Transform ResolveInteractorRoot(Collider other)
        {
            if (other == null)
                return null;

            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor != null)
                return motor.transform;

            var destroyCity = other.GetComponentInParent<PlayerDestroyCityController>();
            if (destroyCity != null)
                return destroyCity.transform;

            return other.transform.root;
        }

        void ClearSelection()
        {
            _selectedInteractable = null;
            _selectedHandler = null;
            _selectedInteractorRoot = null;
            _selectedIcon = null;
        }
    }
}
