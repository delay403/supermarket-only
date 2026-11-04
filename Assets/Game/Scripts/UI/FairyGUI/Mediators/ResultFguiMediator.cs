using FairyGUI;
using Game.Reward;
using Game.Audio;
using Game.Economy;
using Game.Player;
using Game.Save;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;

namespace Game.UI.FairyGUI.Mediators
{
    /// <summary>FairyGUI 滑行结算：Mask 仅遮挡、不可关闭；主面板自顶向下弹入；继续 / 双倍金币后回出生地。</summary>
    public sealed class ResultFguiMediator : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.Result;

        [Header("FGUI package/component")]
        [SerializeField] private string packageName = "Game";
        [SerializeField] private string componentName = "ResultPanel";
        [Tooltip("登顶奖章行（图标+文字）在 ResultPanel 内的节点名；空则跳过。")]
        [SerializeField] private string medalRowObjectName = "MedalReward";
        [Tooltip("本局钻石行（图标+文字）；空则跳过。请在 FGUI 组件中与奖章行同级命名。")]
        [SerializeField] private string gemsRowObjectName = "GemsReward";

        [Header("动画")]
        [SerializeField] private float slideDurationSeconds = 0.38f;
        [SerializeField] private float settleDurationSeconds = 0.28f;
        [SerializeField] private float overshootDownPx = 28f;

        GComponent _view;
        GComponent _mainPanel;
        GObject _mask;
        SettlementViewModel _model;
        EventCallback1 _evContinue;
        EventCallback1 _evDouble;
        EventCallback1 _evClose;
        GButton _btnContinue;
        GButton _btnDouble;
        GButton _btnCloseRoot;

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable()
        {
            KillMainPanelTweens();
            UiCoordinator.Unregister(this);
        }

        void OnDestroy()
        {
            KillMainPanelTweens();
            UnwireUi();
            if (_view == null || _view.isDisposed)
            {
                _view = null;
                return;
            }

            _view.Dispose();
            _view = null;
            _mainPanel = null;
            _mask = null;
        }

        public void Open(UiOpenPayload payload = null)
        {
            EnsureView();
            if (_view == null)
                return;

            _model = payload?.Settlement;
            if (_model == null)
            {
                Debug.LogWarning("ResultFguiMediator: 无 SettlementViewModel。");
                return;
            }

            KillMainPanelTweens();
            WireUi();
            ApplyTexts(_model);

            _view.visible = true;
            _view.MakeFullScreen();
            AudioGameplayFeedback.PlayPanelOpen();
            PlayIntroFromTop();
        }

        public void Close()
        {
            KillMainPanelTweens();
            UnwireUi();
            if (_view != null)
                _view.visible = false;
            _model = null;
        }

        void EnsureView()
        {
            if (_view != null)
                return;

            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(componentName))
                return;

            _view = UIPackage.CreateObject(packageName, componentName)?.asCom;
            if (_view != null)
                GRoot.inst.AddChild(_view);

            _mask = FindDeep(_view, "Mask");
            if (_mask != null)
            {
                _mask.touchable = true;
                _mask.onClick.Clear();
            }

