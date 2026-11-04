using System;
using DG.Tweening;
using Game.Audio;
using Game.Maps;
using Game.Platform;
using Game.Save;
using Game.UI.Controls;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// UGUI 设置：音效 / BGM 音量、振动开关；自屏幕左侧滑入；由 FGUI <c>BtnSetting</c> 打开。
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour, IUiPanelController
    {
        public UiPanelId PanelId => UiPanelId.Settings;

        [Header("Bindings")]
        public GameObject root;
        public RectTransform mainPanel;
        public Image maskImage;
        public Button closeButton;

        [Header("Sliders")]
        public Slider sfxSlider;
        public Slider bgmSlider;

        [Header("Icons (mute when 0 or toggle off)")]
        public Image sfxIcon;
        public Image bgmIcon;
        public Image vibrationIcon;
        public Sprite sfxIconNormal;
        public Sprite sfxIconMuted;
        public Sprite bgmIconNormal;
        public Sprite bgmIconMuted;
        public Sprite vibrationIconNormal;
        public Sprite vibrationIconMuted;

        [Header("Vibration")]
        public CustomSlidingToggle vibrationToggle;

        [Header("Footer")]
        public Button continueGameButton;
        public Button returnHomeButton;
        [Tooltip("可选：Continue / ReturnHome 的父节点；隐藏主页键后会 ForceRebuildLayout，避免单按钮仍偏在一侧。")]
        public RectTransform footerButtonsLayoutRoot;
        [Tooltip("可选：在发起回主页跳转前触发（例如存档、埋点）。默认实际加载由 GameSceneLoader.NavigateToMainHome 执行；若已注册 returnHomeOverride 则由其接管。")]
        public UnityEvent onReturnToHome;

        /// <summary>
        /// 玩法场景可注册的「返回主页」替代实现（例如单玩法包体回到模式内主页）。
        /// 非 null 时按钮保持可见且不再切换 Unity 场景。由 <see cref="SetReturnHomeOverride"/> 设置。
        /// </summary>
        [NonSerialized]
        public Action returnHomeOverride;

        [Header("Animation")]
        [SerializeField] private UiSlidingPanelHost slideHost;
        public bool playSlideAnimation = true;

        [Header("Icon swap animation")]
        [Tooltip("图标先在当前图上放大再换图再收回一半耗时（缩放 UP / DOWN 各占一格）。")]
        public float iconSwapPopHalfSeconds = 0.09f;

        Sequence _sfxIconTweenSeq;
        Sequence _bgmIconTweenSeq;
        Sequence _vibrationIconTweenSeq;

        Sprite _appliedSfxSprite;
        Sprite _appliedBgmSprite;
        Sprite _appliedVibrationSprite;

        Sprite _sfxTweenSpriteGoal;
        Sprite _bgmTweenSpriteGoal;
        Sprite _vibrationTweenSpriteGoal;
        bool _shown;

        void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);
            if (continueGameButton != null)
                continueGameButton.onClick.AddListener(OnContinueClicked);
            if (returnHomeButton != null)
                returnHomeButton.onClick.AddListener(OnReturnHomeClicked);

            if (sfxSlider != null)
                sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            if (bgmSlider != null)
                bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);

            WireSliderReleaseSound(sfxSlider);
            WireSliderReleaseSound(bgmSlider);

            if (vibrationToggle != null)
                vibrationToggle.ValueChanged += OnVibrationChanged;

            EnsureSlideHost();
            HideImmediate();
        }

        void OnDestroy()
        {
            KillPanelTweens();
            if (vibrationToggle != null)
                vibrationToggle.ValueChanged -= OnVibrationChanged;
            // 切场景时 Hide() 动画可能来不及 PopBlock，避免 Stage 触摸被永久屏蔽。
            if (_shown)
                HideImmediate();
        }

        void OnEnable() => UiCoordinator.Register(this);

        void OnDisable() => UiCoordinator.Unregister(this);

        public void Open(UiOpenPayload payload = null)
        {
            Show();
        }

        public void Close() => Hide();

        public void Show()
        {
            EnsureSlideHost();
            _shown = slideHost.IsShown;

            ApplyReturnHomeButtonVisibility();
            LoadFromSave();
            RefreshIcons(snapWithoutAnimation: true);

            if (playSlideAnimation)
                slideHost.PlayOpen();
            else
            {
                if (!slideHost.IsShown)
                    slideHost.PlayOpen();
                slideHost.KillTweens();
            }

            if (maskImage != null)
                maskImage.raycastTarget = true;
        }

        public void Hide()
        {
            EnsureSlideHost();
            if (!slideHost.IsShown || slideHost.IsClosing)
                return;

            if (!playSlideAnimation)
            {
                slideHost.HideImmediate();
                _shown = false;
                return;
            }

            slideHost.RequestClose(() => _shown = false);
        }

        void HideImmediate()
        {
            KillPanelTweens();
            slideHost?.HideImmediate();
            _shown = false;
        }

        void EnsureSlideHost()
        {
            UiSlidingPanelHost.EnsureOn(
                this,
                ref slideHost,
                root,
                mainPanel,
                UiSlidingPanelHost.SlideDirection.FromRight);
        }

        void LoadFromSave()
        {
            var data = SaveService.Data;
            if (sfxSlider != null)
                sfxSlider.SetValueWithoutNotify(data != null ? data.settingsSfxVolume01 : 1f);
            if (bgmSlider != null)
                bgmSlider.SetValueWithoutNotify(data != null ? data.settingsBgmVolume01 : 1f);

            if (AudioManager.Instance != null)
                AudioManager.Instance.ApplyUserVolumeFromSave();
            else if (data != null)
            {
                PlatformMediaMgr.UserSfxVolume01 = data.settingsSfxVolume01;
                PlatformMediaMgr.UserBgmVolume01 = data.settingsBgmVolume01;
            }

            var vib = data == null || data.settingsVibrationEnabled;
            PlatformMediaMgr.VibrationEnabled = vib;
            if (vibrationToggle != null)
                vibrationToggle.SetValue(vib, notify: false, animate: false);
        }

        void OnSfxSliderChanged(float v)
        {
            AudioManager.Instance?.SetUserSfxVolume01(v, true);
            RefreshIcons(snapWithoutAnimation: false);
        }

        void OnBgmSliderChanged(float v)
        {
            AudioManager.Instance?.SetUserBgmVolume01(v, true);
            RefreshIcons(snapWithoutAnimation: false);
        }

        void OnVibrationChanged(bool on)
        {
            PlatformMediaMgr.VibrationEnabled = on;
            if (SaveService.Data != null)
            {
                SaveService.Data.settingsVibrationEnabled = on;
                SaveService.Save();
            }

            RefreshIcons(snapWithoutAnimation: false);
        }

        void RefreshIcons(bool snapWithoutAnimation = false)
        {
            const float muteEps = 0.0001f;
            var sfxV = sfxSlider != null ? sfxSlider.value : 1f;
            var bgmV = bgmSlider != null ? bgmSlider.value : 1f;

            if (sfxIcon != null && sfxIconNormal != null && sfxIconMuted != null)
            {
                var target = sfxV <= muteEps ? sfxIconMuted : sfxIconNormal;
                ApplyIconTarget(sfxIcon, target, () => _appliedSfxSprite, s => _appliedSfxSprite = s,
                    ref _sfxIconTweenSeq, ref _sfxTweenSpriteGoal, snapWithoutAnimation);
            }

            if (bgmIcon != null && bgmIconNormal != null && bgmIconMuted != null)
            {
                var target = bgmV <= muteEps ? bgmIconMuted : bgmIconNormal;
                ApplyIconTarget(bgmIcon, target, () => _appliedBgmSprite, s => _appliedBgmSprite = s,
                    ref _bgmIconTweenSeq, ref _bgmTweenSpriteGoal, snapWithoutAnimation);
            }

            if (vibrationIcon != null && vibrationIconNormal != null && vibrationIconMuted != null)
            {
                var on = vibrationToggle == null || vibrationToggle.IsOn;
                var target = on ? vibrationIconNormal : vibrationIconMuted;
                ApplyIconTarget(vibrationIcon, target, () => _appliedVibrationSprite, s => _appliedVibrationSprite = s,
                    ref _vibrationIconTweenSeq, ref _vibrationTweenSpriteGoal, snapWithoutAnimation);
            }
        }

        void ApplyIconTarget(Image img, Sprite target, Func<Sprite> getApplied, Action<Sprite> setApplied,
            ref Sequence seq, ref Sprite tweenSpriteGoal, bool snap)
        {
            if (img == null || target == null)
                return;

            if (seq != null && seq.IsActive() && tweenSpriteGoal == target)
                return;

            var applied = getApplied();
            if (applied == target)
            {
                img.sprite = target;
                return;
            }

            if (snap || applied == null || iconSwapPopHalfSeconds <= 0.001f)
            {
                seq?.Kill();
                tweenSpriteGoal = null;
                img.rectTransform.localScale = Vector3.one;
                img.sprite = target;
                setApplied(target);
                return;
            }

            seq?.Kill();
            var rt = img.rectTransform;
            img.sprite = applied;
            rt.localScale = Vector3.one;

            tweenSpriteGoal = target;

            var half = iconSwapPopHalfSeconds;
            AudioGameplayFeedback.PlayPanelOpen();
            seq = DOTween.Sequence();
            seq.Append(rt.DOScale(1.15f, half).SetEase(Ease.OutQuad))
                .AppendCallback(() => img.sprite = target)
                .Append(rt.DOScale(1f, half).SetEase(Ease.InQuad))
                .OnComplete(() => setApplied(target))
                .OnKill(() =>
                {
                    if (rt != null)
                        rt.localScale = Vector3.one;
                });
        }

        static void WireSliderReleaseSound(Slider slider)
        {
            if (slider == null)
                return;

            var et = slider.GetComponent<EventTrigger>();
            if (et == null)
                et = slider.gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entry.callback.AddListener(_ => AudioGameplayFeedback.PlayUiSliderChange());
            et.triggers.Add(entry);
        }

        void KillPanelTweens()
        {
            slideHost?.KillTweens();
            _sfxIconTweenSeq?.Kill();
            _bgmIconTweenSeq?.Kill();
            _vibrationIconTweenSeq?.Kill();
            _sfxTweenSpriteGoal = null;
            _bgmTweenSpriteGoal = null;
            _vibrationTweenSpriteGoal = null;
            if (mainPanel != null)
                mainPanel.DOKill();
        }

        void OnCloseClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            Hide();
        }

        void OnContinueClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            Hide();
        }

        void OnReturnHomeClicked()
        {
            AudioGameplayFeedback.PlayUiButton();
            onReturnToHome?.Invoke();
            // 须同步 PopBlock；异步 Hide() 在 LoadScene 前来不及完成会导致主页 FGUI 按钮无法点击。
            HideImmediate();

            if (returnHomeOverride != null)
            {
                returnHomeOverride();
                return;
            }

            GameSceneLoader.NavigateToMainHome();
        }

        /// <summary>
        /// 注册/清除「返回主页」的在模式内替代实现；handler 为 null 时恢复默认的 NavigateToMainHome。
        /// </summary>
        public void SetReturnHomeOverride(Action handler)
        {
            if (returnHomeOverride == handler)
                return;

            returnHomeOverride = handler;
            ApplyReturnHomeButtonVisibility();
        }

        void ApplyReturnHomeButtonVisibility()
        {
            var cfg = SceneNavigationSettings.ResolveActive();
            var useHome = returnHomeOverride != null || cfg == null || cfg.useMainHomeScene;

            if (returnHomeButton != null)
                returnHomeButton.gameObject.SetActive(useHome);

            RectTransform layoutRoot = footerButtonsLayoutRoot;
            if (layoutRoot == null && continueGameButton != null)
                layoutRoot = continueGameButton.transform.parent as RectTransform;

            if (layoutRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
        }
    }
}
