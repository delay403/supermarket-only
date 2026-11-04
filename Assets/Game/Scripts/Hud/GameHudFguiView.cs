using System;
using FairyGUI;
using Game;
using Game.Reward;
using Game.Audio;
using Game.Core;
using Game.Core.Config;
using Game.Economy;
using Game.Glide;
using Game.Maps;
using Game.Player;
using Game.Save;
using Game.Tower;
using Game.UI.Framework;
using UnityEngine;

namespace Game.Hud
{
    /// <summary>
    /// 绑定主 HUD：高度、体力条、跳跃。优先使用 <see cref="UIGameWnd"/> 强类型字段；失败时按节点名深度查找。
    /// </summary>
    public sealed class GameHudFguiView
    {
        private GComponent _hudRoot;
        private GButton _btnJump;
        private GTextField _heightLabel;
        private GObject _staminaSliderRoot;
        private GLoader _staminaFill;
        private GTextField _staminaLabel;
        private GObject _speedBuffRoot;
        private UICtrpSpeed _speedBuffButton;
        private UICtrpClimb _autoClimbButton;
        private ClimbController _cachedClimb;
        private UIRewardForMoney _moneyBuffButton;
        private EventCallback1 _speedBuffClickHandler;
        private EventCallback1 _autoClimbClickHandler;
        private EventCallback1 _moneyBuffClickHandler;
        private GTextField _coinsLabel;
        private GTextField _gemsLabel;
        private GTextField _medalsLabel;
        private UIItemMap _mapHudShortcut;
        private GButton _mapHudClickTarget;
        private GTextField _mapHudTitleLabel;
        private EventCallback1 _mapHudClickHandler;
        private LegacyPlayerInput _jumpInput;
        private EventCallback1 _jumpHandler;
        private GButton _routerWing;
        private EventCallback1 _wingShopClickHandler;
        private GButton _routerTrail;
        private EventCallback1 _trailShopClickHandler;
        private GButton _routerPet;
        private GButton _routerPetShop;
        private GButton _routerCharacter;
        private EventCallback1 _routerPetClickHandler;
        private EventCallback1 _routerPetHatchClickHandler;
        private EventCallback1 _routerCharacterClickHandler;

        private GButton _btnSetting;
        private EventCallback1 _settingClickHandler;

        private GButton _btnLoop;
        private EventCallback1 _luckyWheelClickHandler;
        private GButton _btnSignin;
        private EventCallback1 _signInClickHandler;

        private GObject _economyMoneyClickRoot;
        private GObject _economyDiamondClickRoot;
        private EventCallback1 _economyMoneyClickHandler;
        private EventCallback1 _economyDiamondClickHandler;

        /// <summary>
        /// 速度 Buff 脉冲动画已启动且仍应对应当前激活状态；避免 TickBuffDisplays 周期性打断 GTween yoyo（否则会一直只播放「放大」半段）。
        /// </summary>
        bool _speedBuffIconPulseRunningForClimbBuff;

        public bool IsBound => _hudRoot != null;