            _mainPanel = FindDeep(_view, "MainPanel")?.asCom;
        }

        void WireUi()
        {
            UnwireUi();
            var btnRoot = FindDeep(_view, "Btn")?.asCom;
            _btnContinue = FindDeep(btnRoot ?? _view, "Continue")?.asButton;
            _btnDouble = FindDeep(btnRoot ?? _view, "BtnUse")?.asButton;
            _btnCloseRoot = FindDeep(_view, "Close")?.asButton;

            if (_btnContinue != null)
            {
                _evContinue = _ => OnContinueClicked();
                _btnContinue.onClick.Add(_evContinue);
            }

            if (_btnDouble != null)
            {
                _evDouble = _ => OnDoubleRewardClicked();
                _btnDouble.onClick.Add(_evDouble);
            }

            if (_btnCloseRoot != null)
            {
                _evClose = _ => OnContinueClicked();
                _btnCloseRoot.onClick.Add(_evClose);
            }
        }

        void UnwireUi()
        {
            if (_btnContinue != null && _evContinue != null)
                _btnContinue.onClick.Remove(_evContinue);
            if (_btnDouble != null && _evDouble != null)
                _btnDouble.onClick.Remove(_evDouble);
            if (_btnCloseRoot != null && _evClose != null)
                _btnCloseRoot.onClick.Remove(_evClose);
            _evContinue = null;
            _evDouble = null;
            _evClose = null;
            _btnContinue = null;
            _btnDouble = null;
            _btnCloseRoot = null;
        }

        void OnContinueClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            FinishAndRespawn();
        }

        void OnDoubleRewardClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            if (_model == null || SaveService.Data == null || !_model.HasDoubleReward)
            {
                FinishAndRespawn();
                return;
            }

            var extra = _model.BuildDoubleRewardExtra();
            FreeRewardGate.Grant(() =>
            {
                RewardApplier.Apply(extra);
                SaveService.Save();
                UiCoordinator.Hud?.RefreshEconomyDisplay();
                FinishAndRespawn();
            });
        }

        void FinishAndRespawn()
        {
            Close();
            RespawnService.RespawnPlayerToActivePoint();
        }

        void ApplyTexts(SettlementViewModel vm)
        {
            var content = FindDeep(_view, "Content")?.asCom;
            SetMoneyRow(vm.CoinsReward, FindDeep(content ?? _view, "Reward"));

            SetOptionalText(FindDeep(content ?? _view, "CurDistance"),
                $"滑翔距离: {vm.GlideDistanceFromTowerMeters:0}米");
            SetOptionalText(FindDeep(content ?? _view, "RecordDistance"),
                $"(最远距离: {vm.RecordGlideDistanceMeters:0}米)");
            SetOptionalText(FindDeep(content ?? _view, "Height"),
                $"爬塔高度: {vm.HeightMeters:0}米");

            var titleLabel = FindDeep(FindDeep(_view, "Title")?.asCom ?? _view, "Label")?.asTextField;
            if (titleLabel != null) titleLabel.text = vm.Title;

            ApplyMedalRow(vm, content ?? _view);
            ApplyGemsRow(vm, content ?? _view);
        }

        void ApplyGemsRow(SettlementViewModel vm, GComponent scope)
        {
            if (string.IsNullOrEmpty(gemsRowObjectName) || scope == null)
                return;

            var gemRoot = FindDeep(scope, gemsRowObjectName);
            if (gemRoot == null)
                return;

            var n = vm.GemsReward;
            gemRoot.visible = n > 0;

            if (n <= 0)
                return;

            var label = gemRoot.asCom?.GetChild("Label")?.asTextField
                        ?? FindDeep(gemRoot.asCom, "Label")?.asTextField;
            if (label != null)
                label.text = $"钻石 x{n}";
        }

        void ApplyMedalRow(SettlementViewModel vm, GComponent scope)
        {
            if (string.IsNullOrEmpty(medalRowObjectName) || scope == null)
                return;

            var medalRoot = FindDeep(scope, medalRowObjectName);
            if (medalRoot == null)
                return;

            var n = vm.TowerClearMedalCount;
            medalRoot.visible = n > 0;

            if (n <= 0)
                return;

            var label = medalRoot.asCom?.GetChild("Label")?.asTextField
                        ?? FindDeep(medalRoot.asCom, "Label")?.asTextField;
            if (label != null)
                label.text = $"登顶奖章 x{n}";
        }

        static void SetMoneyRow(long coins, GObject rewardNode)
        {
            if (rewardNode == null)
                return;
            var label = rewardNode.asCom?.GetChild("Label")?.asTextField;
            if (label != null)
                label.text = $"x{coins}";
        }

        static void SetOptionalText(GObject node, string text)
        {
            if (node == null || string.IsNullOrEmpty(text))
                return;
            if (node is GTextField tf)
            {
                tf.text = text;
                return;
            }

            var com = node.asCom;
            var child = com?.GetChild("Label")?.asTextField ?? com?.GetChild("title")?.asTextField;
            if (child != null) child.text = text;
        }

        void PlayIntroFromTop()
        {
            if (_mainPanel == null)
                return;

            var rest = _mainPanel.xy;
            float drop = GRoot.inst.height * 0.55f + _mainPanel.height + 120f;
            _mainPanel.SetXY(rest.x, rest.y - drop);

            _mainPanel.TweenMove(new Vector2(rest.x, rest.y + overshootDownPx), slideDurationSeconds)
                .SetEase(EaseType.CubicOut)
                .OnComplete(() =>
                {
                    _mainPanel.TweenMove(rest, settleDurationSeconds).SetEase(EaseType.BackOut);
                });
        }

        void KillMainPanelTweens()
        {
            if (_mainPanel != null)
                GTween.Kill(_mainPanel, TweenPropType.XY, false);
        }

        static GObject FindDeep(GComponent parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
                return null;

            var direct = parent.GetChild(name);
            if (direct != null)
                return direct;

            for (var i = 0; i < parent.numChildren; i++)
            {
                var c = parent.GetChildAt(i).asCom;
                if (c == null) continue;
                var found = FindDeep(c, name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
