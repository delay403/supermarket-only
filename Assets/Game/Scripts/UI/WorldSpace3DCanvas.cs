using Game.Collectibles;
using Game.Core.Config;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 世界空间 Canvas：自动朝向 Main Camera，仅绕 Y 轴旋转；子级 <see cref="Text"/> 支持固有色、品质库色与神话彩虹流动色（每实例独立配置）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class WorldSpace3DCanvas : MonoBehaviour
    {
        public enum TextColorMode
        {
            [Tooltip("使用 solidColor。")]
            Solid = 0,

            [Tooltip("从 QualityVisualLibrary 读取对应品质固有色（神话档为条目色，不流动）。")]
            QualityLibrary = 1,

            [Tooltip("神话彩虹：色相随时间变化；可用本组件或库内参数。")]
            MythicRainbow = 2,
        }

        [Header("Canvas")]
        [Tooltip("留空则使用同物体上的 Canvas；进入 Play 时若未设为 World Space 会强制改为 World Space。")]
        [SerializeField] private Canvas canvas;

        [Tooltip("勾选后在 Awake 将 Canvas 设为 World Space。")]
        [SerializeField] private bool enforceWorldSpace = true;

        [Header("朝向相机（仅 Y 轴）")]
        [SerializeField] private bool enableBillboard = true;
        [Tooltip("留空则运行时自动使用 Main Camera（Camera.main）。")]
        [FormerlySerializedAs("playerTarget")]
        [SerializeField] private Transform cameraTarget;

        [Tooltip("看向相机时的世界空间偏移。")]
        [SerializeField] private Vector3 lookAtWorldOffset;

        [Tooltip("水平距离小于该值时不旋转，避免抖动。")]
        [SerializeField] private float minRotateDistance = 0.05f;

        [Header("文本")]
        [Tooltip("留空则在子物体中查找第一个 Text。")]
        [SerializeField] private Text label;

        [SerializeField] private string displayText = "";

        [SerializeField] private TextColorMode textColorMode = TextColorMode.Solid;

        [SerializeField] private Color solidColor = Color.white;

        [SerializeField] private CosmeticQuality qualityTier = CosmeticQuality.Common;

        [Tooltip("空则使用 ConfigRegistry.QualityVisuals / Resources。")]
        [SerializeField] private QualityVisualLibrary qualityVisualLibrary;

        [Header("神话彩虹（TextColorMode = MythicRainbow）")]
        [Tooltip("勾选后使用下方本物体参数；否则使用 QualityVisualLibrary 内神话参数。")]
        [SerializeField] private bool useLocalMythicRainbowParameters;

        [SerializeField] private float mythicHueCyclesPerSecond = 0.25f;

        [Range(0f, 1f)]
        [SerializeField] private float mythicRainbowSaturation = 0.82f;

        [Range(0f, 1f)]
        [SerializeField] private float mythicRainbowValue = 1f;

        Transform _resolvedCamera;
        bool _needsMythicColorTick;

        void Reset()
        {
            canvas = GetComponent<Canvas>();
            label = GetComponentInChildren<Text>(true);
        }

        void Awake()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();

            if (enforceWorldSpace && canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                canvas.renderMode = RenderMode.WorldSpace;

            EnsureCanvasInputReady();

            if (label == null)
                label = GetComponentInChildren<Text>(true);

            if (cameraTarget != null)
                _resolvedCamera = cameraTarget;
            else if (Camera.main != null)
                _resolvedCamera = Camera.main.transform;

            ApplyTextContent();
            RefreshTextColor(force: true);
        }

        void OnValidate()
        {
            if (label == null)
                label = GetComponentInChildren<Text>(true);

            if (!Application.isPlaying)
            {
                ApplyTextContent();
                RefreshTextColor(force: true);
            }
        }

        void LateUpdate()
        {
            if (_needsMythicColorTick)
                RefreshTextColor(force: false);
            if (!enableBillboard)
                return;

            TickBillboard();
        }

        void EnsureCanvasInputReady()
        {
            if (canvas == null)
                return;

            if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
            {
                var cam = Camera.main;
                if (cam != null)
                    canvas.worldCamera = cam;
            }

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        /// <summary>供其它世界空间 UI 脚本复用：绑定相机与 GraphicRaycaster。</summary>
        public static void EnsureCanvasInputReady(Canvas target)
        {
            if (target == null)
                return;

            if (target.renderMode == RenderMode.WorldSpace && target.worldCamera == null)
            {
                var cam = Camera.main;
                if (cam != null)
                    target.worldCamera = cam;
            }

            if (target.GetComponent<GraphicRaycaster>() == null)
                target.gameObject.AddComponent<GraphicRaycaster>();
        }

        /// <summary>运行时修改显示文案。</summary>
        public void SetDisplayText(string text)
        {
            displayText = text ?? string.Empty;
            ApplyTextContent();
        }

        /// <summary>运行时修改着色模式（会立即刷新颜色）。</summary>
        public void SetTextColorMode(TextColorMode mode)
        {
            textColorMode = mode;
            RefreshTextColor(force: true);
        }

        /// <summary>运行时修改固有色（Solid 模式）。</summary>
        public void SetSolidColor(Color color)
        {
            solidColor = color;
            if (textColorMode == TextColorMode.Solid)
                RefreshTextColor(force: true);
        }

        /// <summary>手动指定相机 Transform（覆盖 Main Camera 自动查找）。</summary>
        public void SetCameraTarget(Transform target)
        {
            cameraTarget = target;
            _resolvedCamera = target;
        }

        void TickBillboard()
        {
            var target = ResolveCameraTransform();
            if (target == null)
                return;

            var aim = target.position + lookAtWorldOffset;
            var flat = aim - transform.position;
            flat.y = 0f;

            if (flat.sqrMagnitude < minRotateDistance * minRotateDistance)
                return;

            // UGUI 世界空间 Canvas 正面常在 -Z，相对 LookAt 需再绕 Y 翻转 180°。
            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
        }

        Transform ResolveCameraTransform()
        {
            if (cameraTarget != null)
            {
                _resolvedCamera = cameraTarget;
                return _resolvedCamera;
            }

            if (_resolvedCamera != null)
                return _resolvedCamera;

            var main = Camera.main;
            if (main != null)
                _resolvedCamera = main.transform;

            return _resolvedCamera;
        }

        void ApplyTextContent()
        {
            if (label == null)
                return;

            label.text = displayText;
        }

        void RefreshTextColor(bool force)
        {
            if (label == null)
                return;

            _needsMythicColorTick = textColorMode == TextColorMode.MythicRainbow;

            if (!_needsMythicColorTick && !force)
                return;

            label.color = ResolveLabelColor();
        }

        Color ResolveLabelColor()
        {
            switch (textColorMode)
            {
                case TextColorMode.Solid:
                    return solidColor;

                case TextColorMode.QualityLibrary:
                    var libQ = ResolvedQualityLibrary();
                    return libQ != null
                        ? libQ.ResolveRichTextColor(qualityTier)
                        : Color.white;

                case TextColorMode.MythicRainbow:
                    return ResolveMythicRainbowColor(Time.unscaledTime);

                default:
                    return solidColor;
            }
        }

        Color ResolveMythicRainbowColor(float unscaledTime)
        {
            if (!useLocalMythicRainbowParameters)
            {
                var lib = ResolvedQualityLibrary();
                if (lib != null)
                    return lib.ResolveMythicRainbowColor(unscaledTime);
            }

            if (mythicHueCyclesPerSecond <= 0f)
            {
                var libFallback = ResolvedQualityLibrary();
                return libFallback != null
                    ? libFallback.ResolveRichTextColor(CosmeticQuality.Mythic)
                    : solidColor;
            }

            var h = (unscaledTime * mythicHueCyclesPerSecond) % 1f;
            if (h < 0f)
                h += 1f;
            return Color.HSVToRGB(h, mythicRainbowSaturation, mythicRainbowValue);
        }

        QualityVisualLibrary ResolvedQualityLibrary()
        {
            if (qualityVisualLibrary != null)
                return qualityVisualLibrary;

            var fromRegistry = ConfigRegistry.QualityVisuals;
            if (fromRegistry != null)
                return fromRegistry;

            return Resources.Load<QualityVisualLibrary>("QualityVisualLibrary");
        }
    }
}
