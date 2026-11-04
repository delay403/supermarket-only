using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// 将锥形/定向粒子特效的起点与终点锁定到两个锚点。
    /// 通过每帧更新 Transform 朝向与 <see cref="ParticleSystem.ShapeModule.length"/> 拉伸发射体积，
    /// 保留粒子 Noise、Trails 等原有模块效果（抖动仍在局部坐标系中垂直于光束方向）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(150)]
    public sealed class ParticleBeamConnector : MonoBehaviour
    {
        [Header("Anchors")]
        [SerializeField] private Transform startAnchor;
        [SerializeField] private Transform endAnchor;
        [SerializeField] private Vector3 startOffset;
        [SerializeField] private Vector3 endOffset;

        [Header("Particles")]
        [SerializeField] private ParticleSystem[] particleSystems;

        [Header("Beam")]
        [Min(0.01f)]
        [SerializeField] private float minBeamLength = 0.15f;
        [SerializeField] private float lengthPadding = 0.02f;
        [Tooltip("按距离同比缩放 startSpeed，使电弧覆盖整段长度。")]
        [SerializeField] private bool scaleSpeedWithLength = true;

        Vector3? _startWorldOverride;
        Vector3? _endWorldOverride;
        bool _beamActive = true;
        float _referenceLength = 5f;
        float _referenceSpeed = 10f;
        bool _referenceCaptured;

        public Transform StartAnchor
        {
            get => startAnchor;
            set => startAnchor = value;
        }

        public Transform EndAnchor
        {
            get => endAnchor;
            set => endAnchor = value;
        }

        public bool BeamActive
        {
            get => _beamActive;
            set
            {
                if (_beamActive == value)
                    return;

                _beamActive = value;
                if (_beamActive)
                    PlayParticles();
                else
                    StopParticles();
            }
        }

        void Awake()
        {
            if (particleSystems == null || particleSystems.Length == 0)
                particleSystems = GetComponentsInChildren<ParticleSystem>(true);

            CaptureReferenceValues();
        }

        void OnEnable()
        {
            if (_beamActive)
                PlayParticles();
        }

        void OnDisable() => StopParticles();

        void LateUpdate()
        {
            if (!_beamActive)
                return;

            if (!TryResolveEndpoints(out var startWorld, out var endWorld))
                return;

            ApplyBeam(startWorld, endWorld);
        }

        public void SetAnchors(Transform start, Transform end)
        {
            startAnchor = start;
            endAnchor = end;
            ClearWorldOverrides();
        }

        public void SetWorldEndpoints(Vector3 startWorld, Vector3 endWorld)
        {
            _startWorldOverride = startWorld;
            _endWorldOverride = endWorld;
        }

        public void ClearWorldOverrides()
        {
            _startWorldOverride = null;
            _endWorldOverride = null;
        }

        public void PlayParticles()
        {
            if (particleSystems == null)
                return;

            for (var i = 0; i < particleSystems.Length; i++)
            {
                var ps = particleSystems[i];
                if (ps == null)
                    continue;

                if (!ps.isPlaying)
                    ps.Play(true);
            }
        }

        public void StopParticles()
        {
            if (particleSystems == null)
                return;

            for (var i = 0; i < particleSystems.Length; i++)
            {
                var ps = particleSystems[i];
                if (ps == null)
                    continue;

                if (ps.isPlaying)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        bool TryResolveEndpoints(out Vector3 startWorld, out Vector3 endWorld)
        {
            if (_startWorldOverride.HasValue && _endWorldOverride.HasValue)
            {
                startWorld = _startWorldOverride.Value;
                endWorld = _endWorldOverride.Value;
                return true;
            }

            if (startAnchor == null || endAnchor == null)
            {
                startWorld = default;
                endWorld = default;
                return false;
            }

            startWorld = startAnchor.TransformPoint(startOffset);
            endWorld = endAnchor.TransformPoint(endOffset);
            return true;
        }

        void ApplyBeam(Vector3 startWorld, Vector3 endWorld)
        {
            var delta = endWorld - startWorld;
            var distance = delta.magnitude;
            if (distance < 0.0001f)
                return;

            var direction = delta / distance;
            var beamLength = Mathf.Max(minBeamLength, distance + lengthPadding);

            transform.SetPositionAndRotation(startWorld, Quaternion.LookRotation(direction, Vector3.up));

            if (particleSystems == null)
                return;

            if (!_referenceCaptured)
                CaptureReferenceValues();

            for (var i = 0; i < particleSystems.Length; i++)
            {
                var ps = particleSystems[i];
                if (ps == null)
                    continue;

                var shape = ps.shape;
                if (shape.enabled)
                    shape.length = beamLength;

                if (!scaleSpeedWithLength || _referenceLength <= 0.0001f)
                    continue;

                var main = ps.main;
                var speed = _referenceSpeed * (beamLength / _referenceLength);
                main.startSpeed = speed;
            }
        }

        void CaptureReferenceValues()
        {
            if (particleSystems == null || particleSystems.Length == 0)
                return;

            var ps = particleSystems[0];
            if (ps == null)
                return;

            var shape = ps.shape;
            if (shape.enabled && shape.length > 0.0001f)
                _referenceLength = shape.length;

            var main = ps.main;
            _referenceSpeed = main.startSpeed.constantMax > 0f
                ? main.startSpeed.constantMax
                : main.startSpeed.constant;

            _referenceCaptured = true;
        }
    }
}
