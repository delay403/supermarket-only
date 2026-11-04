using System;
using Game.Glide;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hud
{
    /// <summary>
    /// 全屏边缘气流 UI：多层 <see cref="Image"/> 透明度正弦呼吸，并沿各自的「局部轴」往复位移
    ///（默认局部 +X / Scene 中红轴），使气流条在指向屏心的方向上内外摆动；角速度与
    /// 沿局部轴往复位移速度；与 <see cref="GlideController.GetTrajectorySpeed01"/> 联动时可整体加快。
    /// 亦可在 Inspector 调节整体倍率字段 <c>animationSpeedMultiplier</c>。
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class GlideAirflowScreenFx : MonoBehaviour
    {
        private static Sprite s_whiteSprite;

        [SerializeField] private GlideController glide;

        [Tooltip("传给 glide.GetTrajectorySpeed01 的参考速率，与坠落风参考等可对齐。")]
        [SerializeField] private float hudSpeedReferenceMps = 200f;

        [SerializeField] private bool showDuringJumpAscent;

        [Tooltip("Inspector 中未配置图层时在运行时创建左右上下四条半透明带。")]
        [SerializeField] private bool buildDefaultLayersIfEmpty = true;

        [SerializeField] private int canvasSortOrder = 28500;

        [SerializeField] private EdgeLayer[] edgeLayers = Array.Empty<EdgeLayer>();

        [Tooltip(
            "位移沿该 Rect 局部轴（Scene 矩形工具红=+X / 绿=+Y）；与图中箭头一致时请选 Local X。")]
        [SerializeField] private OscillationDisplacementAxis displacementAxis = OscillationDisplacementAxis.LocalX;

        [Tooltip("滑翔 UI 隐藏时把各矩形 localPosition 恢复为采样到的静止值，避免留在偏移pose。")]
        [SerializeField] private bool restoreLocalPositionWhenHidden = true;

        [Tooltip("透明度与位移振荡的整体倍率（同时放大各层的角速度）；1=默认，2≈两倍速。")]
        [SerializeField] [Min(0.01f)] private float animationSpeedMultiplier = 1f;

        private Canvas _canvas;

        private enum OscillationDisplacementAxis
        {
            LocalX = 0,
            LocalY = 1,
        }

        [Serializable]
        public sealed class EdgeLayer
        {
            public RectTransform rect;

            [Tooltip("速度≈0 时的角速度 (rad/s)。")]
            public float baseAngularVelocity = 1f;

            [Tooltip("speed01≈1 时额外叠加的角速度（透明度忽明忽暗更快）。")]
            public float angularVelocityFromTrajectorySpeed01 = 2.5f;

            public float phaseOffsetRadians;

            public float alphaMin = 0.028f;

            public float alphaMax = 0.22f;

            [Header("位移（局部轴往复）")]
            [Tooltip("沿 rect 局部 X 或 Y（由组件上一级 displacementAxis 指定）正负摆动的幅度（与锚点所在单位一致）。")]
            public float positionAmplitude = 14f;

            [Tooltip("为真：位移摆动角速度与透明度呼吸相同（仅相位可错开）。")]
            public bool useAlphaFrequencyForPosition = true;

            [Tooltip("为假：位移使用独立的角速度，与透明度可不同频。")]
            public float positionBaseAngularVelocity = 1f;

            public float positionAngularVelocityFromTrajectorySpeed01 = 2.5f;

            [Tooltip("位移正弦相位偏移（弧度），与各层振幅/透明度错开。")]
            public float positionPhaseOffsetRadians;

            [NonSerialized] public float PhaseAccumulator;

            [NonSerialized] public float PositionPhaseAccumulator;

            /// <summary>首次显示滑翔 UI 时采样的静止 localPosition。</summary>
            [NonSerialized] public Vector3 RestLocalPosition;

            [NonSerialized] public bool RestCaptured;
        }

        private bool _defaultsBuilt;

        private void Awake()
        {
            if (glide == null)
                glide = ResolveGlideController();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            LazyBuildDefaults();
            if (glide == null)
                glide = ResolveGlideController();

            var show = glide != null && glide.isGliding;
            if (show && !showDuringJumpAscent && glide.IsPreGlideJumpAscent)
                show = false;

            if (_canvas != null)
                _canvas.enabled = show && edgeLayers.Length > 0;

            if (!show || edgeLayers.Length == 0)
            {
                if (restoreLocalPositionWhenHidden)
                    RestoreLayerLocalPositionsToRest();
                return;
            }

            var speed01 = glide.GetTrajectorySpeed01(hudSpeedReferenceMps);
            var dt = Time.deltaTime;
            var pace = Mathf.Max(0.01f, animationSpeedMultiplier);

            foreach (var layer in edgeLayers)
                TickEdgeLayer(layer, speed01, dt, pace);
        }

        private void TickEdgeLayer(EdgeLayer layer, float trajectorySpeed01, float dt, float pace)
        {
            if (layer.rect == null)
                return;

            CaptureRestIfNeeded(layer);

            var omegaAlpha = Mathf.Max(
                0f,
                layer.baseAngularVelocity + trajectorySpeed01 * layer.angularVelocityFromTrajectorySpeed01) * pace;
            layer.PhaseAccumulator += dt * omegaAlpha;

            var img = layer.rect.GetComponent<Image>();
            if (img != null)
            {
                var blend = Mathf.Clamp01(
                    0.5f + 0.5f * Mathf.Sin(layer.PhaseAccumulator + layer.phaseOffsetRadians));
                var a = Mathf.Clamp01(Mathf.Lerp(layer.alphaMin, layer.alphaMax, blend));
                var c = img.color;
                c.a = a;
                img.color = c;
            }

            ApplyDisplacementAlongConfiguredAxis(layer, trajectorySpeed01, dt, omegaAlpha, pace);
        }

        private static void CaptureRestIfNeeded(EdgeLayer layer)
        {
            if (layer.RestCaptured)
                return;
            layer.RestLocalPosition = layer.rect.localPosition;
            layer.RestCaptured = true;
        }

        private void RestoreLayerLocalPositionsToRest()
        {
            foreach (var layer in edgeLayers)
            {
                if (layer?.rect == null || !layer.RestCaptured)
                    continue;
                layer.rect.localPosition = layer.RestLocalPosition;
            }
        }

        /// <summary>
        /// 在 Rect 自身的局部 ±X（或 ±Y）方向位移；该方向换算到<strong>父空间</strong>后叠加到 localPosition
        ///（Unity 下子物体 localPosition 相对父轴向，不能直接只改 localPosition.x）。
        /// </summary>
        private void ApplyDisplacementAlongConfiguredAxis(
            EdgeLayer layer,
            float trajectorySpeed01,
            float dt,
            float omegaAlphaScaled,
            float pace)
        {
            if (layer.positionAmplitude <= 1e-5f)
            {
                layer.rect.localPosition = layer.RestLocalPosition;
                return;
            }

            var omegaPos = layer.useAlphaFrequencyForPosition
                ? omegaAlphaScaled
                : Mathf.Max(
                    0f,
                    layer.positionBaseAngularVelocity +
                    trajectorySpeed01 * layer.positionAngularVelocityFromTrajectorySpeed01) * pace;

            layer.PositionPhaseAccumulator += dt * omegaPos;

            var localAxis = displacementAxis == OscillationDisplacementAxis.LocalX
                ? Vector3.right
                : Vector3.up;
            var oscillationWorld = layer.rect.TransformDirection(localAxis);

            Transform parent = layer.rect.parent;
            var oscillationInParentSpace = parent != null
                ? Quaternion.Inverse(parent.rotation) * oscillationWorld
                : oscillationWorld;

            var s = Mathf.Sin(layer.PositionPhaseAccumulator + layer.positionPhaseOffsetRadians);
            layer.rect.localPosition = layer.RestLocalPosition + oscillationInParentSpace * (s * layer.positionAmplitude);
        }

        private void LazyBuildDefaults()
        {
            if (_defaultsBuilt)
                return;
            _defaultsBuilt = true;

            var hasConfigured = edgeLayers != null && edgeLayers.Length > 0;
            if (hasConfigured)
            {
                if (_canvas == null && edgeLayers.Length > 0 && edgeLayers[0].rect != null)
                    _canvas = edgeLayers[0].rect.GetComponentInParent<Canvas>();
                return;
            }

            if (!buildDefaultLayersIfEmpty)
                return;

            _canvas = EnsureCanvasFor(transform);
            edgeLayers = BuildDefaultEdgeLayers(_canvas.transform);
        }

        private Canvas EnsureCanvasFor(Transform parent)
        {
            var cvGo = new GameObject("GlideAirflowCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cvGo.layer = parent.gameObject.layer;
            cvGo.transform.SetParent(parent, false);
            cvGo.transform.SetAsFirstSibling();

            var canvas = cvGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = canvasSortOrder;

            var scaler = cvGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            cvGo.SetActive(false);

            return canvas;
        }

        /// <summary>自动生成：屏幕四边 + 四角补强条（8 层，角速度与相位各不相同）。</summary>
        private static EdgeLayer[] BuildDefaultEdgeLayers(Transform canvasTransform)
        {
            const float thick = 0.16f;
            const float corner = 0.12f;

            return new[]
            {
                Band(canvasTransform, "Wind_L", new Vector2(0f, 0f), new Vector2(thick, 1f),
                    1.12f, 3.05f, 0.2f, 0.022f, 0.24f),

                Band(canvasTransform, "Wind_R", new Vector2(1f - thick, 0f), new Vector2(1f, 1f),
                    0.88f, 3.82f, 1.95f, 0.03f, 0.26f),

                Band(canvasTransform, "Wind_B", new Vector2(0f, 0f), new Vector2(1f, thick),
                    1.42f, 2.08f, 0.71f, 0.026f, 0.17f),

                Band(canvasTransform, "Wind_T", new Vector2(0f, 1f - thick), new Vector2(1f, 1f),
                    0.74f, 4.05f, 3.08f, 0.026f, 0.21f),

                Band(canvasTransform, "Wind_NB", new Vector2(0f, 1f - corner), new Vector2(corner, 1f),
                    0.93f, 2.92f, 2.41f, 0.019f, 0.13f),

                Band(canvasTransform, "Wind_SB", new Vector2(1f - corner, 1f - corner), Vector2.one,
                    1.06f, 3.52f, 4.93f, 0.019f, 0.13f),

                Band(canvasTransform, "Wind_SW", Vector2.zero, new Vector2(corner, corner),
                    1.31f, 2.74f, 1.06f, 0.019f, 0.13f),

                Band(canvasTransform, "Wind_NE", new Vector2(1f - corner, 0f), new Vector2(1f, corner),
                    0.62f, 3.93f, 5.55f, 0.019f, 0.13f),
            };
        }

        private static EdgeLayer Band(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float baseOmega,
            float omegaFromSpeed01,
            float phase,
            float aMin,
            float aMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            AttachWhite(img);
            img.color = new Color(1f, 1f, 1f, aMin);

            return new EdgeLayer
            {
                rect = rt,
                baseAngularVelocity = baseOmega,
                angularVelocityFromTrajectorySpeed01 = omegaFromSpeed01,
                phaseOffsetRadians = phase,
                alphaMin = aMin,
                alphaMax = aMax,
                PhaseAccumulator = phase,
                PositionPhaseAccumulator = phase * 1.37f + 0.61f,
                positionPhaseOffsetRadians = phase * 2.03f + 0.93f,
                positionAmplitude = 15f,
                useAlphaFrequencyForPosition = false,
                positionBaseAngularVelocity = Mathf.Max(0.35f, baseOmega * 0.92f),
                positionAngularVelocityFromTrajectorySpeed01 = Mathf.Max(0.5f, omegaFromSpeed01 * 1.08f),
            };
        }

        private static void AttachWhite(Image image)
        {
            if (s_whiteSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                s_whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }

            image.sprite = s_whiteSprite;
            image.type = Image.Type.Simple;
        }

        private static GlideController ResolveGlideController()
        {
#if UNITY_2023_1_OR_NEWER
            var list = UnityEngine.Object.FindObjectsByType<GlideController>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var list = UnityEngine.Object.FindObjectsOfType<GlideController>();
#endif
            return list != null && list.Length > 0 ? list[0] : null;
        }
    }
}
