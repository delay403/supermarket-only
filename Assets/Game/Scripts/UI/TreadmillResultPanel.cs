using Game.Audio;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>跑步机锻炼结算面板；须在场景中注册为 <see cref="UiPanelId.TreadmillResult"/>。</summary>
    public sealed class TreadmillResultPanel : MonoBehaviour, IUiPanelController
    {
        public GameObject root;
        public RectTransform mainPanel;
        public Button maskCloseButton;
        public Button closeButton;
        public Text titleText;
        public Text bodyText;
        public Text totalClimbSpeedText;

        [Header("Rich Text")]
        public Color labelColor = new Color(0.67f, 0.67f, 0.67f, 1f);
        public Color tierColor = new Color(1f, 0.84f, 0.31f, 1f);
        public Color durationColor = Color.white;
        public Color gainColor = new Color(0.3f, 0.85f, 0.39f, 1f);
        public Color instantGainColor = new Color(0.98f, 0.55f, 0.2f, 1f);
        public Color arrowColor = new Color(1f, 0.75f, 0.2f, 1f);
        public Color totalBeforeColor = new Color(0.51f, 0.83f, 0.98f, 1f);
        public Color totalAfterColor = new Color(0.3f, 0.85f, 0.39f, 1f);
        public Color currentClimbSpeedColor = new Color(0.4f, 0.92f, 1f, 1f);
        public string arrowSymbol = "▲";
        [Range(1, 12)] public int maxArrowCount = 8;

        [Header("Animation")]
        public float slideDuration = 0.38f;
        public float settleDuration = 0.28f;
        public float overshootDownPx = 28f;

        [SerializeField] private UiSlidingPanelHost slideHost;

        TreadmillResultViewModel _vm;

        public UiPanelId PanelId => UiPanelId.TreadmillResult;

        void Awake()
        {
            maskCloseButton?.onClick.AddListener(OnDismissClicked);
            closeButton?.onClick.AddListener(OnDismissClicked);

            if (bodyText != null)
                bodyText.supportRichText = true;

            EnsureSlideHost();
            HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        void OnDestroy() => slideHost?.KillTweens();

        public void Open(UiOpenPayload payload = null)
        {
            _vm = payload?.TreadmillResult;
            if (_vm == null)
            {
                Debug.LogWarning($"{nameof(TreadmillResultPanel)}: 缺少 TreadmillResult payload。");
                return;
            }

            if (titleText != null)
                titleText.text = "锻炼结算";

            if (bodyText != null)
                bodyText.text = BuildRichBodyText(_vm);

            RefreshTotalClimbSpeedText(_vm);

            Show();
        }

        void RefreshTotalClimbSpeedText(TreadmillResultViewModel vm)
        {
            if (totalClimbSpeedText == null)
                return;

            totalClimbSpeedText.supportRichText = true;
            var label = Hex(labelColor);
            var value = Hex(currentClimbSpeedColor);
            totalClimbSpeedText.text =
                $"<color=#{label}>当前爬塔速度</color>  <color=#{value}>{vm.CoreClimbSpeed:0.###} m/s</color>";
        }

        public void Close() => Hide();

        string BuildRichBodyText(TreadmillResultViewModel vm)
        {
            var label = Hex(labelColor);
            var tier = Hex(tierColor);
            var duration = Hex(durationColor);
            var gain = Hex(gainColor);
            var instantGainHex = Hex(instantGainColor);
            var arrow = Hex(arrowColor);
            var before = Hex(totalBeforeColor);
            var after = Hex(totalAfterColor);

            var arrowCount = vm.GetGainArrowCount(maxArrowCount);
            var arrowChar = string.IsNullOrEmpty(arrowSymbol) ? '▲' : arrowSymbol[0];
            var arrows = arrowCount > 0
                ? $"<color=#{arrow}>{new string(arrowChar, arrowCount)}</color> "
                : string.Empty;

            string gainLine;
            if (vm.SessionClimbSpeedGain <= 0.0001f)
            {
                gainLine = $"<color=#{label}>本次无提升</color>";
            }
            else if (vm.SessionInstantGain > 0.0001f)
            {
                gainLine =
                    $"{arrows}<color=#{gain}>+{vm.SessionClimbSpeedGain:0.###} m/s</color>\n" +
                    $"<color=#{label}>  跑步</color> <color=#{gain}>+{vm.SessionRunningGain:0.###}</color>  " +
                    $"<color=#{label}>领取</color> <color=#{instantGainHex}>+{vm.SessionInstantGain:0.###}</color>";
            }
            else
            {
                gainLine = $"{arrows}<color=#{gain}>+{vm.SessionClimbSpeedGain:0.###} m/s</color>";
            }

            var totalLine = vm.SessionClimbSpeedGain > 0.0001f
                ? $"<color=#{before}>{vm.PreviousPermanentBonus:0.###}</color> " +
                  $"<color=#{arrow}>→</color> " +
                  $"<color=#{after}>{vm.TotalPermanentClimbSpeedBonus:0.###} m/s</color>"
                : $"<color=#{after}>+{vm.TotalPermanentClimbSpeedBonus:0.###} m/s</color>";

            var climbSpeedLine = totalClimbSpeedText == null
                ? $"\n\n<color=#{label}>当前爬塔速度</color>  <color=#{Hex(currentClimbSpeedColor)}>{vm.CoreClimbSpeed:0.###} m/s</color>"
                : string.Empty;

            return
                $"<color=#{label}>档位</color>  <color=#{tier}>{vm.TierLabel}</color>\n" +
                $"<color=#{label}>时长</color>  <color=#{duration}>{vm.FormattedDuration}</color>\n\n" +
                $"<color=#{label}>本次提升</color>\n{gainLine}\n\n" +
                $"<color=#{label}>跑步机永久加成</color>\n{totalLine}{climbSpeedLine}";
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        void Show()
        {
            EnsureSlideHost();
            slideHost.PlayOpen();
        }

        void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;
            slideHost.RequestClose(() => _vm = null);
        }

        void HideImmediate()
        {
            EnsureSlideHost();
            slideHost.HideImmediate();
            _vm = null;
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

        void OnDismissClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            Hide();
        }
    }
}
