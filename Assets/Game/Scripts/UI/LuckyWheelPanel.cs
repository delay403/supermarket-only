using DG.Tweening;

using Game.Reward;

using Game.Audio;

using Game.Core.Config;

using Game.DailySignIn;

using Game.Economy;

using Game.Features.LuckyWheel;

using Game.Save;

using Game.UI.Framework;

using UnityEngine;

using UnityEngine.UI;



namespace Game.UI

{

    /// <summary>

    /// UGUI 幸运转盘：FGUI BtnLoop 打开；从左向右弹入；免费次数每日三次，领取后扣次数并转盘发奖。

    /// </summary>

    public sealed class LuckyWheelPanel : MonoBehaviour, IUiPanelController

    {

        public UiPanelId PanelId => UiPanelId.LuckyWheel;



        [Header("Bindings")]

        public GameObject root;

        public RectTransform mainPanel;

        public Image maskImage;

        public Button maskCloseButton;

        public Button closeButton;



        [Header("Wheel")]

        public RectTransform wheelGraphic;



        [Tooltip("转盘上有序排列的 8 个子物体；留空且在 Slots Root 上赋值则从子节点解析。")]

        public WheelRewardSlotView[] wheelSlots = new WheelRewardSlotView[8];



        public Transform slotsRoot;

        public Button dailyFreeSpinButton;

        public Text dailySpinCounterText;



        [Header("Result popup")]

        public GameObject rewardPopupRoot;

        public Image rewardPopupIcon;

        public Text rewardPopupText;

        public Button rewardClaimButton;



        [Header("Animation (left → right)")]

        public float slideDuration = 0.42f;

        public float settleDuration = 0.32f;

        public float overshootRightPx = 72f;

        [SerializeField] private float closeSpeedMultiplier = 1.55f;



        [SerializeField] private UiSlidingPanelHost slideHost;



        [Header("Spin")]

        public float spinDurationSeconds = 3.1f;

        public int fullSpinLoops = 5;



        [Header("Pointer vs slices (对齐指针与中奖索引)")]

        [Tooltip("扇区数量，须与 WheelDatabase 条目数、场景子物体数一致。")]

        [SerializeField] private int sliceCount = 8;



        [Tooltip("固定指针从转盘中心所指的方向（度）。Canvas 使用 X 右 Y 上时，指针在正上方多为 90°。")]

        [SerializeField] private float pointerReferenceDegrees = 90f;



        [Tooltip("wheelGraphic.z=0 时，索引 0 扇区<strong>中心</strong>相对同一角度坐标的方向（度）。常见美术把索引 0 摆在正上方则为 90°。")]

        [SerializeField] private float slice0CenterDegrees = 90f;



        [Tooltip("索引增大时，扇区中心沿顺时针还是逆时针排布（俯视转盘）。")]

        [SerializeField] private bool sliceIndexIncreasesClockwise = true;



        [Tooltip("整体微调：在算得的停轮角上再加一度偏移，用于对齐美术与指针有误差时。")]

        [SerializeField] private float spinResultFineTuneDegrees = 0f;



        [Tooltip("false 时整段旋转绕负 Z（与 true 相反），转盘转向相反时可切换。")]

        [SerializeField] private bool spinIncreasesPositiveZ = true;



        Tweener _spinTween;

        bool _spinning;

        int _lastWinIndex;

        RewardDef _lastReward;



        private void Awake()

        {

            if (maskCloseButton != null)

                maskCloseButton.onClick.AddListener(OnDismissClicked);

            if (closeButton != null)

                closeButton.onClick.AddListener(OnDismissClicked);

            if (dailyFreeSpinButton != null)

                dailyFreeSpinButton.onClick.AddListener(OnDailySpinClicked);

            EnsureCenterSpinButton();

            if (rewardClaimButton != null)

                rewardClaimButton.onClick.AddListener(OnRewardClaimClicked);



            if (maskImage != null)

                maskImage.raycastTarget = true;



            if (rewardPopupRoot != null)

                rewardPopupRoot.SetActive(false);



            EnsureSlideHost();

            HideImmediate();

        }



        private void OnDestroy()

        {

            slideHost?.KillTweens();

            if (_spinTween != null && _spinTween.IsActive())

                _spinTween.Kill();

        }



        private void OnEnable() => UiCoordinator.Register(this);



        private void OnDisable() => UiCoordinator.Unregister(this);



        public void Open(UiOpenPayload payload = null) => Show();



        public void Close() => Hide();



        public void Show()
        {
            EnsureSlideHost();
            HideRewardPopupSilent();
            SetDismissControlsInteractable(true);
            slideHost.PlayOpen(PopulateAfterOpen);
        }

        void PopulateAfterOpen()
        {
            ResolveWheelSlots();
            RefreshSlicesFromDatabase();
            RefreshDailyCounterUi();
        }



        void OnDismissClicked()

