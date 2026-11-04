using Game.Audio;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>超市收纳胜利结算：全部货架与物品整理完成后弹出。</summary>
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

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.38f;
        [SerializeField] private float settleDuration = 0.28f;
        [SerializeField] private float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        SupermarketOrganizeResultViewModel _viewModel;

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
                titleText.text = "整理完毕";

            if (messageText != null)
                messageText.text = "超市整理顺利完毕！";

            var shelfCount = vm.CompletedShelfCount > 0 ? vm.CompletedShelfCount : vm.TotalShelfCount;
            var itemCount = vm.PlacedItemCount > 0 ? vm.PlacedItemCount : vm.TotalItemSlotCount;

            if (shelfCountText != null)
            {
                shelfCountText.supportRichText = true;
                shelfCountText.text = $"已收拾货架：<color=#4BE06C>{shelfCount}</color>个";
            }

            if (itemCountText != null)
            {
                itemCountText.supportRichText = true;
                itemCountText.text = $"已摆放商品：<color=#4BE06C>{itemCount}</color>个";
            }
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
