using Game.Reward;
using Game.Audio;
using Game.Core;
using Game.Maps;
using Game.UI;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>
    /// 超市收纳模式专用：绑定设置面板里的「重新生成地面物品」按钮；
    /// 单玩法包体（无 MainHome）下把设置面板的「返回主页」接管为就地回到模式内主页。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeSettingsFeatureBridge : MonoBehaviour
    {
        [Tooltip("设置面板中「重新生成地面物品」整行 UI（含文案与按钮）。")]
        [SerializeField] private GameObject groundShuffleRow;

        [Tooltip("免费打乱地面物品的按钮；留空则从整行下自动查找 Button。")]
        [SerializeField] private Button groundShuffleButton;

        [Tooltip("可选：用于判断设置面板是否打开；留空则自动查找 SettingsPanel。")]
        [SerializeField] private SettingsPanel settingsPanel;

        bool _buttonWired;
        bool _returnHomeWired;
        bool _gameplayStateBound;
        bool _lastShouldShow;
        bool _hasLastShouldShow;
        SupermarketOrganizeDirector _director;
        SupermarketOrganizeService _service;

        void Awake()
        {
            if (groundShuffleRow != null)
                groundShuffleRow.SetActive(false);

            WireActionButton();
            WireSettingsReturnHome();
        }

        void OnEnable()
        {
            WireSettingsReturnHome();
            TryBindGameplayEvents();
            ApplyVisibility(force: true);
        }

        void OnDisable() => UnbindGameplayEvents();

        void Update()
        {
            if (!GameplayModuleContext.IsModuleActive(GameplayModule.SupermarketOrganize))
                return;

            TryBindGameplayEvents();
            ApplyVisibility();
        }

        void WireActionButton()
        {
            if (_buttonWired)
                return;

            if (groundShuffleButton == null && groundShuffleRow != null)
                groundShuffleButton = groundShuffleRow.GetComponentInChildren<Button>(true);

            if (groundShuffleButton == null)
                return;

            groundShuffleButton.onClick.AddListener(OnGroundShuffleClicked);
            _buttonWired = true;
        }

        void WireSettingsReturnHome()
        {
            if (_returnHomeWired)
                return;

            var panel = ResolveSettingsPanel();
            if (panel == null)
                return;

            // 有 MainHome 的融合包体保持默认跳转；单玩法包体（MainHome 未启用）才接管为回模式内主页。
            var cfg = SceneNavigationSettings.ResolveActive();
            if (cfg != null && cfg.useMainHomeScene)
                return;

            panel.SetReturnHomeOverride(OnSettingsReturnHome);
            _returnHomeWired = true;
        }

        void TryBindGameplayEvents()
        {
            if (_gameplayStateBound)
                return;

            _director = SupermarketOrganizeDirector.Instance
                        ?? FindObjectOfType<SupermarketOrganizeDirector>();
            _service = _director?.Service;
            if (_service == null)
                return;

            _service.GameStarted += OnGameplayStateChanged;
            _gameplayStateBound = true;
        }

        void UnbindGameplayEvents()
        {
            if (!_gameplayStateBound || _service == null)
                return;

            _service.GameStarted -= OnGameplayStateChanged;
            _gameplayStateBound = false;
            _service = null;
            _director = null;
            _hasLastShouldShow = false;
        }

        void OnGameplayStateChanged() => ApplyVisibility(force: true);

        void OnSettingsReturnHome()
        {
            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            if (director != null)
            {
                // 中途退出本局：就地回到场景内主页（难度选择），不切场景。
                director.ShowHome();
                return;
            }

            UiRouter.OpenSupermarketOrganizeHome();
        }

        void ApplyVisibility(bool force = false)
        {
            if (groundShuffleRow == null)
                return;

            var shouldShow = GameplayModuleContext.IsModuleActive(GameplayModule.SupermarketOrganize)
                             && IsSettingsVisible()
                             && IsChallengeGameplayActive();

            if (!force && _hasLastShouldShow && shouldShow == _lastShouldShow)
                return;

            _lastShouldShow = shouldShow;
            _hasLastShouldShow = true;

            if (groundShuffleRow.activeSelf != shouldShow)
                groundShuffleRow.SetActive(shouldShow);
        }

        bool IsChallengeGameplayActive()
        {
            _director ??= SupermarketOrganizeDirector.Instance
                         ?? FindObjectOfType<SupermarketOrganizeDirector>();
            return _director != null
                   && _director.HasChallengeStarted
                   && _director.Service != null
                   && _director.Service.IsGameRunning;
        }

        bool IsSettingsVisible()
        {
            var panel = ResolveSettingsPanel();
            if (panel == null)
                return false;

            if (panel.root != null)
                return panel.root.activeInHierarchy;

            return panel.isActiveAndEnabled;
        }

        SettingsPanel ResolveSettingsPanel()
        {
            if (settingsPanel != null)
                return settingsPanel;

#if UNITY_2023_1_OR_NEWER
            settingsPanel = FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
#else
            settingsPanel = FindObjectOfType<SettingsPanel>(true);
#endif
            return settingsPanel;
        }

        void OnGroundShuffleClicked()
        {
            AudioGameplayFeedback.PlayUiButton();

            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            if (director == null)
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiError);
                return;
            }

            FreeRewardGate.Grant(() =>
            {
                if (director.TryShuffleGroundItems())
                    AudioGameplayFeedback.PlayUiButton();
                else
                    AudioManager.Instance?.PlaySfx(SfxId.UiError);
            });
        }

        void OnDestroy()
        {
            UnbindGameplayEvents();

            if (groundShuffleButton != null && _buttonWired)
                groundShuffleButton.onClick.RemoveListener(OnGroundShuffleClicked);

            if (_returnHomeWired)
            {
                var panel = ResolveSettingsPanel();
                if (panel != null)
                    panel.SetReturnHomeOverride(null);
            }
        }
    }
}