        {

            if (_spinning)

                return;

            Hide();

        }



        void SetDismissControlsInteractable(bool interactable)

        {

            if (closeButton != null)

                closeButton.interactable = interactable;

            if (maskCloseButton != null)

                maskCloseButton.interactable = interactable;

        }



        public void Hide()

        {

            if (_spinning)

                return;



            EnsureSlideHost();

            if (!slideHost.IsShown || slideHost.IsClosing)

                return;



            HideRewardPopupSilent();

            if (_spinTween != null && _spinTween.IsActive())

                _spinTween.Kill();

            _spinning = false;



            SetDismissControlsInteractable(false);

            slideHost.RequestClose(() => SetDismissControlsInteractable(true));

        }



        void HideImmediate()

        {

            HideRewardPopupSilent();

            if (_spinTween != null && _spinTween.IsActive())

                _spinTween.Kill();

            _spinning = false;

            SetDismissControlsInteractable(true);

            EnsureSlideHost();

            slideHost.HideImmediate();

        }



        void EnsureSlideHost()

        {

            UiSlidingPanelHost.EnsureOn(

                this,

                ref slideHost,

                root,

                mainPanel,

                UiSlidingPanelHost.SlideDirection.FromRight,

                slideDuration,

                settleDuration,

                overshootRightPx,

                closeSpeed: closeSpeedMultiplier);

        }



        void ResolveWheelSlots()

        {

            if (wheelSlots == null || wheelSlots.Length != 8)

                wheelSlots = new WheelRewardSlotView[8];



            var allSet = true;

            for (var i = 0; i < 8; i++)

            {

                if (wheelSlots[i] == null) { allSet = false; break; }

            }



            if (allSet) return;



            if (slotsRoot == null) return;



            var resolved = new WheelRewardSlotView[8];

            var childCount = slotsRoot.childCount;

            for (var i = 0; i < 8 && i < childCount; i++)

            {

                var ch = slotsRoot.GetChild(i);

                resolved[i] = ch.GetComponentInChildren<WheelRewardSlotView>(true);

            }



            wheelSlots = resolved;

        }



        void RefreshSlicesFromDatabase()

        {

            var db = ConfigRegistry.Wheel;

            if (db == null || db.slices == null)

            {

                Debug.LogWarning("LuckyWheelPanel: ConfigRegistry.Wheel 未配置（Resources/WheelDatabase 或 Bootstrapper 绑定）。");

                return;

            }



            for (var i = 0; i < 8; i++)

            {

                var slot = wheelSlots != null && i < wheelSlots.Length ? wheelSlots[i] : null;

                if (slot == null) continue;



                var slice = db.GetSlice(i);

                if (slice == null) continue;



                slot.Bind(slice.reward, slice.iconOverride, slice.labelOverride);

            }

        }



        void RefreshDailyCounterUi()

        {

            LuckyWheelService.RefreshDayFlag();

            var left = LuckyWheelService.FreeSpinsRemainingToday();

            if (dailySpinCounterText != null)

                dailySpinCounterText.text = $"{left}/{LuckyWheelService.MaxFreeSpinsPerGameDay}";



            var can = left > 0 && !_spinning;

            if (dailyFreeSpinButton != null) dailyFreeSpinButton.interactable = can;

        }



        void EnsureCenterSpinButton()

        {

            if (wheelGraphic == null)

                return;



            var center = wheelGraphic.parent != null ? wheelGraphic.parent.Find("Center") : null;

            if (center == null)

                return;



            var button = center.GetComponent<Button>();

            if (button == null)

                button = center.gameObject.AddComponent<Button>();



            button.onClick.RemoveListener(OnDailySpinClicked);

            button.onClick.AddListener(OnDailySpinClicked);

        }



        void OnDailySpinClicked()

        {

            AudioGameplayFeedback.PlayUiButton();

            if (_spinning) return;



            if (LuckyWheelService.FreeSpinsRemainingToday() <= 0)

            {

                AudioGameplayFeedback.PlayUiError();

                RefreshDailyCounterUi();

                return;

            }



            FreeRewardGate.Grant(() =>

            {

                EnsureSlideHost();

                if (!slideHost.IsShown || root == null || !root.activeInHierarchy)

                    return;

                if (_spinning)

                    return;



                if (ConfigRegistry.Wheel == null || wheelGraphic == null)

                {

                    Debug.LogWarning("LuckyWheelPanel: 缺少 WheelDatabase 或 wheelGraphic，无法转盘。");

                    AudioGameplayFeedback.PlayUiError();

                    return;

                }



                if (!LuckyWheelService.TryConsumeFreeSpin())

                {

                    AudioGameplayFeedback.PlayUiError();

                    RefreshDailyCounterUi();

                    return;

                }



                RefreshDailyCounterUi();

                RunSpinAnimation();

            });

        }



        void RunSpinAnimation()

