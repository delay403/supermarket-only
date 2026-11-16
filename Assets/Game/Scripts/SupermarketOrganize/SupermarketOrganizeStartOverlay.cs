using Game.Audio;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeStartOverlay : MonoBehaviour, IUiPanelController
    {
        [SerializeField] GameObject root;
        [SerializeField] Text titleText;
        [SerializeField] Button startButton;
        [SerializeField] Button helpButton;
        [SerializeField] GameObject helpPopupRoot;
        [SerializeField] Button helpCloseButton;
        [SerializeField] Button helpMaskButton;

        bool _starting;

        public UiPanelId PanelId => UiPanelId.SupermarketOrganizeStart;

        void Awake()
        {
            startButton?.onClick.AddListener(OnStartClicked);
            helpButton?.onClick.AddListener(OnHelpClicked);
            helpCloseButton?.onClick.AddListener(OnHelpCloseClicked);
            helpMaskButton?.onClick.AddListener(OnHelpCloseClicked);
            SetHelpVisible(false);
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        public void Open(UiOpenPayload payload = null)
        {
            _starting = false;
            if (root != null)
                root.SetActive(true);

            SetHelpVisible(false);
            SetStartLayerInteractable(true);
        }

        public void Close()
        {
            SetHelpVisible(false);
            if (root != null)
                root.SetActive(false);
        }

        public void ShowOnSceneStart() => Open();

        void OnHelpClicked()
        {
            if (_starting)
                return;

            AudioGameplayFeedback.PlayUiButton();
            SetHelpVisible(true);
        }

        void OnHelpCloseClicked()
        {
            if (_starting)
                return;

            AudioGameplayFeedback.PlayUiButton();
            SetHelpVisible(false);
        }

        void OnStartClicked()
        {
            if (_starting)
                return;

            _starting = true;
            SetStartLayerInteractable(false);
            AudioGameplayFeedback.PlayUiButton();
            SetHelpVisible(false);
            Close();
            UiRouter.OpenSupermarketOrganizeHome();
            var home = FindHomePanel();
            home?.SetDifficultyLabelsVisible(true);
        }

        void SetHelpVisible(bool visible)
        {
            if (helpPopupRoot != null)
                helpPopupRoot.SetActive(visible);

            if (!_starting)
                SetStartLayerInteractable(!visible);
        }

        void SetStartLayerInteractable(bool interactable)
        {
            if (startButton != null)
                startButton.interactable = interactable;
            if (helpButton != null)
                helpButton.interactable = interactable;
        }

        static SupermarketOrganizeHomePanel FindHomePanel()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<SupermarketOrganizeHomePanel>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<SupermarketOrganizeHomePanel>(true);
#endif
        }
    }
}
