using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 水面涟漪：随时间扩散并淡出；供对象池 / 预制体复用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingWaterRippleFx : MonoBehaviour
    {
        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        [SerializeField] private float lifetimeSeconds = 2.5f;
        [SerializeField] private float startScale = 0.15f;
        [SerializeField] private float endScale = 2.8f;
        [Tooltip("生命周期进度 ≥ 此值后开始淡出（0~1）。")]
        [SerializeField] private float fadeStartNormalized = 0.25f;
        [SerializeField] private Renderer[] renderers;

        MaterialPropertyBlock _mpb;
        float _elapsed;
        Vector3 _baseScale = Vector3.one;

        public float LifetimeSeconds => Mathf.Max(0.1f, lifetimeSeconds);

        void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
            _baseScale = transform.localScale;
        }

        void OnEnable()
        {
            _elapsed = 0f;
            ApplyProgress(0f);
            ApplyScale(0f);
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(_elapsed / LifetimeSeconds);
            ApplyProgress(progress);
            ApplyScale(progress);

            if (_elapsed >= LifetimeSeconds)
                gameObject.SetActive(false);
        }

        void ApplyProgress(float progress)
        {
            if (renderers == null)
                return;

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();
            _mpb.SetFloat(ProgressId, progress);
            _mpb.SetFloat(FadeId, ComputeFade(progress));

            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;
                r.SetPropertyBlock(_mpb);
            }
        }

        float ComputeFade(float progress01)
        {
            progress01 = Mathf.Clamp01(progress01);
            var fadeStart = Mathf.Clamp01(fadeStartNormalized);
            if (progress01 <= fadeStart)
                return 1f;

            var t = (progress01 - fadeStart) / Mathf.Max(0.0001f, 1f - fadeStart);
            t = t * t;
            return 1f - t;
        }

        void ApplyScale(float progress01)
        {
            var scale = Mathf.Lerp(startScale, endScale, progress01);
            transform.localScale = _baseScale * scale;
        }
    }
}