        public void Bind(UIPanel panel, LegacyPlayerInput jumpInput)
        {
            Unbind();

            _jumpInput = jumpInput;

            if (panel == null)
            {
                Debug.LogWarning("GameHudFguiView: UIPanel 未指定，无法绑定 HUD。");
                return;
            }

            var root = panel.ui as GComponent;
            if (root == null)
            {
                Debug.LogWarning("GameHudFguiView: UIPanel.ui 为空或不是 GComponent，请检查包名/组件名与加载时机。");
                return;
            }

            _hudRoot = root;
            WireControls();

            if (_btnJump == null)
                Debug.LogWarning("GameHudFguiView: 未找到跳跃按钮（BtnJump），请检查 FGUI 节点名。");
            if (_heightLabel == null)
                Debug.LogWarning("GameHudFguiView: 未找到高度文本（HeightLabel）。");
            if (_staminaFill == null && _staminaLabel == null)
                Debug.LogWarning("GameHudFguiView: 未找到体力条（SliderPower / Slider 下 Fill、Label）。");
            if (_coinsLabel == null)
                Debug.LogWarning("GameHudFguiView: 未找到金币文案（ItemMoney / Economy / Money）。");
            if (_gemsLabel == null)
                Debug.LogWarning("GameHudFguiView: 未找到宝石文案（ItemDiamond / Diamond）。");

            if (_btnJump != null)
            {
                _btnJump.touchable = true;
                _jumpHandler = OnJumpClicked;
                _btnJump.onClick.Add(_jumpHandler);
            }

            if (_hudRoot is UIGameWnd gameWnd && gameWnd.m_RouterWing != null)
                _routerWing = gameWnd.m_RouterWing;
            _routerWing ??= FindDeep(_hudRoot, "RouterWing")?.asButton;
            if (_routerWing != null)
            {
                _wingShopClickHandler = OnRouterWingClicked;
                _routerWing.onClick.Add(_wingShopClickHandler);
            }

            if (_hudRoot is UIGameWnd gameWndTrail && gameWndTrail.m_RouterTrail != null)
                _routerTrail = gameWndTrail.m_RouterTrail;
            _routerTrail ??= FindDeep(_hudRoot, "RouterTrail")?.asButton;
            if (_routerTrail != null)
            {
                _trailShopClickHandler = OnRouterTrailClicked;
                _routerTrail.onClick.Add(_trailShopClickHandler);
            }

            if (_hudRoot is UIGameWnd gw)
            {
                if (gw.m_RouterPet != null) _routerPet = gw.m_RouterPet;
                if (gw.m_RouterPerShop != null) _routerPetShop = gw.m_RouterPerShop;
                if (gw.m_RouterCha != null) _routerCharacter = gw.m_RouterCha;
                if (gw.m_BtnSetting != null) _btnSetting = gw.m_BtnSetting;
            }
            _routerPet ??= FindDeep(_hudRoot, "RouterPet")?.asButton;
            _routerPetShop ??= FindDeep(_hudRoot, "RouterPerShop")?.asButton
                              ?? FindDeep(_hudRoot, "RouterPetShop")?.asButton;
            _routerCharacter ??= FindDeep(_hudRoot, "RouterCha")?.asButton;
            _btnSetting ??= FindDeep(_hudRoot, "BtnSetting")?.asButton;

            if (_routerPet != null)
            {
                _routerPetClickHandler = OnRouterPetBackpackClicked;
                _routerPet.onClick.Add(_routerPetClickHandler);
            }
            if (_routerPetShop != null)
            {
                _routerPetHatchClickHandler = OnRouterPetHatchClicked;
                _routerPetShop.onClick.Add(_routerPetHatchClickHandler);
            }
            if (_routerCharacter != null)
            {
                _routerCharacterClickHandler = OnRouterCharacterClicked;
                _routerCharacter.onClick.Add(_routerCharacterClickHandler);
            }
            if (_btnSetting != null)
            {
                _settingClickHandler = OnSettingsClicked;
                _btnSetting.onClick.Add(_settingClickHandler);
            }

            WireEconomyOfferClickTargets();
            WireLuckyWheelAndSignInButtons();
            WireMedalsAndMapShortcut();
            WireBuffButtons();
            WireAutoClimbButton();

            SetClimbOnlyHudVisible(false);
            TickBuffDisplays();
            RefreshHudMapShortcutLabel();
        }

        void WireMedalsAndMapShortcut()
        {
            MapRuntime.Changed -= OnMapRuntimeChanged;
            MapRuntime.Changed += OnMapRuntimeChanged;

            if (_mapHudClickTarget != null && _mapHudClickHandler != null)
                _mapHudClickTarget.onClick.Remove(_mapHudClickHandler);

            if (_hudRoot is UIGameWnd gwMed && gwMed.m_ItemChapion != null)
                _medalsLabel = gwMed.m_ItemChapion.m_Label;
            _medalsLabel ??= ResolveEconomyLabel(_hudRoot, "ItemChapion", "Chapion");

            _mapHudShortcut = null;
            _mapHudClickTarget = null;
            _mapHudTitleLabel = null;

            GObject mapObj = null;
            if (_hudRoot is UIGameWnd gwMap)
            {
                for (var i = 0; i < gwMap.numChildren; i++)
                {
                    var ch = gwMap.GetChildAt(i);
                    if (ch is UIItemMap uiMap)
                    {
                        mapObj = uiMap;
                        break;
                    }
                }
            }

            mapObj ??= FindDeep(_hudRoot, "ItemMap");

            if (mapObj != null)
            {
                _mapHudShortcut = mapObj as UIItemMap;

                _mapHudClickTarget = _mapHudShortcut ?? mapObj.asButton;
                if (_mapHudClickTarget != null)
                {
                    _mapHudClickTarget.touchable = true;
                    _mapHudClickHandler = OnMapShortcutClicked;
                    _mapHudClickTarget.onClick.Add(_mapHudClickHandler);
                }

                _mapHudTitleLabel = ResolveItemMapTitleLabel(mapObj);
                if (_mapHudShortcut != null && _mapHudTitleLabel != null)
                    _mapHudShortcut.m_Label = _mapHudTitleLabel;
            }
        }

