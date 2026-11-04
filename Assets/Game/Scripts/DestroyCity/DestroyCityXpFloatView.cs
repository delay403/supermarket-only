using UnityEngine;
using UnityEngine.UI;

namespace Game.DestroyCity
{
    /// <summary>世界空间经验飘字（上浮淡出）。挂在自制预制体根节点上。</summary>
    [DisallowMultipleComponent]
    public sealed class DestroyCityXpFloatView : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private float lifetimeSeconds = 1.2f;
        [SerializeField] private float riseSpeed = 1.4f;
        [SerializeField] private Vector3 worldRiseDirection = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private bool faceCamera = true;

        float _elapsed;
        Color _baseColor = Color.white;
        Vector3 _riseDirection = Vector3.up;

        void Awake()
        {
            if (label == null)
                label = GetComponentInChildren<Text>(true);
            if (label != null)
                _baseColor = label.color;

            _riseDirection = worldRiseDirection.sqrMagnitude > 0.0001f
                ? worldRiseDirection.normalized
                : Vector3.up;
        }

        /// <summary>由 <see cref="DestroyCityXpFloater"/> 在实例化后调用。</summary>
        public void Play(float xpAmount)
        {
            if (xpAmount <= 0.01f)
            {
                Destroy(gameObject);
                return;
            }

            if (label != null)
            {
                label.text = FormatXp(xpAmount);
                _baseColor = label.color;
            }

            _elapsed = 0f;
            enabled = true;
        }

        public static string FormatXp(float value)
        {
            if (value >= 1_000_000f)
                return $"+{(value / 1_000_000f):0.#}M XP";
            if (value >= 10_000f)
                return $"+{(value / 1_000f):0.#}K XP";
            return $"+{Mathf.RoundToInt(value)} XP";
        }

        void LateUpdate()
        {
            if (!faceCamera)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            var toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            transform.position += _riseDirection * (riseSpeed * Time.deltaTime);

            if (label != null)
            {
                var c = _baseColor;
                c.a = Mathf.Lerp(1f, 0f, _elapsed / Mathf.Max(0.05f, lifetimeSeconds));
                label.color = c;
            }

            if (_elapsed >= lifetimeSeconds)
                Destroy(gameObject);
        }
    }
}
