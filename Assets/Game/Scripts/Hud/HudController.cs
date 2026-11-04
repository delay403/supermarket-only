using System.Collections;
using FairyGUI;
using Game.Core;
using Game.Economy;
using Game.Player;
using Game.Save;
using Game.Tower;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Hud
{
    public sealed class HudController : MonoBehaviour
    {
        [Header("HUD")]
        [Tooltip("优先使用 UGUI 主 HUD；未挂载时回退 FGUI GameWnd。")]
        [SerializeField] private GameHudUguiPanel uguiHud;

        [Header("FGUI（回退）")]
        [Tooltip("主界面 UIPanel（包 Game / 组件 GameWnd）。UGUI 启用后会被隐藏。")]
        [SerializeField] private UIPanel gameUIPanel;

        [Tooltip("用于跳跃/滑翔按钮；留空则自动查找场景中的 LegacyPlayerInput。")]
        [SerializeField] private LegacyPlayerInput jumpInput;

        private readonly GameHudFguiView _fgui = new GameHudFguiView();
        private long _lastCoins = long.MinValue;
        private long _lastGems = long.MinValue;
        private long _lastMedals = long.MinValue;

        private void Awake()
        {
            if (uguiHud == null)
                uguiHud = GetComponent<GameHudUguiPanel>();
            if (jumpInput == null)
                jumpInput = UnityObjectFinder.FindFirst<LegacyPlayerInput>();
        }

        bool UseUgui => uguiHud != null && uguiHud.HasBindings;

        private void OnEnable()
        {
            UiCoordinator.RegisterHud(this);
            EconomyDisplayEvents.Changed += OnEconomyDisplayChanged;
            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded -= OnGameplaySceneLoaded;
                SceneManager.sceneLoaded += OnGameplaySceneLoaded;
                StartCoroutine(BindHudWhenReady());
            }
        }

        void OnGameplaySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!isActiveAndEnabled)
                return;

            GameplayModuleContext.ApplyLoadedScene(scene);
            _fgui.Unbind();
            TryBindHud();
        }

        private IEnumerator BindHudWhenReady()
        {
            yield return null;
            TryBindHud();
        }

        private void Start() => TryBindHud();

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnGameplaySceneLoaded;
            EconomyDisplayEvents.Changed -= OnEconomyDisplayChanged;
            _fgui.Unbind();
            UiCoordinator.UnregisterHud(this);
        }

        void OnEconomyDisplayChanged() => SyncEconomyFromSave(force: false);

        private void TryBindHud()
        {
            if (!Application.isPlaying) return;

            if (uguiHud == null)
                uguiHud = GetComponent<GameHudUguiPanel>();

            if (UseUgui)
            {
                if (_fgui.IsBound)
                    _fgui.Unbind();
                var panel = gameUIPanel != null ? gameUIPanel : ResolveDefaultGamePanel();
                if (panel != null)
                    panel.gameObject.SetActive(false);
            }
            else
            {
                if (_fgui.IsBound) return;

                var panel = gameUIPanel != null ? gameUIPanel : ResolveDefaultGamePanel();
                if (jumpInput == null)
                    jumpInput = UnityObjectFinder.FindFirst<LegacyPlayerInput>();

                if (panel == null) return;
                if (panel.container == null) return;

                _fgui.Bind(panel, jumpInput);
            }

            var climb = UnityObjectFinder.FindFirst<ClimbController>();
            var climbing = climb != null && climb.State != ClimbController.ClimbState.Inactive;
            if (UseUgui)
            {
                uguiHud.SetClimbOnlyHudVisible(climbing);
                uguiHud.ApplyProductModeVisibility();
            }
            else
                _fgui.SetClimbOnlyHudVisible(climbing);

            SyncEconomyFromSave(force: true);
        }

        private float _nextBuffUiTickUnscaled = -1f;

        private void Update()
        {
            if (!UseUgui && !_fgui.IsBound)
                return;

            if (Time.unscaledTime >= _nextBuffUiTickUnscaled)
            {
                _nextBuffUiTickUnscaled = Time.unscaledTime + 0.25f;
                if (UseUgui)
                    uguiHud.TickBuffDisplays();
                else
                    _fgui.TickBuffDisplays();
            }

            SyncEconomyFromSave(force: false);
        }

        private void SyncEconomyFromSave(bool force)
        {
            var d = SaveService.Data;
            if (d == null) return;
            if (!force && d.coins == _lastCoins && d.gems == _lastGems && d.medals == _lastMedals) return;

            var prevCoins = _lastCoins;
            var prevGems = _lastGems;
            var economyChanged = d.coins != _lastCoins || d.gems != _lastGems || d.medals != _lastMedals;
            var balanceDecreased = prevCoins != long.MinValue && (d.coins < prevCoins || d.gems < prevGems);
            _lastCoins = d.coins;
            _lastGems = d.gems;
            _lastMedals = d.medals;
            if (UseUgui)
                uguiHud.SetEconomyDisplay(
                    _lastCoins,
                    _lastGems,
                    _lastMedals,
                    immediate: force || !economyChanged || balanceDecreased);
            else if (_fgui.IsBound)
                _fgui.SetEconomyDisplay(_lastCoins, _lastGems, _lastMedals);
            if (economyChanged)
                EconomyDisplayEvents.Raise();
        }

        public static UIPanel ResolveDefaultGamePanel()
        {
#if UNITY_2023_1_OR_NEWER
            var panels = Object.FindObjectsByType<UIPanel>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var panels = Object.FindObjectsOfType<UIPanel>();
#endif

            var activeScene = SceneManager.GetActiveScene();
            UIPanel fallback = null;

            for (var i = 0; i < panels.Length; i++)
            {
                var p = panels[i];
                if (p == null || p.packageName != "Game" || p.componentName != "GameWnd")
                    continue;

                if (p.gameObject.scene == activeScene)
                    return p;

                fallback ??= p;
            }

            return fallback;
        }

        public void SetHeight(float heightMeters)
        {
            if (!UseUgui && !_fgui.IsBound) TryBindHud();
            if (UseUgui) uguiHud.SetHeight(heightMeters);
            else _fgui.SetHeight(heightMeters);
        }

        /// <param name="current">当前体力</param>
        /// <param name="max">体力上限（用于进度与文案）</param>
        public void SetStamina(float current, float max)
        {
            if (!UseUgui && !_fgui.IsBound) TryBindHud();
            if (UseUgui) uguiHud.SetStamina(current, max);
            else _fgui.SetStamina(current, max);
        }

        /// <summary>攀爬专用 HUD（高度、体力条、两枚奖励按钮）显隐。</summary>
        public void SetClimbOnlyHudVisible(bool visible)
        {
            if (!UseUgui && !_fgui.IsBound) TryBindHud();
            if (UseUgui) uguiHud.SetClimbOnlyHudVisible(visible);
            else _fgui.SetClimbOnlyHudVisible(visible);
        }

        /// <summary>刷新攀爬速度 / 双倍金币 HUD 倒计时（激励 Buff）。</summary>
        public void RefreshBuffDisplays()
        {
            if (!UseUgui && !_fgui.IsBound) TryBindHud();
            if (UseUgui) uguiHud.TickBuffDisplays();
            else _fgui.TickBuffDisplays();
        }

        /// <summary>从 <see cref="SaveService.Data"/> 刷新顶部金币、宝石文案（商店/结算等改档后可立即调用）。</summary>
        public void RefreshEconomyDisplay()
        {
            if (!UseUgui && !_fgui.IsBound) TryBindHud();
            SyncEconomyFromSave(force: true);
            if (UseUgui) uguiHud.RefreshHudMapShortcutLabel();
            else _fgui.RefreshHudMapShortcutLabel();
        }

        /// <summary>金币飞入逐枚到账：写存档并同步顶栏数字。</summary>
        public void AddCoinsDisplay(long delta, bool immediate = true)
        {
            if (delta <= 0)
                return;

            if (SaveService.Data == null)
                return;

            ModeSaveAccess.AddCoins(delta);
            _lastCoins = SaveService.Data.coins;
            SaveService.Save();

            if (!UseUgui && !_fgui.IsBound)
                TryBindHud();

            if (UseUgui)
                uguiHud.AddCoinsToDisplay(delta, immediate);
            else if (_fgui.IsBound)
                _fgui.AddCoinsToDisplay(delta);

            EconomyDisplayEvents.Raise();
        }

        /// <summary>刷新钓鱼力量 HUD（换装/训练后立即更新）。</summary>
        public void RefreshStrengthDisplay()
        {
            if (!UseUgui && !_fgui.IsBound) TryBindHud();
            if (UseUgui) uguiHud.RefreshStrengthDisplay();
        }
    }
}
