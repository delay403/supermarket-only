using Game.Core;
using Game.Economy;
using Game.Fishing;
using Game.Save;
using Game.UI;
using Game.UI.Framework;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>
    /// 优惠商品事件入口：玩法里程碑、在线时长、定时轮询、HUD 倒计时。
    /// 仅在当前激活玩法模块支持优惠礼包时运行，避免跨模式无效轮询。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public sealed class DiscountOfferEventManager : MonoBehaviour
    {
        [SerializeField] private DiscountOfferHudController hudController;
        [SerializeField] private float schedulePollIntervalSeconds = 1f;
        [SerializeField] private float hudTimerRefreshIntervalSeconds = 0.25f;

        static DiscountOfferEventManager s_instance;
        static PlayerFishingController s_registeredFishing;

        bool _runtimeActive;
        GameplayModule _trackedModule = GameplayModule.None;
        float _nextSchedulePoll;
        float _nextHudTimerRefresh;
        float _onlineSaveAccumulator;

        void Awake()
        {
            s_instance = this;
        }

        void OnEnable()
        {
            GameplayModuleContext.ModuleChanged += OnModuleChanged;
            RefreshRuntimeState();
        }

        void OnDisable()
        {
            GameplayModuleContext.ModuleChanged -= OnModuleChanged;
            UnregisterFishing(s_registeredFishing);
            SetRuntimeActive(false);
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;

            UnregisterFishing(s_registeredFishing);
        }

        void Update()
        {
            if (!_runtimeActive)
                return;

            DiscountOfferService.AccumulateOnlineTime(Time.unscaledDeltaTime);
            _onlineSaveAccumulator += Time.unscaledDeltaTime;
            if (_onlineSaveAccumulator >= 30f)
            {
                _onlineSaveAccumulator = 0f;
                if (DiscountOfferSaveBridge.TryGetMilestones(out var milestones)
                    && milestones.TotalOnlineSeconds > 0f)
                {
                    SaveService.Save();
                }
            }

            var now = Time.unscaledTime;
            if (now >= _nextSchedulePoll)
            {
                _nextSchedulePoll = now + Mathf.Max(0.25f, schedulePollIntervalSeconds);
                DiscountOfferService.EvaluateAllTriggers();
            }

            if (hudController != null && now >= _nextHudTimerRefresh)
            {
                _nextHudTimerRefresh = now + Mathf.Max(0.1f, hudTimerRefreshIntervalSeconds);
                hudController.TickTimers();
            }
        }

        void OnModuleChanged(GameplayModule previous, GameplayModule current)
        {
            RefreshRuntimeState();
        }

        void RefreshRuntimeState()
        {
            var shouldRun = DiscountOfferSaveBridge.IsActive;
            var module = GameplayModuleContext.ActiveModule;

            if (!shouldRun)
            {
                if (_runtimeActive)
                {
                    SetRuntimeActive(false);
                    DiscountOfferPopupQueue.OnActiveScopeChanged();
                    hudController?.RefreshAll();
                }

                _trackedModule = GameplayModule.None;
                return;
            }

            var moduleChanged = _trackedModule != module;
            if (!_runtimeActive || moduleChanged)
            {
                if (_runtimeActive && moduleChanged)
                    DiscountOfferPopupQueue.OnActiveScopeChanged();

                SetRuntimeActive(true);
                _trackedModule = module;
                DiscountOfferService.RestoreHudFromSave();
                hudController?.RefreshAll();
                DiscountOfferService.EvaluateAllTriggers();
                _nextSchedulePoll = Time.unscaledTime;
                _nextHudTimerRefresh = Time.unscaledTime;
                return;
            }

            hudController?.RefreshAll();
        }

        void SetRuntimeActive(bool active)
        {
            _runtimeActive = active;
            if (!active)
                UnregisterFishing(s_registeredFishing);
        }

        public static void RegisterFishing(PlayerFishingController fishing)
        {
            if (fishing == null || !GameplayModuleContext.IsModuleActive(GameplayModule.Fishing))
                return;

            if (s_registeredFishing == fishing)
                return;

            UnregisterFishing(s_registeredFishing);
            s_registeredFishing = fishing;
            fishing.CatchCompleted += OnCatchCompleted;
        }

        public static void UnregisterFishing(PlayerFishingController fishing)
        {
            if (fishing == null || s_registeredFishing != fishing)
                return;

            fishing.CatchCompleted -= OnCatchCompleted;
            s_registeredFishing = null;
        }

        static void OnCatchCompleted(FishingService.CatchResult result)
        {
            if (!result.success)
                return;

            DiscountOfferService.OnSuccessfulCatch();
        }
    }
}