        {

            var db = ConfigRegistry.Wheel;

            if (db == null || wheelGraphic == null)

            {

                Debug.LogWarning("LuckyWheelPanel: 缺少 WheelDatabase 或 wheelGraphic。");

                return;

            }



            _spinning = true;

            if (dailyFreeSpinButton != null) dailyFreeSpinButton.interactable = false;

            SetDismissControlsInteractable(false);



            var n = ResolvedSliceCount();

            _lastWinIndex = db.PickWeightedIndex(n);

            var slice = db.GetSlice(_lastWinIndex);

            _lastReward = slice != null ? slice.reward : default;



            var targetZ = ComputeSpinTargetLocalEulerZ(wheelGraphic.localEulerAngles.z, _lastWinIndex, n);



            _spinTween?.Kill();

            _spinTween = wheelGraphic

                .DOLocalRotate(new Vector3(0f, 0f, targetZ), spinDurationSeconds, RotateMode.FastBeyond360)

                .SetEase(Ease.OutCubic)

                .OnComplete(OnSpinCompleted);

        }



        int ResolvedSliceCount()

        {

            var db = ConfigRegistry.Wheel;

            if (db != null && db.slices != null && db.slices.Count > 0)

                return Mathf.Max(1, db.slices.Count);

            return Mathf.Max(1, sliceCount);

        }



        /// <summary>

        /// 扇区索引 <paramref name="sliceIndex"/> 的中心转到指针方向时，转盘应有的 localEulerAngles.z（含整圈数）。

        /// 约定：子物体随转盘旋转，指针在父级/世界中固定；扇区中心角与指针角同一坐标系（X 右 Y 上，逆时针为度增大）。

        /// </summary>

        float ComputeSpinTargetLocalEulerZ(float currentLocalZ, int sliceIndex, int n)

        {

            var sliceCenter = ResolveSliceCenterDegrees(sliceIndex, n);

            var landingNorm = NormalizeAngle360(pointerReferenceDegrees - sliceCenter + spinResultFineTuneDegrees);



            var curNorm = NormalizeAngle360(currentLocalZ);

            var delta = NormalizeAngle360(landingNorm - curNorm);

            if (delta < 0.01f)

                delta = 360f;



            var loopsPart = fullSpinLoops * 360f;

            var signedLoops = spinIncreasesPositiveZ ? loopsPart : -loopsPart;

            var signedDelta = spinIncreasesPositiveZ ? delta : -delta;

            return currentLocalZ + signedLoops + signedDelta;

        }



        float ResolveSliceCenterDegrees(int sliceIndex, int n)

        {

            if (wheelSlots != null && sliceIndex >= 0 && sliceIndex < wheelSlots.Length)

            {

                var slot = wheelSlots[sliceIndex];

                if (slot != null)

                {

                    var slotRt = slot.transform as RectTransform;

                    if (slotRt != null)

                    {

                        var localPos = slotRt.localPosition;

                        if (localPos.sqrMagnitude > 1f)

                            return NormalizeAngle360(Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg);

                    }

                }

            }



            var step = 360f / Mathf.Max(1, n);

            var dir = sliceIndexIncreasesClockwise ? -1f : 1f;

            return NormalizeAngle360(slice0CenterDegrees + dir * Mathf.Clamp(sliceIndex, 0, n - 1) * step);

        }



        static float NormalizeAngle360(float degrees)

        {

            var x = degrees % 360f;

            if (x < 0f) x += 360f;

            return x;

        }



        void OnSpinCompleted()

        {

            _spinning = false;

            RefreshDailyCounterUi();

            SetDismissControlsInteractable(true);



            RewardGrant.Apply(_lastReward);

            SaveService.Save();

            AudioGameplayFeedback.PlayResourceCollect();

            UiCoordinator.Hud?.RefreshEconomyDisplay();

            UiCoordinator.RefreshEquipmentVisuals();

            EconomyDisplayEvents.Raise();



            ShowRewardPopup(_lastReward);

        }



        void ShowRewardPopup(in RewardDef reward)

        {

            if (rewardPopupRoot == null) return;



            var slice = ConfigRegistry.Wheel?.GetSlice(_lastWinIndex);

            var spr = slice != null && slice.iconOverride != null

                ? slice.iconOverride

                : RewardPresentation.TryResolveIcon(reward);



            if (rewardPopupIcon != null)

            {

                rewardPopupIcon.sprite = spr;

                rewardPopupIcon.enabled = spr != null;

            }



            if (rewardPopupText != null)

                rewardPopupText.text = RewardPresentation.FormatRewardLine(reward);



            rewardPopupRoot.SetActive(true);

        }



        void OnRewardClaimClicked()

        {

            AudioGameplayFeedback.PlayUiButton();

            HideRewardPopupSilent();

        }



        void HideRewardPopupSilent()

        {

            if (rewardPopupRoot != null)

                rewardPopupRoot.SetActive(false);

        }

    }

}


