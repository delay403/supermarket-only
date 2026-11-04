using System;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 钓获鱼模型：水平靠近 → 姿态过渡 → 竖直钓起 → HandFishRoot 展示 → 消失。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(150)]
    public sealed class FishingCatchLineFollower : MonoBehaviour
    {
        enum RetrievePhase
        {
            Horizontal = 0,
            TransitionToVertical = 1,
            Vertical = 2,
            HandShowcase = 3,
        }

        static readonly Quaternion HorizontalFishRotation = Quaternion.Euler(0f, 180f, 0f);
        static readonly Quaternion CaughtFishRotation = Quaternion.Euler(-90f, 180f, 0f);

        [SerializeField] private FishingLineVisual lineVisual;
        [SerializeField] private Vector3 hookOffset;
        [SerializeField] private float horizontalMoveSpeed = 11f;
        [SerializeField] private float verticalMoveSpeedUnderwater = 5f;
        [SerializeField] private float verticalMoveSpeedAboveWater = 11f;
        [SerializeField] private float arrivalThreshold = 0.08f;
        private float horizontalSplashInterval = 0.08f;
        [SerializeField] private float holdLineLengthFromRod = 1.1f;
        [SerializeField] private float tiltToVerticalDuration = 0.5f;

        RetrievePhase _phase;
        float _horizontalTargetY;
        float _holdSwayPhaseOffset;
        float _tiltElapsed;
        float _nextHorizontalSplashTime;
        Vector3 _transitionStartPos;
        FishingPresentationFx _presentationFx;
        bool _completed;
        bool _handShowcaseSignaled;

        /// <summary>竖直钓起完成，即将进入 HandFishRoot 展示。</summary>
        public event Action<FishingCatchLineFollower> ReadyForHandShowcase;

        /// <summary>展示结束、即将销毁时触发。</summary>
        public event Action<FishingCatchLineFollower> FollowCompleted;

        public bool IsActive => !_completed;

        /// <summary>水平靠近、姿态过渡、竖直上升阶段（不含手持展示）。</summary>
        public bool IsRetrieveInProgress => IsActive && _phase != RetrievePhase.HandShowcase;

        public void Bind(
            FishingLineVisual visual,
            FishingPresentationFx presentationFx,
            Vector3 offset = default)
        {
            lineVisual = visual;
            _presentationFx = presentationFx;
            if (_presentationFx == null && visual != null)
            {
                var playerRoot = visual.transform.root;
                _presentationFx = playerRoot.GetComponent<FishingPresentationFx>()
                                  ?? playerRoot.GetComponentInParent<FishingPresentationFx>()
                                  ?? playerRoot.GetComponentInChildren<FishingPresentationFx>();
            }

            hookOffset = offset;
            _horizontalTargetY = transform.position.y;
            _phase = RetrievePhase.Horizontal;
            _handShowcaseSignaled = false;
            _holdSwayPhaseOffset = UnityEngine.Random.value * 100f;
            _nextHorizontalSplashTime = 0f;
            _tiltElapsed = 0f;
            transform.rotation = HorizontalFishRotation;
            lineVisual?.SetCatchRetrievalLineStraight(false);

            PlayRetrieveSplash("RetrieveBindInitial");
        }

        /// <summary>由 <see cref="PlayerFishingCatchShowcaseDriver"/> 挂到 HandFishRoot。</summary>
        public void AttachToHandFishRoot(Transform socket, Vector3 localPosition, Vector3 localEuler)
        {
            if (socket == null)
                return;

            transform.SetParent(socket, false);
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(localEuler);
            transform.localScale = Vector3.one;
            lineVisual = null;
            DisableCatchPhysics();
        }

        public void NotifyHandShowcaseComplete() => CompleteFollow();

        /// <summary>展示结束前提前隐藏鱼模型（不影响 FollowCompleted 时机）。</summary>
        public void HideHandFishVisual()
        {
            if (_completed || !gameObject.activeSelf)
                return;

            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_completed)
                return;

            if (_phase == RetrievePhase.HandShowcase)
            {
                UpdateHandShowcasePhase();
                return;
            }

            if (lineVisual == null)
            {
                CompleteFollow();
                return;
            }

            var rodTip = lineVisual.RodTipWorld;

            switch (_phase)
            {
                case RetrievePhase.Horizontal:
                    UpdateHorizontalPhase(rodTip);
                    break;
                case RetrievePhase.TransitionToVertical:
                    UpdateTransitionPhase(rodTip);
                    break;
                default:
                    UpdateVerticalPhase(rodTip);
                    break;
            }

            SyncLineEnd();
        }

        void UpdateHorizontalPhase(Vector3 rodTip)
        {
            lineVisual.SetCatchRetrievalLineStraight(false);

            var horizontalTarget = new Vector3(rodTip.x, _horizontalTargetY, rodTip.z);
            transform.position = Vector3.MoveTowards(
                transform.position,
                horizontalTarget,
                horizontalMoveSpeed * Time.deltaTime);

            TryPeriodicRetrieveSplash("RetrieveHorizontal");

            if ((transform.position - horizontalTarget).sqrMagnitude <= arrivalThreshold * arrivalThreshold)
                BeginTransitionToVertical();
        }

        void BeginTransitionToVertical()
        {
            _phase = RetrievePhase.TransitionToVertical;
            _transitionStartPos = transform.position;
            _tiltElapsed = 0f;
            lineVisual.SetCatchRetrievalLineStraight(true);
            PlayRetrieveSplash("RetrieveVerticalStart");
        }

        void UpdateTransitionPhase(Vector3 rodTip)
        {
            lineVisual.SetCatchRetrievalLineStraight(true);

            _tiltElapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_tiltElapsed / Mathf.Max(0.05f, tiltToVerticalDuration));
            var smooth = t * t * (3f - 2f * t);
            transform.rotation = Quaternion.Slerp(HorizontalFishRotation, CaughtFishRotation, smooth);

            var targetFish = ComputeHookWorldNearRod(rodTip) - hookOffset;
            var liftT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.2f) / 0.8f));
            transform.position = Vector3.Lerp(_transitionStartPos, targetFish, liftT);

            if (t >= 1f)
                _phase = RetrievePhase.Vertical;
        }

        void UpdateVerticalPhase(Vector3 rodTip)
        {
            lineVisual.SetCatchRetrievalLineStraight(true);
            transform.rotation = CaughtFishRotation;

            var targetFish = ComputeHookWorldNearRod(rodTip) - hookOffset;
            var hookWorld = GetHookWorld();
            var speed = hookWorld.y >= lineVisual.WaterSurfaceY - 0.02f
                ? verticalMoveSpeedAboveWater
                : verticalMoveSpeedUnderwater;

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetFish,
                speed * Time.deltaTime);

            if ((transform.position - targetFish).sqrMagnitude <= arrivalThreshold * arrivalThreshold)
                BeginHandShowcase(rodTip);
        }

        void BeginHandShowcase(Vector3 rodTip)
        {
            _phase = RetrievePhase.HandShowcase;
            _handShowcaseSignaled = false;
            transform.rotation = CaughtFishRotation;
            transform.position = ComputeHookWorldNearRod(rodTip) - hookOffset;
            lineVisual?.SetCatchRetrievalLineStraight(true);
            DisableCatchPhysics();
            SyncLineEnd();
        }

        void UpdateHandShowcasePhase()
        {
            if (_handShowcaseSignaled)
                return;

            _handShowcaseSignaled = true;
            ReadyForHandShowcase?.Invoke(this);
        }

        Vector3 ComputeHookWorldNearRod(Vector3 rodTip) =>
            rodTip + Vector3.down.normalized * holdLineLengthFromRod;

        void TryPeriodicRetrieveSplash(string source)
        {
            if (_presentationFx == null || Time.time < _nextHorizontalSplashTime)
                return;

            _nextHorizontalSplashTime = Time.time + horizontalSplashInterval;
            PlayRetrieveSplash(source);
        }

        void PlayRetrieveSplash(string source)
        {
            if (_presentationFx == null)
            {
                Debug.LogWarning($"[FishingCatchLineFollower] 跳过水花 {source}：未绑定 FishingPresentationFx。", this);
                return;
            }

            if (TryResolveSplashPosition(out var splashPos))
                _presentationFx.PlayWaterSplash(splashPos, source);
            else
                _presentationFx.PlayWaterSplash(transform.position, source + ":FishPosFallback");
        }

        bool TryResolveSplashPosition(out Vector3 splashPos)
        {
            splashPos = default;
            if (lineVisual == null)
                return false;

            if (lineVisual.TryGetLineWaterContact(out splashPos))
                return true;

            var hookWorld = GetHookWorld();
            var rodTip = lineVisual.RodTipWorld;
            if (lineVisual.TryGetSegmentWaterContact(rodTip, hookWorld, out splashPos))
                return true;

            splashPos = new Vector3(hookWorld.x, lineVisual.WaterSurfaceY, hookWorld.z);
            return true;
        }

        Vector3 GetHookWorld() => transform.position + hookOffset;

        void DisableCatchPhysics()
        {
            var rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            for (var i = 0; i < rigidbodies.Length; i++)
            {
                var rb = rigidbodies[i];
                if (rb == null)
                    continue;

                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            var colliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }

        void SyncLineEnd()
        {
            if (lineVisual == null)
                return;

            lineVisual.SetCatchRetrievalLineEnd(GetHookWorld());
        }

        public void ForceDestroyWithoutNotify()
        {
            _completed = true;
            if (gameObject != null)
                Destroy(gameObject);
        }

        void CompleteFollow()
        {
            if (_completed)
                return;

            _completed = true;
            FollowCompleted?.Invoke(this);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_completed)
                return;

            _completed = true;
            FollowCompleted?.Invoke(this);
        }
    }
}
