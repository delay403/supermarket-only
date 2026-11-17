using Game.Audio;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>超市收纳结算：通关或倒计时结束时弹出，展示本局积分与转入金币。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeResultPanel : MonoBehaviour, IUiPanelController
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform mainPanel;
        [SerializeField] private Image maskImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text shelfCountText;
        [SerializeField] private Text itemCountText;
        [SerializeField] private Button returnMapButton;
        [SerializeField] private Sprite runScoreFrameSprite;
        [SerializeField] private Sprite runScoreIconSprite;

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.38f;
        [SerializeField] private float settleDuration = 0.28f;
        [SerializeField] private float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        SupermarketOrganizeResultViewModel _viewModel;
        Text _runScoreText;
        GameObject _runScoreRoot;

        public UiPanelId PanelId => UiPanelId.SupermarketOrganizeResult;

        void Awake()
        {
            returnMapButton?.onClick.AddListener(OnReturnMapClicked);
            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null)
        {
            _viewModel = payload?.SupermarketOrganizeResult;
            if (_viewModel == null)
            {
                Debug.LogWarning($"{nameof(SupermarketOrganizeResultPanel)}: 缺少结算数据。");
                return;
            }

            BindTexts(_viewModel);
            AudioGameplayFeedback.PlayPanelOpen();
            Show();
        }

        public void Close() => Hide();

        void BindTexts(SupermarketOrganizeResultViewModel vm)
        {
            if (titleText != null)
                titleText.text = vm.IsVictory ? "整理完毕" : "时间到";

            if (messageText != null)
                messageText.text = vm.IsVictory ? "超市整理顺利完毕！" : "时间到，本局未全部完成。";

            if (shelfCountText != null)
            {
                shelfCountText.supportRichText = true;
                shelfCountText.text = $"已收拾货架：<color=#4BE06C>{vm.CompletedShelfCount}</color>/{vm.TotalShelfCount}个";
            }

            if (itemCountText != null)
            {
                itemCountText.supportRichText = true;
                itemCountText.text = $"已摆放商品：<color=#4BE06C>{vm.PlacedItemCount}</color>/{vm.TotalItemSlotCount}个";
            }

            BindRunScore(vm);
        }

        void BindRunScore(SupermarketOrganizeResultViewModel vm)
        {
            EnsureRunScoreUi();
            if (_runScoreText == null)
                return;

            _runScoreText.supportRichText = true;
            _runScoreText.text =
                $"本局获得积分：<color=#FFD76A>{vm.RunScore}</color>" +
                $"\n转入金币：<color=#FFD76A>{vm.CoinsGranted}</color>";
        }

        void EnsureRunScoreUi()
        {
            if (_runScoreText != null)
                return;

            if (mainPanel == null)
                return;

            _runScoreRoot = new GameObject("RunScore", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rootRt = _runScoreRoot.GetComponent<RectTransform>();
            rootRt.SetParent(mainPanel, false);
            rootRt.SetSiblingIndex(2);
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = new Vector2(0f, 160f);
            rootRt.sizeDelta = new Vector2(420f, 86f);

            var frame = _runScoreRoot.GetComponent<Image>();
            frame.raycastTarget = false;
            if (runScoreFrameSprite != null)
            {
                frame.sprite = runScoreFrameSprite;
                frame.color = Color.white;
                frame.preserveAspect = false;
            }
            else
            {
                frame.color = new Color(0.16f, 0.1f, 0.05f, 0.72f);
            }

            if (runScoreIconSprite != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.SetParent(rootRt, false);
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(46f, 0f);
                iconRt.sizeDelta = new Vector2(48f, 48f);
                var icon = iconGo.GetComponent<Image>();
                icon.sprite = runScoreIconSprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            var textGo = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(rootRt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(runScoreIconSprite != null ? 80f : 16f, 4f);
            textRt.offsetMax = new Vector2(-16f, -4f);
            _runScoreText = textGo.GetComponent<Text>();
            _runScoreText.font = shelfCountText != null && shelfCountText.font != null
                ? shelfCountText.font
                : (messageText != null ? messageText.font : Resources.Load<Font>("喜脉体"));
            _runScoreText.fontSize = 28;
            _runScoreText.alignment = TextAnchor.MiddleLeft;
            _runScoreText.color = Color.white;
            _runScoreText.raycastTarget = false;
            _runScoreText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _runScoreText.verticalOverflow = VerticalWrapMode.Overflow;
            _runScoreText.lineSpacing = 1.05f;
            var outline = textGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.2627451f, 0.13333334f, 0.015686275f, 1f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        void OnReturnMapClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            HideImmediate();

            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            if (director != null)
                director.ShowHome();
            else
                UiRouter.OpenSupermarketOrganizeHome();
        }

        void Show()
        {
            EnsureSlideHost();
            if (maskImage != null)
                maskImage.raycastTarget = true;
            slideHost.PlayOpen();
        }

        void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;

            slideHost.RequestClose(() => _viewModel = null);
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
            _viewModel = null;
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
    }
}
