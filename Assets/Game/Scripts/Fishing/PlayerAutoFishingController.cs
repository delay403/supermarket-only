using System.Collections;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 自动钓鱼：在通行证有效且模式开启时自动抛竿、抽杆，钓获后继续循环。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAutoFishingController : MonoBehaviour
    {
        const float ReelIntervalSeconds = 1f;
        const float PollIntervalSeconds = 0.15f;
        const float MoveInputThresholdSq = 0.04f;

        [SerializeField] private PlayerFishingController fishing;
        [SerializeField] private FishingHudInputBridge hudBridge;
        [SerializeField] private LegacyPlayerInput playerInput;

        Coroutine _loopRoutine;

        void Awake()
        {
            if (fishing == null) fishing = GetComponent<PlayerFishingController>();
            if (hudBridge == null) hudBridge = GetComponent<FishingHudInputBridge>();
            if (playerInput == null) playerInput = GetComponent<LegacyPlayerInput>();
        }

        void OnEnable()
        {
            AutoFishingService.ModeChanged += OnAutoFishingModeChanged;
            AutoFishingService.PassChanged += OnAutoFishingPassChanged;
            SyncLoopWithMode();
        }

        void OnDisable()
        {
            AutoFishingService.ModeChanged -= OnAutoFishingModeChanged;
            AutoFishingService.PassChanged -= OnAutoFishingPassChanged;
            StopLoop();
        }

        void Update()
        {
            AutoFishingService.TickPassExpiry();

            if (!AutoFishingService.IsModeEnabled)
                return;

            if (DetectMoveInput())
                AutoFishingService.PauseMode();
        }

        void OnAutoFishingModeChanged() => SyncLoopWithMode();

        void OnAutoFishingPassChanged()
        {
            if (!AutoFishingService.IsPassActive)
                AutoFishingService.PauseMode();
            SyncLoopWithMode();
        }

        void SyncLoopWithMode()
        {
            if (AutoFishingService.IsModeEnabled && AutoFishingService.IsPassActive)
            {
                if (_loopRoutine == null)
                    _loopRoutine = StartCoroutine(CoAutoFishingLoop());
                return;
            }

            StopLoop();
        }

        void StopLoop()
        {
            if (_loopRoutine == null)
                return;

            StopCoroutine(_loopRoutine);
            _loopRoutine = null;
        }

        IEnumerator CoAutoFishingLoop()
        {
            var pollWait = new WaitForSeconds(PollIntervalSeconds);
            var reelWait = new WaitForSeconds(ReelIntervalSeconds);

            while (AutoFishingService.IsModeEnabled && AutoFishingService.IsPassActive)
            {
                if (fishing == null)
                    fishing = GetComponent<PlayerFishingController>();
                if (hudBridge == null)
                    hudBridge = GetComponent<FishingHudInputBridge>();

                if (DetectMoveInput())
                {
                    AutoFishingService.PauseMode();
                    break;
                }

                if (fishing == null || !fishing.IsInsideFishingZone)
                {
                    yield return pollWait;
                    continue;
                }

                if (!fishing.IsSessionActive)
                {
                    if (hudBridge != null && hudBridge.CanStartFishing())
                        hudBridge.OnFishingButtonPressedWithResult();
                    yield return pollWait;
                    continue;
                }

                if (fishing.IsGameplayActive
                    && fishing.IsReelInputAllowed
                    && fishing.CanShowReelButton())
                {
                    fishing.TryReel();
                    yield return reelWait;
                    continue;
                }

                yield return pollWait;
            }

            _loopRoutine = null;
        }

        bool DetectMoveInput()
        {
            if (playerInput == null)
                playerInput = GetComponent<LegacyPlayerInput>();
            if (playerInput == null)
                return false;

            return playerInput.Move.sqrMagnitude > MoveInputThresholdSq;
        }
    }
}