        /// <summary>
        /// 地图按钮下的名称：<see cref="UIItemMap.m_Label"/> 可能与编辑器层级不一致，优先按常用节点名解析。
        /// </summary>
        static GTextField ResolveItemMapTitleLabel(GObject mapRoot)
        {
            var com = mapRoot?.asCom;
            if (com == null) return null;

            var tf = com.GetChild("Label")?.asTextField
                     ?? com.GetChild("TextField")?.asTextField
                     ?? com.GetChild("title")?.asTextField;
            if (tf != null)
                return tf;

            if (com is UIItemMap ui && ui.m_Label != null)
                return ui.m_Label;

            foreach (var key in new[] { "Label", "TextField", "title" })
            {
                var deep = FindDeep(com, key)?.asTextField;
                if (deep != null)
                    return deep;
            }

            for (var i = 0; i < com.numChildren; i++)
            {
                var guess = com.GetChildAt(i)?.asTextField;
                if (guess != null)
                    return guess;
            }

            return null;
        }

        static void OnMapRuntimeChanged() => UiCoordinator.Hud?.RefreshEconomyDisplay();

        /// <summary>地图与各装备商店：攀爬中不可打开；滑翔/坠落/结算至重生前也不可打开。</summary>
        static bool IsMapAndEquipmentNavigationBlocked() =>
            GameplayNavigationBlock.BlocksMapAndEquipmentShops();

        static void ShowCannotUseDuringClimbOrGlideHud() =>
            GameplayNavigationBlock.ShowShopNavigationBlockedHint();

