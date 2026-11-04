using System;
using System.Collections;
using System.Collections.Generic;
using Game.Collectibles.Equipment;
using Game.Player;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 挂在玩家上：驱动鱼线收放；钓获后收鱼、竿上展示，再复位。可被移动/跳跃/再次钓鱼打断。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFishingLineDriver : MonoBehaviour
    {
        [SerializeField] private PlayerFishingController fishing;
        [SerializeField] private EquipmentSpawner equipment;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerJumpController jumpController;

        [Header("钓获展示")]
        [SerializeField] private PlayerFishingCatchShowcaseDriver catchShowcase;
        [SerializeField] private float moveInterruptSpeedThreshold = 0.12f;

        FishingLineVisual _lineVisual;
        bool _searchedLineVisual;

        bool _postCatchActive;
        bool _waitingForCatchFollowers;
        readonly List<FishingCatchLineFollower> _catchFollowers = new List<FishingCatchLineFollower>(2);
        readonly List<FishingCatchLineFollower> _pendingCatchFollowers = new List<FishingCatchLineFollower>(2);
        readonly List<GameObject> _catchFishObjects = new List<GameObject>(2);
        Coroutine _postCatchCoroutine;

        /// <summary>钓获展示（收鱼 + 竿上等待）进行中。</summary>
        public bool IsPostCatchPresentationActive => _postCatchActive;

        /// <summary>收鱼水平/上升阶段进行中，此期间不可再次钓鱼。</summary>
        public bool IsCatchRetrieveBlockingRefish
        {
            get
            {
                if (!_postCatchActive)
                    return false;

                for (var i = 0; i < _catchFollowers.Count; i++)
                {
                    var follower = _catchFollowers[i];
                    if (follower != null && follower.IsRetrieveInProgress)
                        return true;
                }

                return false;
            }
        }

        /// <summary>钓获展示正常结束（true）或被打断（false）。</summary>
        public event Action<bool> PostCatchPresentationEnded;

        /// <summary>鱼已钓起，等待 HandFishRoot 展示。</summary>
        public event Action<FishingCatchLineFollower> CatchFollowerReadyForHandShowcase;

        public float CatchShowcaseDurationSeconds =>
            catchShowcase != null ? catchShowcase.CatchShowcaseDurationSeconds : 2.5f;

        public bool IsCatchShowcaseActive =>
            catchShowcase != null && catchShowcase.IsCatchShowcaseActive;

        void Awake()
        {
            if (fishing == null) fishing = GetComponent<PlayerFishingController>();
            if (equipment == null) equipment = GetComponent<EquipmentSpawner>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (jumpController == null) jumpController = GetComponent<PlayerJumpController>();
            if (catchShowcase == null) catchShowcase = GetComponent<PlayerFishingCatchShowcaseDriver>();
            CacheLineVisual();
        }

        void OnEnable()
        {
            if (fishing != null)
            {
                fishing.SessionStarted += OnSessionStarted;
                fishing.SessionCancelled += OnSessionCancelled;
                fishing.CatchCompleted += OnCatchCompleted;
            }

            if (jumpController != null)
                jumpController.Jumped += OnPlayerJumped;

            CacheLineVisual();
            _lineVisual?.SetRetractedImmediate();

            if (fishing != null && !fishing.IsSessionActive && !IsPostCatchPresentationActive)
                equipment?.MountRodOnBack();
        }

        void OnDisable()
        {
            if (fishing != null)
            {
                fishing.SessionStarted -= OnSessionStarted;
                fishing.SessionCancelled -= OnSessionCancelled;
                fishing.CatchCompleted -= OnCatchCompleted;
            }

            if (jumpController != null)
                jumpController.Jumped -= OnPlayerJumped;

            ForceResetPostCatchPresentation(notifyPresentationEnded: false);
        }

        void Update()
        {
            if (!_postCatchActive || motor == null || !motor.enabled)
                return;

            var planarSpeed = new Vector2(motor.VelocityWorld.x, motor.VelocityWorld.z).magnitude;
            if (planarSpeed > moveInterruptSpeedThreshold)
                ForceResetPostCatchPresentation(interrupted: true);
        }

        void OnPlayerJumped()
        {
            if (_postCatchActive)
                ForceResetPostCatchPresentation(interrupted: true);
        }

        void CacheLineVisual()
        {
            if (_searchedLineVisual && _lineVisual != null)
                return;
            _searchedLineVisual = true;
            _lineVisual = GetComponentInChildren<FishingLineVisual>(true);
        }

        public void RefreshLineReference()
        {
            _searchedLineVisual = false;
            _lineVisual = null;
            CacheLineVisual();
        }

        /// <summary>由 <see cref="PlayerFishingCatchPresenter"/> 在生成跟随鱼时调用。</summary>
        public void RegisterCatchFollower(FishingCatchLineFollower follower)
        {
            if (follower == null)
                return;

            if (!_catchFishObjects.Contains(follower.gameObject))
                _catchFishObjects.Add(follower.gameObject);

            if (_postCatchActive)
                TrackCatchFollower(follower);
            else if (!_pendingCatchFollowers.Contains(follower))
                _pendingCatchFollowers.Add(follower);
        }

        public float CatchHoldOnLineSeconds => CatchShowcaseDurationSeconds;

        public FishingLineVisual LineVisual
        {
            get
            {
                CacheLineVisual();
                return _lineVisual;
            }
        }

        void TrackCatchFollower(FishingCatchLineFollower follower)
        {
            if (follower == null || _catchFollowers.Contains(follower))
                return;

            follower.FollowCompleted += OnCatchFollowerCompleted;
            follower.ReadyForHandShowcase += OnCatchFollowerReadyForHandShowcase;
            _catchFollowers.Add(follower);
            _waitingForCatchFollowers = true;
            TryCompletePostCatchPresentation();
        }

        void OnSessionStarted(FishingSessionState state)
        {
            ForceResetPostCatchPresentation(notifyPresentationEnded: false);
            equipment?.MountRodOnHand();
            CacheLineVisual();
            if (_lineVisual == null)
                return;

            var waterY = transform.position.y;
            if (fishing?.ActiveZone != null)
                waterY = fishing.ActiveZone.GetWaterSurfaceWorldY();

            GetComponent<FishingPresentationFx>()?.RefreshLineReference();
            _lineVisual.PlayCastAnimation(waterY, transform.forward, _lineVisual.CastForwardDistance);
        }

        void OnSessionCancelled()
        {
            ForceResetPostCatchPresentation(notifyPresentationEnded: false);

            if (_lineVisual != null && _lineVisual.EnableLineEffects)
                _lineVisual.PlayRetractAnimation();
            else
                _lineVisual?.SetRetractedImmediate();
        }

        void OnCatchCompleted(FishingService.CatchResult _)
        {
            catchShowcase?.ForceStopShowcase(notifyEnded: false);
            CancelPostCatchPresentation(clearPendingFollowers: false);
            _postCatchActive = true;
            _waitingForCatchFollowers = false;
            _catchFollowers.Clear();

            for (var i = 0; i < _pendingCatchFollowers.Count; i++)
                TrackCatchFollower(_pendingCatchFollowers[i]);
            _pendingCatchFollowers.Clear();

            _postCatchCoroutine = StartCoroutine(CoPostCatchPresentation());
        }

        IEnumerator CoPostCatchPresentation()
        {
            CacheLineVisual();
            if (_lineVisual != null && _lineVisual.EnableLineEffects)
                _lineVisual.BeginCatchRetrieval();

            yield return null;

            if (_catchFollowers.Count == 0)
            {
                _waitingForCatchFollowers = false;
                TryCompletePostCatchPresentation();
            }

            _postCatchCoroutine = null;
        }

        void OnCatchFollowerReadyForHandShowcase(FishingCatchLineFollower follower) =>
            CatchFollowerReadyForHandShowcase?.Invoke(follower);

        void OnCatchFollowerCompleted(FishingCatchLineFollower follower)
        {
            if (follower != null)
            {
                follower.FollowCompleted -= OnCatchFollowerCompleted;
                follower.ReadyForHandShowcase -= OnCatchFollowerReadyForHandShowcase;
            }

            _catchFollowers.Remove(follower);
            _catchFishObjects.Remove(follower.gameObject);

            if (_catchFollowers.Count == 0)
                _waitingForCatchFollowers = false;

            TryCompletePostCatchPresentation();
        }

        void TryCompletePostCatchPresentation()
        {
            if (!_postCatchActive || _waitingForCatchFollowers || _catchFollowers.Count > 0)
                return;

            CompletePostCatchPresentation();
        }

        void CompletePostCatchPresentation()
        {
            _postCatchActive = false;
            _waitingForCatchFollowers = false;
            UnsubscribeCatchFollowers();
            _pendingCatchFollowers.Clear();
            _catchFishObjects.Clear();

            CacheLineVisual();
            if (_lineVisual != null)
            {
                _lineVisual.EndCatchRetrieval();
                _lineVisual.SetRetractedImmediate();
            }

            GetComponent<FishingPresentationFx>()?.StopFootCelebrate();
            PostCatchPresentationEnded?.Invoke(true);
        }

        /// <summary>移动、跳跃或再次钓鱼时强制复位鱼线、鱼模型与收杆动画。</summary>
        public void ForceResetPostCatchPresentation(bool notifyPresentationEnded = true, bool interrupted = true)
        {
            if (_postCatchCoroutine != null)
            {
                StopCoroutine(_postCatchCoroutine);
                _postCatchCoroutine = null;
            }

            var wasActive = _postCatchActive || _catchFishObjects.Count > 0 || _catchFollowers.Count > 0;

            catchShowcase?.ForceStopShowcase(notifyEnded: false);

            _postCatchActive = false;
            _waitingForCatchFollowers = false;
            UnsubscribeCatchFollowers();
            DestroyAllCatchFish();
            _pendingCatchFollowers.Clear();

            CacheLineVisual();
            if (_lineVisual != null)
            {
                _lineVisual.StopAllLineAnimations();
                _lineVisual.EndCatchRetrieval();
                _lineVisual.SetRetractedImmediate();
            }

            if (fishing != null && !fishing.IsSessionActive)
                equipment?.MountRodOnBack();

            GetComponent<FishingPresentationFx>()?.StopFootCelebrate();

            if (notifyPresentationEnded && wasActive)
                PostCatchPresentationEnded?.Invoke(false);
        }

        void CancelPostCatchPresentation(bool clearPendingFollowers = true)
        {
            if (_postCatchCoroutine != null)
            {
                StopCoroutine(_postCatchCoroutine);
                _postCatchCoroutine = null;
            }

            _postCatchActive = false;
            _waitingForCatchFollowers = false;
            UnsubscribeCatchFollowers();
            if (clearPendingFollowers)
            {
                DestroyPendingCatchFish();
                _pendingCatchFollowers.Clear();
            }

            if (_lineVisual != null)
                _lineVisual.EndCatchRetrieval();
        }

        void DestroyAllCatchFish()
        {
            for (var i = 0; i < _catchFollowers.Count; i++)
            {
                var follower = _catchFollowers[i];
                if (follower == null)
                    continue;
                follower.FollowCompleted -= OnCatchFollowerCompleted;
                follower.ReadyForHandShowcase -= OnCatchFollowerReadyForHandShowcase;
                follower.ForceDestroyWithoutNotify();
            }

            _catchFollowers.Clear();

            for (var i = _catchFishObjects.Count - 1; i >= 0; i--)
            {
                var go = _catchFishObjects[i];
                if (go != null)
                    Destroy(go);
            }

            _catchFishObjects.Clear();
        }

        void DestroyPendingCatchFish()
        {
            for (var i = 0; i < _pendingCatchFollowers.Count; i++)
            {
                var follower = _pendingCatchFollowers[i];
                if (follower != null)
                    Destroy(follower.gameObject);
            }
        }

        void UnsubscribeCatchFollowers()
        {
            for (var i = 0; i < _catchFollowers.Count; i++)
            {
                if (_catchFollowers[i] != null)
                {
                    _catchFollowers[i].FollowCompleted -= OnCatchFollowerCompleted;
                    _catchFollowers[i].ReadyForHandShowcase -= OnCatchFollowerReadyForHandShowcase;
                }
            }

            _catchFollowers.Clear();
        }
    }
}
