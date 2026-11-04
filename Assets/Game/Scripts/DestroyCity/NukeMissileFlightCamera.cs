using System.Collections;
using Game.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.DestroyCity
{
    /// <summary>
    /// 导弹飞行镜头：可选跟随导弹；爆炸后可在落点短暂停留。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NukeMissileFlightCamera : MonoBehaviour
    {
        [SerializeField] private ThirdPersonCamera thirdPersonCamera;
        [SerializeField] private Vector3 cameraOffsetLocal = new Vector3(0f, 5f, -14f);
        [SerializeField] private float lookTargetBlend = 0.42f;
        [SerializeField] private float followSharpness = 7f;
        [SerializeField] private float fov = 58f;

        [Header("爆炸观看")]
        [Tooltip("Lv1 时的爆炸观看偏移；更高等级在此基础上累加。")]
        [FormerlySerializedAs("explosionWatchOffset")]
        [SerializeField] private Vector3 baseExplosionWatchOffset = new Vector3(0f, 6f, -6f);
        [Tooltip("核弹每升 1 级，Y 增加该值。")]
        [SerializeField] private float explosionWatchYOffsetPerLevel = 2f;
        [Tooltip("核弹每升 1 级，Z 减少该值（更负、离落点更远）。")]
        [SerializeField] private float explosionWatchZOffsetPerLevel = 4f;
        [SerializeField] private float explosionWatchSharpness = 4f;
        [SerializeField] private float defaultExplosionWatchSeconds = 5f;

        Transform _missile;
        Transform _cityTarget;
        bool _missileFollowActive;
        bool _thirdPersonWasEnabled;
        bool _watchingExplosion;
        Vector3 _explosionWatchPoint;
        float _explosionWatchUntil;
        int _explosionWatchNukeLevel = 1;

        public bool IsMissileFollowActive => _missileFollowActive;
        public bool IsWatchingExplosion => _watchingExplosion;

        void Awake()
        {
            if (thirdPersonCamera == null)
                thirdPersonCamera = FindObjectOfType<ThirdPersonCamera>();
        }

        public Vector3 ResolveExplosionWatchOffset(int nukeLevel = -1)
        {
            var level = nukeLevel > 0 ? nukeLevel : Mathf.Max(1, NukeUpgradeService.GetNukeLevel());
            var steps = level - 1;
            return new Vector3(
                baseExplosionWatchOffset.x,
                baseExplosionWatchOffset.y + steps * explosionWatchYOffsetPerLevel,
                baseExplosionWatchOffset.z - steps * explosionWatchZOffsetPerLevel);
        }

        public void SetFollowTargets(Transform missile, Transform cityTarget)
        {
            _missile = missile;
            _cityTarget = cityTarget;
        }

        public void SetMissileFollowActive(bool active)
        {
            if (active)
            {
                if (!_missileFollowActive && thirdPersonCamera != null)
                {
                    _thirdPersonWasEnabled = thirdPersonCamera.enabled;
                    thirdPersonCamera.enabled = false;
                }

                _missileFollowActive = _missile != null;
                enabled = _missileFollowActive || _watchingExplosion;
                return;
            }

            _missileFollowActive = false;
            if (!_watchingExplosion)
            {
                enabled = false;
                RestoreThirdPersonCamera();
            }
            else
            {
                enabled = true;
            }
        }

        public void SetDescentSteerEnabled(bool enabled)
        {
            NukeLaunchViewModeController.Instance?.OnDescentSteeringChanged(enabled);
        }

        public void BeginExplosionWatch(Vector3 worldPosition, float maxSeconds = -1f, int nukeLevel = -1)
        {
            _missile = null;
            _missileFollowActive = false;
            _watchingExplosion = true;
            _explosionWatchPoint = worldPosition;
            _explosionWatchUntil = Time.time + (maxSeconds > 0f ? maxSeconds : defaultExplosionWatchSeconds);
            _explosionWatchNukeLevel = nukeLevel > 0
                ? nukeLevel
                : Mathf.Max(1, NukeUpgradeService.GetNukeLevel());

            if (thirdPersonCamera != null)
            {
                _thirdPersonWasEnabled = thirdPersonCamera.enabled;
                thirdPersonCamera.enabled = false;
            }

            enabled = true;
        }

        public void EndExplosionWatch()
        {
            _watchingExplosion = false;
            if (!_missileFollowActive)
            {
                enabled = false;
                RestoreThirdPersonCamera();
            }
        }

        public IEnumerator CoWaitExplosionWatch()
        {
            while (_watchingExplosion)
                yield return null;
        }

        void RestoreThirdPersonCamera()
        {
            if (thirdPersonCamera != null)
                thirdPersonCamera.enabled = true;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            if (_watchingExplosion)
            {
                UpdateExplosionWatch(cam);
                if (Time.time >= _explosionWatchUntil)
                    EndExplosionWatch();
                return;
            }

            if (!_missileFollowActive || _missile == null)
                return;

            UpdateMissileFollow(cam);
        }

        void UpdateMissileFollow(Camera cam)
        {
            var landing = _cityTarget != null ? _cityTarget.position : _missile.position + _missile.forward * 80f;
            var focus = Vector3.Lerp(_missile.position, landing, Mathf.Clamp01(lookTargetBlend));

            var frame = Quaternion.LookRotation((focus - _missile.position).normalized, Vector3.up);
            var desiredPos = _missile.position + frame * cameraOffsetLocal;
            ApplyCameraBlend(cam, desiredPos, focus);
        }

        void UpdateExplosionWatch(Camera cam)
        {
            var focus = _explosionWatchPoint;
            var desiredPos = focus + ResolveExplosionWatchOffset(_explosionWatchNukeLevel);
            ApplyCameraBlend(cam, desiredPos, focus, explosionWatchSharpness);
        }

        void ApplyCameraBlend(Camera cam, Vector3 desiredPos, Vector3 focus, float sharpness = -1f)
        {
            if (sharpness <= 0f)
                sharpness = followSharpness;

            var dt = Time.deltaTime;
            var blend = 1f - Mathf.Exp(-sharpness * dt);
            cam.transform.position = Vector3.Lerp(cam.transform.position, desiredPos, blend);
            var lookRot = Quaternion.LookRotation(focus - cam.transform.position, Vector3.up);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, lookRot, blend);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, blend);
        }
    }
}
