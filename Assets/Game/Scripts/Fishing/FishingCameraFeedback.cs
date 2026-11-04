using System;
using System.Collections;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Fishing
{
    [Serializable]
    public struct FishingCameraShakeSettings
    {
        public bool enabled;
        [Tooltip("触发前等待（秒）。")]
        public float delaySeconds;
        [Tooltip("位置抖动峰值（米）。")]
        public float positionMetersPeak;
        [Tooltip("旋转抖动峰值（度）。")]
        public float eulerDegreesPeak;
        [Tooltip("抖动持续时间（秒）。")]
        public float durationSeconds;
        [Tooltip("抖动频率（Hz）。")]
        public float frequencyHz;

        public static FishingCameraShakeSettings DefaultCast() => new FishingCameraShakeSettings
        {
            enabled = true,
            delaySeconds = 0.25f,
            positionMetersPeak = 0.035f,
            eulerDegreesPeak = 0.75f,
            durationSeconds = 0.28f,
            frequencyHz = 36f,
        };

        public static FishingCameraShakeSettings DefaultCatchToast() => new FishingCameraShakeSettings
        {
            enabled = true,
            delaySeconds = 0f,
            positionMetersPeak = 0.055f,
            eulerDegreesPeak = 1.15f,
            durationSeconds = 0.38f,
            frequencyHz = 34f,
        };
    }

    /// <summary>
    /// 钓鱼相关第三人称镜头：抛竿开始拉近、钓获展示开始时恢复默认机位与抖动反馈。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingCameraFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerFishingController fishing;
        [SerializeField] private ThirdPersonCamera thirdPersonCamera;

        [Header("抛竿开始（SessionStarted）")]
        [SerializeField] private FishingCameraShakeSettings castShake = FishingCameraShakeSettings.DefaultCast();

        [Header("钓获展示开始（与 Toast / 庆祝同步）")]
        [SerializeField] private FishingCameraShakeSettings catchToastShake = FishingCameraShakeSettings.DefaultCatchToast();

        [Header("钓鱼拉近（Free Move Z）")]
        [Tooltip("开始钓鱼时 freeOffset.z 目标值（负值=角色身后；正常约 -5，拉近约 -3）。")]
        [SerializeField] private float fishingFreeOffsetZ = -3f;
        [SerializeField] private float fishingZoomBlendSeconds = 0.35f;

        Coroutine _castShakeRoutine;
        Vector3 _savedFreeOffset;
        bool _fishingZoomActive;

        void Awake()
        {
            if (fishing == null)
                fishing = GetComponent<PlayerFishingController>();
        }

        void OnEnable()
        {
            if (fishing != null)
            {
                fishing.SessionStarted += OnSessionStarted;
                fishing.SessionCancelled += OnSessionCancelled;
                fishing.CatchCompleted += OnCatchCompleted;
            }
        }

        void OnDisable()
        {
            if (fishing != null)
            {
                fishing.SessionStarted -= OnSessionStarted;
                fishing.SessionCancelled -= OnSessionCancelled;
                fishing.CatchCompleted -= OnCatchCompleted;
            }

            CancelCastShakeRoutine();
            RestoreFishingZoomImmediateIfNeeded();
        }

        void OnSessionStarted(FishingSessionState _)
        {
            ApplyFishingZoom();
            PlayCastShake();
        }

        void OnSessionCancelled() => RestoreFishingZoom();

        void OnCatchCompleted(FishingService.CatchResult result)
        {
            if (!result.success)
                return;

            RestoreFishingZoom();
        }

        public void ApplyFishingZoom()
        {
            var cam = ResolveCamera();
            if (cam == null)
                return;

            if (!_fishingZoomActive)
            {
                _savedFreeOffset = cam.CurrentFreeOffset;
                _fishingZoomActive = true;
            }

            var target = new Vector3(_savedFreeOffset.x, _savedFreeOffset.y, fishingFreeOffsetZ);
            cam.BlendFreeOffsetTo(target, fishingZoomBlendSeconds);
        }

        /// <summary>钓获展示开始时恢复默认机位。</summary>
        public void RestoreFishingZoom()
        {
            if (!_fishingZoomActive)
                return;

            var cam = ResolveCamera();
            if (cam != null)
                cam.BlendFreeOffsetTo(_savedFreeOffset, fishingZoomBlendSeconds);

            _fishingZoomActive = false;
        }

        void RestoreFishingZoomImmediateIfNeeded()
        {
            if (!_fishingZoomActive)
                return;

            var cam = ResolveCamera();
            if (cam != null)
                cam.BlendFreeOffsetTo(_savedFreeOffset, 0.01f);

            _fishingZoomActive = false;
        }

        public void PlayCastShake()
        {
            CancelCastShakeRoutine();
            if (!castShake.enabled)
                return;

            var delay = Mathf.Max(0f, castShake.delaySeconds);
            if (delay <= 0.0001f)
            {
                ApplyShake(castShake);
                return;
            }

            _castShakeRoutine = StartCoroutine(CoPlayShakeDelayed(castShake, () => _castShakeRoutine = null));
        }

        /// <summary>钓获展示开始时恢复默认机位并播放抖动。</summary>
        public void PlayCatchToastShake()
        {
            RestoreFishingZoom();
            PlayShakeImmediate(catchToastShake);
        }

        void PlayShakeImmediate(FishingCameraShakeSettings settings)
        {
            if (!settings.enabled)
                return;

            var delay = Mathf.Max(0f, settings.delaySeconds);
            if (delay <= 0.0001f)
            {
                ApplyShake(settings);
                return;
            }

            StartCoroutine(CoPlayShakeDelayed(settings, null));
        }

        IEnumerator CoPlayShakeDelayed(FishingCameraShakeSettings settings, Action onComplete)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, settings.delaySeconds));
            ApplyShake(settings);
            onComplete?.Invoke();
        }

        void CancelCastShakeRoutine()
        {
            if (_castShakeRoutine == null)
                return;

            StopCoroutine(_castShakeRoutine);
            _castShakeRoutine = null;
        }

        void ApplyShake(FishingCameraShakeSettings settings)
        {
            var cam = ResolveCamera();
            if (cam == null)
                return;

            cam.TriggerLandingImpactShake(
                settings.positionMetersPeak,
                settings.eulerDegreesPeak,
                settings.durationSeconds,
                settings.frequencyHz);
        }

        ThirdPersonCamera ResolveCamera()
        {
            if (thirdPersonCamera != null)
                return thirdPersonCamera;

            if (Camera.main != null && Camera.main.TryGetComponent(out ThirdPersonCamera mainCam))
                return mainCam;

            return UnityObjectFinder.FindFirst<ThirdPersonCamera>();
        }
    }
}