        static void OnMapShortcutClicked(EventContext _)
        {
            if (IsMapAndEquipmentNavigationBlocked())
            {
                ShowCannotUseDuringClimbOrGlideHud();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();

            if (FairyGuiTouchGuard.IsBlocking)
                Debug.LogWarning(
                    "[HUD] FairyGuiTouchGuard 正在屏蔽 FairyGUI 触摸（通常因有 UGUI 面板仍打开或未正确 Hide/PopBlock）。地图按钮将无效；请先关闭相关面板。");

            UiRouter.OpenMapSelect();
        }

        public void RefreshHudMapShortcutLabel()
        {
            var tf = _mapHudTitleLabel;
            if (tf == null && _mapHudShortcut != null)
                tf = _mapHudShortcut.m_Label;

            if (tf == null)
                return;

            var id = SaveService.Data?.currentMapId;
            if (ConfigRegistry.Maps != null && ConfigRegistry.Maps.TryGet(id, out var m) && m != null)
                tf.text = m.displayName ?? id ?? "";
            else
                tf.text = id ?? "";
        }

        private void WireControls()
        {
            if (_hudRoot is UIGameWnd wnd)
            {
                _heightLabel = wnd.m_HeightLabel;
                _speedBuffButton = wnd.m_BtnAdSpeed;
                _speedBuffRoot = _speedBuffButton;
                if (wnd.m_SliderPower != null)
                {
                    _staminaSliderRoot = wnd.m_SliderPower;
                    _staminaFill = wnd.m_SliderPower.m_Fill;
                    _staminaLabel = wnd.m_SliderPower.m_Label;
                }

                if (wnd.m_ItemMoney != null) _coinsLabel = wnd.m_ItemMoney.m_Label;
                if (wnd.m_ItemDiamond != null) _gemsLabel = wnd.m_ItemDiamond.m_Label;
                if (wnd.m_BtnClimb != null) _autoClimbButton = wnd.m_BtnClimb;
            }

            _btnJump ??= FindDeep(_hudRoot, "BtnJump")?.asButton;
            _heightLabel ??= FindDeep(_hudRoot, "HeightLabel")?.asTextField;

            _speedBuffRoot ??= FindDeep(_hudRoot, "BtnAdSpeed") ?? FindDeep(_hudRoot, "CtrpSpeed");
            _speedBuffButton ??= (_speedBuffRoot as UICtrpSpeed)
                               ?? FindDeepTyped<UICtrpSpeed>(_hudRoot);
            if (_speedBuffButton != null)
                _speedBuffRoot = _speedBuffButton;

            _autoClimbButton ??= FindDeepTyped<UICtrpClimb>(_hudRoot);

            if (_staminaFill == null || _staminaLabel == null || _staminaSliderRoot == null)
            {
                var sliderObj = FindDeep(_hudRoot, "SliderPower") ?? FindDeep(_hudRoot, "Slider");
                var sliderCom = sliderObj?.asCom;
                if (sliderCom != null)
                {
                    _staminaSliderRoot ??= sliderCom;
                    _staminaFill ??= sliderCom.GetChild("Fill")?.asLoader;
                    _staminaLabel ??= sliderCom.GetChild("Label")?.asTextField;
                }
            }

            _coinsLabel ??= ResolveEconomyLabel(_hudRoot, "ItemMoney", "Economy", "Money");
            _gemsLabel ??= ResolveEconomyLabel(_hudRoot, "ItemDiamond", "Diamond");
        }

        /// <summary>解析 Economy / Diamond 组件内的主数字文本（发布代码里一般为第二个子节点 Label）。</summary>
        private static GTextField ResolveEconomyLabel(GComponent root, params string[] nodeNames)
        {
            if (root == null) return null;
            foreach (var name in nodeNames)
            {
                var com = FindDeep(root, name)?.asCom;
                if (com == null) continue;
                if (com is UIEconomy eco) return eco.m_Label;
                if (com is UIDiamond dia) return dia.m_Label;
                var label = com.GetChild("title")?.asTextField;
                if (label != null) return label;
                for (var i = 0; i < com.numChildren; i++)
                {
                    var tf = com.GetChildAt(i) as GTextField;
                    if (tf != null) return tf;
                }
            }

            return null;
        }

        void WireEconomyOfferClickTargets()
        {
            UnwireEconomyOfferClickTargets();

            if (_hudRoot is UIGameWnd gw)
            {
                if (gw.m_ItemMoney != null)
                {
                    _economyMoneyClickRoot = gw.m_ItemMoney;
                    _economyMoneyClickRoot.touchable = true;
                    _economyMoneyClickHandler = OnEconomyMoneyRowClicked;
                    _economyMoneyClickRoot.onClick.Add(_economyMoneyClickHandler);
                }

                if (gw.m_ItemDiamond != null)
                {
                    _economyDiamondClickRoot = gw.m_ItemDiamond;
                    _economyDiamondClickRoot.touchable = true;
                    _economyDiamondClickHandler = OnEconomyDiamondRowClicked;
                    _economyDiamondClickRoot.onClick.Add(_economyDiamondClickHandler);
                }
            }
        }

        void UnwireEconomyOfferClickTargets()
        {
            if (_economyMoneyClickRoot != null && _economyMoneyClickHandler != null)
                _economyMoneyClickRoot.onClick.Remove(_economyMoneyClickHandler);
            if (_economyDiamondClickRoot != null && _economyDiamondClickHandler != null)
                _economyDiamondClickRoot.onClick.Remove(_economyDiamondClickHandler);
            _economyMoneyClickRoot = null;
            _economyDiamondClickRoot = null;
            _economyMoneyClickHandler = null;
            _economyDiamondClickHandler = null;
        }

        void WireLuckyWheelAndSignInButtons()
        {
            UnwireLuckyWheelAndSignInButtons();

            if (_hudRoot is UIGameWnd gw)
            {
                if (gw.m_BtnLoop != null)
                {
                    _btnLoop = gw.m_BtnLoop;
                    _btnLoop.touchable = true;
                    _luckyWheelClickHandler = OnLuckyWheelClicked;
                    _btnLoop.onClick.Add(_luckyWheelClickHandler);
                }

                if (gw.m_BtnSignin != null)
                {
                    _btnSignin = gw.m_BtnSignin;
                    _btnSignin.touchable = true;
                    _signInClickHandler = OnSignInClicked;
                    _btnSignin.onClick.Add(_signInClickHandler);
                }
            }
        }

        void UnwireLuckyWheelAndSignInButtons()
        {
            if (_btnLoop != null && _luckyWheelClickHandler != null)
                _btnLoop.onClick.Remove(_luckyWheelClickHandler);
            if (_btnSignin != null && _signInClickHandler != null)
                _btnSignin.onClick.Remove(_signInClickHandler);
            _btnLoop = null;
            _btnSignin = null;
            _luckyWheelClickHandler = null;
            _signInClickHandler = null;
        }

        static void OnEconomyMoneyRowClicked(EventContext _)
        {
            if (GameplayNavigationBlock.ShouldBlockOpeningFreeCurrencyFromHud())
            {
                GameplayNavigationBlock.ShowShopNavigationBlockedHint();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenFreeCoinsOffer();
        }

        static void OnEconomyDiamondRowClicked(EventContext _)
        {
            if (GameplayNavigationBlock.ShouldBlockOpeningFreeCurrencyFromHud())
            {
                GameplayNavigationBlock.ShowShopNavigationBlockedHint();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenFreeDiamondsOffer();
        }

        static void OnLuckyWheelClicked(EventContext _)
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                GameplayNavigationBlock.ShowShopNavigationBlockedHint();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenLuckyWheel();
        }

        static void OnSignInClicked(EventContext _)
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                GameplayNavigationBlock.ShowShopNavigationBlockedHint();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenSignIn();
        }

        /// <summary>高度文案、体力条、双倍速度按钮：仅在攀爬中显示（双倍金币入口常驻）。</summary>
        public void SetClimbOnlyHudVisible(bool visible)
        {
            if (_heightLabel != null) _heightLabel.visible = visible;
            if (_staminaSliderRoot != null) _staminaSliderRoot.visible = visible;
            if (_speedBuffRoot != null)
            {
                _speedBuffRoot.visible = visible;
                if (_speedBuffButton != null)
                    _speedBuffButton.visible = visible;
                if (!visible)
                    ResetSpeedBuffIconPresentation();
            }

            if (_autoClimbButton != null)
            {
                _autoClimbButton.visible = visible;
                if (visible)
                    RefreshAutoClimbHud();
                else
                    ResetAutoClimbHudPresentation();
            }

            if (visible)
                TickBuffDisplays();
        }

        public void TickBuffDisplays()
        {
            var d = SaveService.Data;

            if (_speedBuffButton != null && _speedBuffButton.visible)
            {
                ForceCtrpSpeedStatusToOnPage(_speedBuffButton);

                var climbBuff = RewardBuffService.IsClimbBuffActive();
                if (_speedBuffButton.m_CountTime != null)
                {
                    _speedBuffButton.m_CountTime.text = climbBuff && d != null
                        ? RewardBuffService.FormatRemainingMmSs(d.climbBuffEndUtcTicks)
                        : RewardBuffService.HintDurationMmSs;
                }

                if (_speedBuffButton.m_Label != null)
                    _speedBuffButton.m_Label.text = "速度+100%";
            }

            RefreshSpeedBuffIconPulse();

            if (_moneyBuffButton != null && _moneyBuffButton.m_LabelTime != null)
            {
                var coinOn = RewardBuffService.IsCoinsBuffActive();
                _moneyBuffButton.m_LabelTime.text = coinOn && d != null
                    ? RewardBuffService.FormatRemainingMmSs(d.coinsBuffEndUtcTicks)
                    : RewardBuffService.HintDurationMmSs;
            }
        }

        void WireBuffButtons()
        {
            if (_moneyBuffButton != null && _moneyBuffClickHandler != null)
                _moneyBuffButton.onClick.Remove(_moneyBuffClickHandler);
            if (_speedBuffButton != null && _speedBuffClickHandler != null)
                _speedBuffButton.onClick.Remove(_speedBuffClickHandler);

            _moneyBuffButton ??= FindDeepTyped<UIRewardForMoney>(_hudRoot);
            if (_moneyBuffButton != null)
            {
                _moneyBuffButton.visible = true;
                _moneyBuffButton.touchable = true;
                _moneyBuffClickHandler = OnMoneyBuffClicked;
                _moneyBuffButton.onClick.Add(_moneyBuffClickHandler);
            }

            _speedBuffButton ??= _speedBuffRoot as UICtrpSpeed;
            if (_speedBuffButton != null)
            {
                _speedBuffRoot = _speedBuffButton;
                _speedBuffButton.touchable = true;
                _speedBuffClickHandler = OnSpeedBuffClicked;
                _speedBuffButton.onClick.Add(_speedBuffClickHandler);
                ForceCtrpSpeedStatusToOnPage(_speedBuffButton);
            }
        }

        void WireAutoClimbButton()
        {
            if (_autoClimbButton != null && _autoClimbClickHandler != null)
                _autoClimbButton.onClick.Remove(_autoClimbClickHandler);
            _autoClimbClickHandler = null;

            if (_autoClimbButton == null)
                return;

            _autoClimbButton.touchable = true;
            _autoClimbClickHandler = OnAutoClimbClicked;
            _autoClimbButton.onClick.Add(_autoClimbClickHandler);
        }

        ClimbController EnsureClimbController() =>
            _cachedClimb != null ? _cachedClimb : (_cachedClimb = UnityObjectFinder.FindFirst<ClimbController>());

        void RefreshAutoClimbHud()
        {
            if (_autoClimbButton == null)
                return;

            var climb = EnsureClimbController();
            var on = climb != null &&
                     climb.State != ClimbController.ClimbState.Inactive &&
                     climb.AutoAscendEnabled;

            if (_autoClimbButton.m_Label != null)
                _autoClimbButton.m_Label.text = on ? "自动攀爬中..." : "自动攀爬";

            if (_autoClimbButton.m_gou != null)
                _autoClimbButton.m_gou.visible = on;

            var icon = _autoClimbButton.m_Icon;
            if (icon == null)
                return;

            GTween.Kill(icon);
            icon.SetScale(1f, 1f);
            if (on)
                icon.TweenScale(new Vector2(1.12f, 1.12f), 0.4f).SetEase(EaseType.Linear).SetRepeat(-1, true);
        }

        void RefreshSpeedBuffIconPulse()
        {
            if (_speedBuffButton == null || !_speedBuffButton.visible)
                return;

            var icon = _speedBuffButton.m_Icon;
            if (icon == null)
                return;

            var buffOn = RewardBuffService.IsClimbBuffActive();
            if (!buffOn)
            {
                if (_speedBuffIconPulseRunningForClimbBuff)
                {
                    _speedBuffIconPulseRunningForClimbBuff = false;
                    GTween.Kill(icon);
                    icon.SetScale(1f, 1f);
                }

                return;
            }

            if (_speedBuffIconPulseRunningForClimbBuff)
                return;

            _speedBuffIconPulseRunningForClimbBuff = true;
            GTween.Kill(icon);
            icon.SetScale(1f, 1f);
            icon.TweenScale(new Vector2(1.12f, 1.12f), 0.4f).SetEase(EaseType.Linear).SetRepeat(-1, true);
        }

        void ResetSpeedBuffIconPresentation()
        {
            _speedBuffIconPulseRunningForClimbBuff = false;
            var icon = _speedBuffButton?.m_Icon;
            if (icon == null)
                return;

            GTween.Kill(icon);
            icon.SetScale(1f, 1f);
        }

        void ResetAutoClimbHudPresentation()
        {
            if (_autoClimbButton?.m_Label != null)
                _autoClimbButton.m_Label.text = "自动攀爬";

            if (_autoClimbButton?.m_gou != null)
                _autoClimbButton.m_gou.visible = false;

            var icon = _autoClimbButton?.m_Icon;
            if (icon == null)
                return;

            GTween.Kill(icon);
            icon.SetScale(1f, 1f);
        }

        void OnAutoClimbClicked(EventContext _)
        {
            AudioGameplayFeedback.PlayUiButton();
            var climb = EnsureClimbController();
            if (climb == null || climb.State == ClimbController.ClimbState.Inactive)
                return;

            climb.AutoAscendEnabled = !climb.AutoAscendEnabled;
            RefreshAutoClimbHud();
        }

        /// <summary>
        /// CtrpSpeed：GearDisplay 在「off」时会把对象移出渲染容器并 SetActive(false)；必须切到「on」页才有文本。
        /// 若资源里勾了「不可见」，也需在 FGUI 里取消，否则初始 internalVisible 仍为 false。
        /// </summary>
        static Controller ResolveCtrpSpeedStatusController(UICtrpSpeed btn)
        {
            if (btn == null)
                return null;

            var c = btn.GetController("StatusAd");
            return c ?? btn.m_StatusAd;
        }

        static int ResolveCtrpSpeedOnPageIndex(Controller c)
        {
            if (c == null || c.pageCount <= 0)
                return 0;

            for (var i = 0; i < c.pageCount; i++)
            {
                var pn = c.GetPageName(i);
                if (!string.IsNullOrEmpty(pn) && pn.Equals("on", StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            for (var i = 0; i < c.pageCount; i++)
            {
                var pn = c.GetPageName(i);
                if (!string.IsNullOrEmpty(pn) && !pn.Equals("off", StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return c.pageCount >= 2 ? 1 : 0;
        }

        static void ForceCtrpSpeedStatusToOnPage(UICtrpSpeed btn)
        {
            var c = ResolveCtrpSpeedStatusController(btn);
            if (c == null || c.pageCount <= 0)
                return;

            var onIdx = ResolveCtrpSpeedOnPageIndex(c);
            if (c.selectedIndex != onIdx)
            {
                c.selectedIndex = onIdx;
                return;
            }

            var bump = onIdx == 0 ? Mathf.Min(1, c.pageCount - 1) : 0;
            if (bump != onIdx)
            {
                c.SetSelectedIndex(bump);
                c.selectedIndex = onIdx;
            }
        }

        static T FindDeepTyped<T>(GComponent parent) where T : GObject
        {
            if (parent == null)
                return null;

            for (var i = 0; i < parent.numChildren; i++)
            {
                var ch = parent.GetChildAt(i);
                if (ch is T match)
                    return match;
                var com = ch.asCom;
                if (com != null)
                {
                    var inner = FindDeepTyped<T>(com);
                    if (inner != null)
                        return inner;
                }
            }

            return null;
        }

        void OnSpeedBuffClicked(EventContext _)
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                GameplayNavigationBlock.ShowShopNavigationBlockedHint();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenDoubleClimbSpeedOffer();
        }

        void OnMoneyBuffClicked(EventContext _)
        {
            if (GameplayNavigationBlock.BlocksOffersBuffsAndRewardDialogsUntilRespawn())
            {
                GameplayNavigationBlock.ShowShopNavigationBlockedHint();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenDoubleSettlementCoinsOffer();
        }

        private static GObject FindDeep(GComponent parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;

            var direct = parent.GetChild(name);
            if (direct != null) return direct;

            for (var i = 0; i < parent.numChildren; i++)
            {
                var c = parent.GetChildAt(i).asCom;
                if (c == null) continue;
                var found = FindDeep(c, name);
                if (found != null) return found;
            }

            return null;
        }

        private void OnJumpClicked(EventContext _)
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_jumpInput != null)
                _jumpInput.QueueGlidePressFromUi();
        }

        private static void OnRouterWingClicked(EventContext _)
        {
            if (IsMapAndEquipmentNavigationBlocked())
            {
                ShowCannotUseDuringClimbOrGlideHud();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenWingShop();
        }

        private static void OnRouterTrailClicked(EventContext _)
        {
            if (IsMapAndEquipmentNavigationBlocked())
            {
                ShowCannotUseDuringClimbOrGlideHud();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenTrailShop();
        }

        private static void OnRouterPetBackpackClicked(EventContext _)
        {
            if (IsMapAndEquipmentNavigationBlocked())
            {
                ShowCannotUseDuringClimbOrGlideHud();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenPet();
        }

        private static void OnRouterPetHatchClicked(EventContext _)
        {
            if (IsMapAndEquipmentNavigationBlocked())
            {
                ShowCannotUseDuringClimbOrGlideHud();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenPetHatch();
        }

        private static void OnRouterCharacterClicked(EventContext _)
        {
            if (IsMapAndEquipmentNavigationBlocked())
            {
                ShowCannotUseDuringClimbOrGlideHud();
                return;
            }

            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenCharacterShop();
        }

        private static void OnSettingsClicked(EventContext _)
        {
            AudioGameplayFeedback.PlayUiButton();
            UiRouter.OpenSettings();
        }

        public void Unbind()
        {
            MapRuntime.Changed -= OnMapRuntimeChanged;

            if (_mapHudClickTarget != null && _mapHudClickHandler != null)
                _mapHudClickTarget.onClick.Remove(_mapHudClickHandler);

            if (_btnJump != null && _jumpHandler != null)
                _btnJump.onClick.Remove(_jumpHandler);

            if (_routerWing != null && _wingShopClickHandler != null)
                _routerWing.onClick.Remove(_wingShopClickHandler);
            if (_routerTrail != null && _trailShopClickHandler != null)
                _routerTrail.onClick.Remove(_trailShopClickHandler);
            if (_routerPet != null && _routerPetClickHandler != null)
                _routerPet.onClick.Remove(_routerPetClickHandler);
            if (_routerPetShop != null && _routerPetHatchClickHandler != null)
                _routerPetShop.onClick.Remove(_routerPetHatchClickHandler);
            if (_routerCharacter != null && _routerCharacterClickHandler != null)
                _routerCharacter.onClick.Remove(_routerCharacterClickHandler);
            if (_btnSetting != null && _settingClickHandler != null)
                _btnSetting.onClick.Remove(_settingClickHandler);

            UnwireEconomyOfferClickTargets();
            UnwireLuckyWheelAndSignInButtons();

            if (_moneyBuffButton != null && _moneyBuffClickHandler != null)
                _moneyBuffButton.onClick.Remove(_moneyBuffClickHandler);
            if (_speedBuffButton != null && _speedBuffClickHandler != null)
                _speedBuffButton.onClick.Remove(_speedBuffClickHandler);
            if (_autoClimbButton != null && _autoClimbClickHandler != null)
                _autoClimbButton.onClick.Remove(_autoClimbClickHandler);

            var autoClimbIcon = _autoClimbButton?.m_Icon;
            if (autoClimbIcon != null)
            {
                GTween.Kill(autoClimbIcon);
                autoClimbIcon.SetScale(1f, 1f);
            }

            var speedBuffIcon = _speedBuffButton?.m_Icon;
            if (speedBuffIcon != null)
            {
                GTween.Kill(speedBuffIcon);
                speedBuffIcon.SetScale(1f, 1f);
            }

            _speedBuffIconPulseRunningForClimbBuff = false;

            _jumpHandler = null;
            _wingShopClickHandler = null;
            _trailShopClickHandler = null;
            _routerPetClickHandler = null;
            _routerPetHatchClickHandler = null;
            _routerCharacterClickHandler = null;
            _settingClickHandler = null;
            _routerWing = null;
            _routerTrail = null;
            _routerPet = null;
            _routerPetShop = null;
            _routerCharacter = null;
            _btnSetting = null;
            _hudRoot = null;
            _btnJump = null;
            _heightLabel = null;
            _staminaSliderRoot = null;
            _staminaFill = null;
            _staminaLabel = null;
            _speedBuffRoot = null;
            _speedBuffButton = null;
            _autoClimbButton = null;
            _cachedClimb = null;
            _moneyBuffButton = null;
            _speedBuffClickHandler = null;
            _autoClimbClickHandler = null;
            _moneyBuffClickHandler = null;
            _coinsLabel = null;
            _gemsLabel = null;
            _medalsLabel = null;
            _mapHudShortcut = null;
            _mapHudClickTarget = null;
            _mapHudTitleLabel = null;
            _mapHudClickHandler = null;
            _jumpInput = null;
        }

        public void SetEconomyDisplay(long coins, long gems, long medals)
        {
            if (_coinsLabel != null) _coinsLabel.text = EconomyDisplayFormat.Format(coins);
            if (_gemsLabel != null) _gemsLabel.text = EconomyDisplayFormat.Format(gems);
            if (_medalsLabel != null) _medalsLabel.text = EconomyDisplayFormat.Format(medals);
            RefreshHudMapShortcutLabel();
        }

        public void AddCoinsToDisplay(long delta)
        {
            if (delta <= 0 || _coinsLabel == null || SaveService.Data == null)
                return;

            _coinsLabel.text = EconomyDisplayFormat.Format(SaveService.Data.coins);
        }

        public void SetHeight(float heightMeters)
        {
            if (_heightLabel == null) return;
            _heightLabel.text = $"当前: {Mathf.Max(0f, heightMeters):0}米";
        }

        public void SetStamina(float current, float max)
        {
            var n = max <= 0.0001f ? 0f : Mathf.Clamp01(current / max);
            if (_staminaFill != null)
                _staminaFill.fillAmount = n;

            if (_staminaLabel != null)
            {
                var curInt = Mathf.Max(0, Mathf.RoundToInt(current));
                var maxInt = Mathf.Max(0, Mathf.RoundToInt(max));
                _staminaLabel.text = $"{curInt}/{maxInt}";
            }
        }
    }
}
