using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 鱼线可视化：收线（杆身圆弧）| 抛竿动画（自适应曲线采样）| 钓鱼中（3 点直线）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(200)]
    public sealed class FishingLineVisual : MonoBehaviour
    {
        public const int DefaultRetractedPoints = 16;
        public const int DefaultPointsPerSegment = 4;
        public const int DefaultCastAnimPoints = 32;
        public const int FishingLinePointCount = 3;

        public enum LineVisualState
        {
            Retracted = 0,
            CastAnimating = 1,
            Fishing = 2,
        }

        [Header("杆身节点（从竿根到竿尖）")]
        [SerializeField] private Transform[] rodLineNodes = new Transform[4];

        [Tooltip("可选：线起点（线轮）；留空则用 rodLineNodes[0]。")]
        [SerializeField] private Transform lineOrigin;

        [Header("收线 — 杆身圆弧")]
        [SerializeField] private int retractedPointCount = DefaultRetractedPoints;
        [SerializeField] private int pointsPerSegment = DefaultPointsPerSegment;
        [Tooltip("相邻杆节点之间圆弧凸起量（米）。")]
        [SerializeField] private float retractedArcSag = 0.06f;

        [Header("抛竿动画 — 曲线采样")]
        [SerializeField] private int castAnimPointCount = DefaultCastAnimPoints;
        [Tooltip("高密度参考路径采样数（用于曲率分析，越大越准）。")]
        [SerializeField] private int castAnimDenseSampleCount = 64;
        [Tooltip("曲率对节点密度的影响；越大弯曲处节点越密。")]
        [SerializeField] private float castCurvatureDensityBias = 3f;

        [Header("抛竿动画 — 关键帧（秒）")]
        [Tooltip("抛竿线动画总时长（秒），建议 ≥ 末关键帧。")]
        private float castAnimDuration = 1.5f;
        [Tooltip("拿起竿等待后再进入后摆。")]
        private float castPrepareWaitTime = 0.54f;
        [Tooltip("关键帧1：后摆蓄力。")]
        private float castKeyframe1Time = 0.72f;
        [Tooltip("关键帧2：抛物线落水（前甩结束）。")]
        private float castKeyframe2Time = 1.2f;

        [Header("抛竿动画 — 形态")]
        [SerializeField] private float castForwardDistance = 12f;
        [SerializeField] private float castSwingBackDistance = 5f;
        [SerializeField] private float castSwingBackHeight = 1.5f;
        [Tooltip("前甩抛物线拱高（越大越高；平稳甩出可 0.6~1.0）。")]
        [SerializeField] private float castForwardArcHeight = 0.85f;
        [Tooltip("竿身段占路径采样比例（其余为线端弧线）。")]
        [Range(0.08f, 0.35f)]
        [SerializeField] private float castRodSpanRatio = 0.18f;

        [Tooltip("收线 morph 时长（秒）。")]
        private float retractAnimDuration = 1f;
        [Tooltip("抛竿进度 ≥ 此值时在鱼线触水处播放落水水花（0~1）。")]
        [Range(0.5f, 1f)]
        [SerializeField] private float castSplashTriggerNormalized = 0.88f;

        [Header("钓鱼中 — 落点")]
        [SerializeField] private float castEndDepthBelowTarget = 0.35f;
        [Tooltip("抛竿动画参考曲线：竿尖到落点间的抬升。")]
        [SerializeField] private float castStartLift = 0.15f;
        [SerializeField] private float castCurvaturePower = 2.2f;
        [Tooltip("竿尖到线端因重力下垂量（米）。")]
        [SerializeField] private float fishingLineSag = 0.35f;
        [SerializeField] private int fishingLineSegmentPoints = 6;

        [Header("线宽")]
        [SerializeField] private float lineWidth = 0.09f;

        [Header("演示 / 调试")]
        [Tooltip("关闭时不更新 LineRenderer、不播放抛竿/收线动画（避免卡死，仅保留钓鱼逻辑演示）。")]
        [SerializeField] private bool enableLineEffects = true;

        /// <summary>是否启用鱼线绘制与动画。</summary>
        public bool EnableLineEffects => enableLineEffects;

        LineRenderer _line;
        Transform[] _cachedRodNodes = System.Array.Empty<Transform>();
        readonly List<Vector3> _positions = new List<Vector3>(64);
        readonly List<Vector3> _denseScratch = new List<Vector3>(128);
        readonly List<Vector3> _retractedScratch = new List<Vector3>(32);
        readonly List<Vector3> _castKeyframeScratchA = new List<Vector3>(64);
        readonly List<Vector3> _castKeyframeScratchB = new List<Vector3>(64);

        Vector3 _castTargetWorld;
        Vector3 _castLandingWorld;
        Vector3 _castForwardPlanar = Vector3.forward;
        float _waterSurfaceY;
        Vector3 _lockedLineEndWorld;
        bool _hasLockedLineEnd;
        bool _catchRetrievalActive;
        bool _catchLineStraight;
        bool _guideLineActive;
        bool _castSplashPlayed;
        LineVisualState _state = LineVisualState.Retracted;
        Coroutine _animCoroutine;

        public LineVisualState State => _state;
        public Vector3 LockedLineEndWorld => _lockedLineEndWorld;
        public bool IsCatchRetrievalActive => _catchRetrievalActive;

        /// <summary>竿尖世界坐标（每帧随竿更新）。</summary>
        public Vector3 RodTipWorld => GetRodLastWorld();

        public float WaterSurfaceY => _waterSurfaceY;
        public float CastForwardDistance => castForwardDistance;

        /// <summary>鱼线触水（抛竿落水）。</summary>
        public event Action<Vector3> LineHitWater;

        /// <summary>收线动画播放完毕（进入 Retracted 状态）时触发。</summary>
        public event Action RetractAnimationCompleted;

        /// <summary>当前鱼线终点世界坐标（收线/抛竿动画与钓鱼中均可用）。</summary>
        public Vector3 CurrentLineEndWorld
        {
            get
            {
                if (_positions.Count > 0)
                    return _positions[_positions.Count - 1];
                if (_hasLockedLineEnd)
                    return _lockedLineEndWorld;
                return ResolveLineEndWorld(_castTargetWorld);
            }
        }

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            EnsureLineRendererSetup();
            ResolveRodNodesIfEmpty();
            RefreshRodNodeCache();
            if (!enableLineEffects)
                HideLineCompletely();
        }

        void OnEnable()
        {
            EnsureLineRendererSetup();
            RefreshRodNodeCache();
            if (!enableLineEffects)
                HideLineCompletely();
            else
                SetRetractedImmediate();
        }

        void LateUpdate()
        {
            if (!enableLineEffects)
                return;

            if (_state == LineVisualState.CastAnimating)
                return;

            if (_state == LineVisualState.Retracted)
                ApplyRetractedLine();
            else if (_state == LineVisualState.Fishing)
                ApplyFishingLine();

            PushToLineRenderer();
        }

        void HideLineCompletely()
        {
            StopAnim();
            _hasLockedLineEnd = false;
            _state = LineVisualState.Retracted;
            if (_line != null)
            {
                _line.enabled = false;
                _line.positionCount = 0;
            }
        }

        public void SetRetractedImmediate()
        {
            if (!enableLineEffects)
            {
                HideLineCompletely();
                return;
            }

            StopAnim();
            _hasLockedLineEnd = false;
            _catchRetrievalActive = false;
            _guideLineActive = false;
            _state = LineVisualState.Retracted;
            ApplyRetractedLine();
            PushToLineRenderer();
        }

        /// <summary>非钓鱼场景指引线（如力量训练池中心）。</summary>
        public void SetGuideLineTarget(Vector3 worldTarget)
        {
            if (!enableLineEffects)
            {
                HideLineCompletely();
                return;
            }

            StopAnim();
            _guideLineActive = true;
            _catchRetrievalActive = false;
            _hasLockedLineEnd = true;
            _lockedLineEndWorld = worldTarget;
            _waterSurfaceY = worldTarget.y;
            _state = LineVisualState.Fishing;
            if (_line != null)
                _line.enabled = true;
            ApplyFishingLine();
            PushToLineRenderer();
        }

        public void ClearGuideLine()
        {
            _guideLineActive = false;
            if (!_catchRetrievalActive && _state == LineVisualState.Fishing)
                SetRetractedImmediate();
        }

        public void SetFishingImmediate(Vector3 castTargetWorld)
        {
            if (!enableLineEffects)
            {
                HideLineCompletely();
                return;
            }

            StopAnim();
            _waterSurfaceY = castTargetWorld.y + castEndDepthBelowTarget;
            _castLandingWorld = new Vector3(castTargetWorld.x, _waterSurfaceY, castTargetWorld.z);
            _castTargetWorld = _castLandingWorld;
            _lockedLineEndWorld = _castLandingWorld;
            _hasLockedLineEnd = true;
            _state = LineVisualState.Fishing;
            ApplyFishingLine();
            PushToLineRenderer();
        }

        public void PlayCastAnimation(Vector3 castTargetWorld)
        {
            var forward = castTargetWorld - GetRodLastWorld();
            forward.y = 0f;
            var waterY = castTargetWorld.y + castEndDepthBelowTarget;
            PlayCastAnimation(waterY, forward.sqrMagnitude > 0.01f ? forward : transform.root.forward, castForwardDistance);
        }

        /// <summary>按角色朝向与水面高度抛竿；落水点由动画终点决定（仅约束 Y 在水面）。</summary>
        public void PlayCastAnimation(float waterSurfaceY, Vector3 castForwardPlanar, float forwardDistance)
        {
            if (!enableLineEffects)
            {
                SetupCastLanding(waterSurfaceY, castForwardPlanar, forwardDistance);
                _hasLockedLineEnd = true;
                _lockedLineEndWorld = _castLandingWorld;
                _state = LineVisualState.Fishing;
                HideLineCompletely();
                return;
            }

            StopAnim();
            SetupCastLanding(waterSurfaceY, castForwardPlanar, forwardDistance);
            _hasLockedLineEnd = false;
            _castSplashPlayed = false;
            _animCoroutine = StartCoroutine(CoCastAnimation());
        }

        void SetupCastLanding(float waterSurfaceY, Vector3 castForwardPlanar, float forwardDistance)
        {
            _waterSurfaceY = waterSurfaceY;
            _castForwardPlanar = castForwardPlanar;
            _castForwardPlanar.y = 0f;
            if (_castForwardPlanar.sqrMagnitude < 0.0001f)
                _castForwardPlanar = transform.root.forward;
            _castForwardPlanar.Normalize();

            forwardDistance = Mathf.Max(1f, forwardDistance);
            var rodTip = GetRodLastWorld();
            _castLandingWorld = rodTip + _castForwardPlanar * forwardDistance;
            _castLandingWorld.y = _waterSurfaceY;
            _castTargetWorld = _castLandingWorld;
            _lockedLineEndWorld = _castLandingWorld;
        }

        public static float ClampToWaterSurfaceY(float worldY, FishingLineVisual visual)
        {
            if (visual != null)
                return visual._waterSurfaceY;
            return worldY;
        }

        public void PlayRetractAnimation()
        {
            if (!enableLineEffects)
            {
                HideLineCompletely();
                NotifyRetractAnimationCompleted();
                return;
            }

            StopAnim();
            _catchRetrievalActive = false;
            _animCoroutine = StartCoroutine(CoRetractAnimation());
        }

        /// <summary>钓获后进入收鱼阶段：鱼线每帧跟随竿尖到鱼，直至 <see cref="EndCatchRetrieval"/>。</summary>
        public void BeginCatchRetrieval()
        {
            if (!enableLineEffects)
                return;

            StopAnim();
            _catchRetrievalActive = true;
            _catchLineStraight = false;
            _hasLockedLineEnd = true;
            _lockedLineEndWorld = CurrentLineEndWorld;
            _state = LineVisualState.Fishing;
            if (_line != null)
                _line.enabled = true;
            ApplyFishingLine();
            PushToLineRenderer();
        }

        public void SetCatchRetrievalLineEnd(Vector3 world)
        {
            if (!_catchRetrievalActive)
                return;

            _lockedLineEndWorld = world;
            _hasLockedLineEnd = true;
            if (_state != LineVisualState.Fishing)
            {
                _state = LineVisualState.Fishing;
                if (_line != null)
                    _line.enabled = true;
            }

            if (enableLineEffects)
            {
                ApplyFishingLine();
                PushToLineRenderer();
            }
        }

        public void SetCatchRetrievalLineStraight(bool straight)
        {
            if (!_catchRetrievalActive)
                return;
            _catchLineStraight = straight;
        }

        public void EndCatchRetrieval()
        {
            _catchRetrievalActive = false;
            _catchLineStraight = false;
        }

        /// <summary>停止抛竿/收线 coroutine（不打断 Retracted/Fishing 状态）。</summary>
        public void StopAllLineAnimations()
        {
            StopAnim();
        }

        void SetCastTarget(Vector3 world)
        {
            _castTargetWorld = world;
            _lockedLineEndWorld = ResolveLineEndWorld(world);
        }

        Vector3 ResolveLineEndWorld(Vector3 castTargetWorld)
        {
            if (castTargetWorld == Vector3.zero && _hasLockedLineEnd)
                return _lockedLineEndWorld;
            return castTargetWorld + Vector3.down * castEndDepthBelowTarget;
        }

        void StopAnim()
        {
            if (_animCoroutine == null)
                return;
            StopCoroutine(_animCoroutine);
            _animCoroutine = null;
        }

        IEnumerator CoCastAnimation()
        {
            _state = LineVisualState.CastAnimating;
            var duration = ResolveCastAnimEndTime();
            var pointBudget = Mathf.Clamp(castAnimPointCount, 8, 48);
            var denseBudget = Mathf.Clamp(castAnimDenseSampleCount, 8, 64);

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                BuildCastAnimPathAtTime(elapsed, _denseScratch, denseBudget);
                ResampleByCurvature(_denseScratch, pointBudget, castCurvatureDensityBias, _positions);
                PushToLineRenderer();

                if (!_castSplashPlayed && elapsed / duration >= castSplashTriggerNormalized)
                    TryNotifyLineHitWaterAtLineEnd();

                yield return null;
            }

            _hasLockedLineEnd = true;
            _lockedLineEndWorld = _castLandingWorld;
            _state = LineVisualState.Fishing;
            ApplyFishingLine();
            PushToLineRenderer();
            if (!_castSplashPlayed)
                TryNotifyLineHitWaterAtLineEnd();
            _animCoroutine = null;
        }

        float ResolveCastAnimEndTime() =>
            Mathf.Max(castAnimDuration, castKeyframe2Time, castKeyframe1Time, castPrepareWaitTime, 0.05f);

        void TryNotifyLineHitWaterAtLineEnd()
        {
            if (_castSplashPlayed)
                return;

            _castSplashPlayed = true;
            if (!TryGetLineWaterContact(out var contact))
            {
                var end = _positions.Count > 0
                    ? _positions[_positions.Count - 1]
                    : (_castLandingWorld.sqrMagnitude > 0.01f ? _castLandingWorld : CurrentLineEndWorld);
                contact = new Vector3(end.x, _waterSurfaceY, end.z);
            }

            LineHitWater?.Invoke(contact);
        }

        /// <summary>当前鱼线折线与水面 / WaterEdge 的首次接触点。</summary>
        public bool TryGetLineWaterContact(out Vector3 contactWorld) =>
            FishingWaterSurfaceContact.TryGetPolylineContact(_positions, _waterSurfaceY, out contactWorld);

        /// <summary>线段与水面 / WaterEdge 的接触点（如竿尖 → 钩）。</summary>
        public bool TryGetSegmentWaterContact(Vector3 fromWorld, Vector3 toWorld, out Vector3 contactWorld) =>
            FishingWaterSurfaceContact.TryGetSegmentContact(fromWorld, toWorld, _waterSurfaceY, out contactWorld);

        IEnumerator CoRetractAnimation()
        {
            _state = LineVisualState.CastAnimating;
            _catchRetrievalActive = false;
            var duration = Mathf.Max(0.05f, retractAnimDuration);
            var pointBudget = Mathf.Clamp(castAnimPointCount, 8, 48);
            var denseBudget = Mathf.Clamp(castAnimDenseSampleCount, 8, 64);

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var t = SmoothStep(Mathf.Clamp01(elapsed / duration));
                ApplyFishingLine();
                var fromLive = new List<Vector3>(_positions);
                BuildRetractedPath(_retractedScratch);
                BuildRetractMorphPath(fromLive, _retractedScratch, t, _denseScratch, denseBudget);
                ResampleByCurvature(_denseScratch, pointBudget, castCurvatureDensityBias, _positions);
                PushToLineRenderer();
                yield return null;
            }

            SetRetractedImmediate();
            _animCoroutine = null;
            NotifyRetractAnimationCompleted();
        }

        void NotifyRetractAnimationCompleted() => RetractAnimationCompleted?.Invoke();

        /// <summary>抛竿：准备(收线)→后摆→抛物线落水。</summary>
        void BuildCastAnimPathAtTime(float elapsedSeconds, List<Vector3> denseOut, int denseCount)
        {
            var kp = Mathf.Max(0f, castPrepareWaitTime);
            var k1 = Mathf.Max(kp + 0.05f, castKeyframe1Time);
            var k2 = Mathf.Max(k1 + 0.05f, castKeyframe2Time);

            if (elapsedSeconds <= kp)
            {
                BuildCastKeyframePath(0, denseOut, denseCount);
                return;
            }

            if (elapsedSeconds <= k1)
            {
                var t = SmoothStep(Mathf.Clamp01((elapsedSeconds - kp) / (k1 - kp)));
                BuildCastKeyframePath(0, _castKeyframeScratchA, denseCount);
                BuildCastKeyframePath(1, _castKeyframeScratchB, denseCount);
                MorphPolylinePaths(_castKeyframeScratchA, _castKeyframeScratchB, t, denseOut, denseCount);
                return;
            }

            if (elapsedSeconds >= k2)
            {
                BuildCastKeyframePath(2, denseOut, denseCount);
                return;
            }

            var tLand = SmoothStep(Mathf.Clamp01((elapsedSeconds - k1) / (k2 - k1)));
            BuildCastKeyframePath(1, _castKeyframeScratchA, denseCount);
            BuildCastKeyframePath(2, _castKeyframeScratchB, denseCount);
            MorphPolylinePaths(_castKeyframeScratchA, _castKeyframeScratchB, tLand, denseOut, denseCount);
        }

        /// <summary>0=收线杆身，1=后摆，2=抛物线落水。</summary>
        void BuildCastKeyframePath(int keyframeIndex, List<Vector3> denseOut, int denseCount)
        {
            denseCount = Mathf.Max(denseCount, 8);
            switch (keyframeIndex)
            {
                case 0:
                    BuildRetractedPath(denseOut);
                    PadPolylineToCount(denseOut, denseCount);
                    return;
                case 1:
                    BuildBackSwingCastPath(denseOut, denseCount);
                    return;
                default:
                    BuildParabolicFishingPath(denseOut, denseCount);
                    return;
            }
        }

        /// <summary>关键帧1：竿身 + 线往身后上方拖出（后摆蓄力）。</summary>
        void BuildBackSwingCastPath(List<Vector3> denseOut, int denseCount)
        {
            denseOut.Clear();
            var rodFirst = GetRodFirstWorld();
            var rodTip = GetRodLastWorld();
            var backDir = ComputeSwingBackDirection(rodTip);
            var rodSpan = Mathf.Clamp(castRodSpanRatio, 0.08f, 0.35f);
            var backEnd = rodTip
                          + backDir * castSwingBackDistance
                          + Vector3.up * (castSwingBackHeight * 0.75f);
            var backControl = rodTip
                              + backDir * (castSwingBackDistance * 0.42f)
                              + Vector3.up * castSwingBackHeight;

            denseCount = Mathf.Max(denseCount, 8);
            for (var i = 0; i < denseCount; i++)
            {
                var u = i / (float)(denseCount - 1);
                if (u <= rodSpan)
                {
                    denseOut.Add(Vector3.Lerp(rodFirst, rodTip, u / rodSpan));
                    continue;
                }

                var lineT = (u - rodSpan) / (1f - rodSpan);
                denseOut.Add(QuadraticBezier(rodTip, backControl, backEnd, lineT));
            }

            denseOut[denseOut.Count - 1] = backEnd;
        }

        /// <summary>关键帧2：竿身 + 平稳抛物线至水面落点。</summary>
        void BuildParabolicFishingPath(List<Vector3> denseOut, int denseCount)
        {
            denseOut.Clear();
            var rodFirst = GetRodFirstWorld();
            var rodTip = GetRodLastWorld();
            var landing = _castLandingWorld;
            var rodSpan = Mathf.Clamp(castRodSpanRatio, 0.08f, 0.35f);
            var control = Vector3.Lerp(rodTip, landing, 0.5f) + Vector3.up * castForwardArcHeight;

            denseCount = Mathf.Max(denseCount, 8);
            for (var i = 0; i < denseCount; i++)
            {
                var u = i / (float)(denseCount - 1);
                if (u <= rodSpan)
                {
                    denseOut.Add(Vector3.Lerp(rodFirst, rodTip, u / rodSpan));
                    continue;
                }

                var lineT = (u - rodSpan) / (1f - rodSpan);
                denseOut.Add(QuadraticBezier(rodTip, control, landing, lineT));
            }

            denseOut[denseOut.Count - 1] = landing;
        }

        /// <summary>钓鱼中：竿根 → 竿尖 → 线端（收鱼拉起后为直线；平时为重力线下垂）。</summary>
        void ApplyFishingLine()
        {
            _positions.Clear();
            var rodFirst = GetRodFirstWorld();
            var rodTip = GetRodLastWorld();
            var lineEnd = ResolveFishingLineEndWorld();
            _positions.Add(rodFirst);
            _positions.Add(rodTip);

            if (_guideLineActive || (_catchRetrievalActive && _catchLineStraight))
            {
                _positions.Add(lineEnd);
                return;
            }

            AppendGravitySagSegment(rodTip, lineEnd, _positions);
        }

        Vector3 ResolveFishingLineEndWorld()
        {
            if (_hasLockedLineEnd)
            {
                if (_catchRetrievalActive || _guideLineActive)
                    return _lockedLineEndWorld;
                return new Vector3(_lockedLineEndWorld.x, _waterSurfaceY, _lockedLineEndWorld.z);
            }

            return _castLandingWorld;
        }

        void AppendGravitySagSegment(Vector3 fromWorld, Vector3 toWorld, List<Vector3> output)
        {
            var count = Mathf.Clamp(fishingLineSegmentPoints, 2, 16);
            var sag = toWorld.y < fromWorld.y - 0.05f ? fishingLineSag : fishingLineSag * 0.12f;
            for (var i = 1; i <= count; i++)
            {
                var t = i / (float)count;
                output.Add(SampleArc(fromWorld, toWorld, t, sag, gravitySag: true));
            }
        }

        static void MorphPolylinePaths(
            IReadOnlyList<Vector3> from,
            IReadOnlyList<Vector3> to,
            float t,
            List<Vector3> denseOut,
            int denseCount)
        {
            denseOut.Clear();
            denseCount = Mathf.Max(denseCount, 8);
            for (var i = 0; i < denseCount; i++)
            {
                var u = i / (float)(denseCount - 1);
                var a = SamplePolyline(from, u);
                var b = SamplePolyline(to, u);
                denseOut.Add(Vector3.Lerp(a, b, t));
            }
        }

        static void PadPolylineToCount(List<Vector3> points, int targetCount)
        {
            if (points.Count == 0)
                return;

            targetCount = Mathf.Max(targetCount, 2);
            while (points.Count < targetCount)
                points.Add(points[points.Count - 1]);
        }

        /// <summary>兼容旧接口：normalized 0~1 映射到动画时长。</summary>
        void BuildCastAnimPath(float animT, List<Vector3> denseOut, int denseCount) =>
            BuildCastAnimPathAtTime(animT * ResolveCastAnimEndTime(), denseOut, denseCount);

        void BuildRetractMorphPath(
            List<Vector3> fromFishing,
            List<Vector3> retracted,
            float t,
            List<Vector3> denseOut,
            int denseCount)
        {
            denseOut.Clear();
            denseCount = Mathf.Max(denseCount, 8);
            for (var i = 0; i < denseCount; i++)
            {
                var u = i / (float)(denseCount - 1);
                var a = SamplePolyline(fromFishing, u);
                var b = SamplePolyline(retracted, u);
                denseOut.Add(Vector3.Lerp(a, b, t));
            }
        }

        void ApplyRetractedLine()
        {
            BuildRetractedPath(_positions);
        }

        /// <summary>抛竿最终形态的高密度参考：直线(根→尖) + 曲线(尖→落点)。</summary>
        void BuildFishingArcDensePath(Vector3 rodFirst, Vector3 rodTip, Vector3 lineEnd, List<Vector3> denseOut, int sampleCount)
        {
            denseOut.Clear();
            var n = Mathf.Clamp(sampleCount, 8, 64);
            var rodSpan = Mathf.Max(0.08f, 0.22f);

            for (var i = 0; i < n; i++)
            {
                var u = i / (float)(n - 1);
                if (u <= rodSpan)
                {
                    var t = u / rodSpan;
                    denseOut.Add(Vector3.Lerp(rodFirst, rodTip, t));
                }
                else
                {
                    var t = (u - rodSpan) / (1f - rodSpan);
                    var curved = Mathf.Pow(t, castCurvaturePower);
                    var basePos = Vector3.Lerp(rodTip, lineEnd, t);
                    var lift = castStartLift * (1f - t) * (1f - curved * 0.35f);
                    var drop = Mathf.Lerp(0f, -castEndDepthBelowTarget * 0.5f, curved);
                    basePos.y += lift + drop;
                    denseOut.Add(basePos);
                }
            }

            denseOut[denseOut.Count - 1] = lineEnd;
        }

        void BuildRetractedPath(List<Vector3> output)
        {
            output.Clear();
            var segments = ResolveSegmentCount();
            var perSeg = Mathf.Clamp(pointsPerSegment, 2, 8);
            var maxPoints = Mathf.Clamp(retractedPointCount, 4, 32);

            for (var s = 0; s < segments && output.Count < maxPoints; s++)
            {
                var a = GetSegmentStartWorld(s);
                var b = GetSegmentEndWorld(s);
                var sag = retractedArcSag * (1f + s * 0.08f);
                for (var j = 0; j < perSeg && output.Count < maxPoints; j++)
                {
                    var t = j / (float)(perSeg - 1);
                    output.Add(SampleArc(a, b, t, sag));
                }
            }

            var padGuard = 0;
            while (output.Count < maxPoints && output.Count > 0 && padGuard++ < maxPoints)
                output.Add(output[output.Count - 1]);
        }

        /// <summary>按曲率非均分重采样：弯曲越大分配越多节点。</summary>
        static void ResampleByCurvature(
            IReadOnlyList<Vector3> dense,
            int outputCount,
            float curvatureBias,
            List<Vector3> output)
        {
            output.Clear();
            if (dense == null || dense.Count == 0)
                return;

            outputCount = Mathf.Max(2, outputCount);
            if (dense.Count <= outputCount)
            {
                for (var i = 0; i < dense.Count; i++)
                    output.Add(dense[i]);
                return;
            }

            var n = dense.Count;
            var weights = new float[n];
            weights[0] = 1f;
            weights[n - 1] = 1f;

            for (var i = 1; i < n - 1; i++)
            {
                var v0 = dense[i] - dense[i - 1];
                var v1 = dense[i + 1] - dense[i];
                if (v0.sqrMagnitude < 0.000001f || v1.sqrMagnitude < 0.000001f)
                {
                    weights[i] = 1f;
                    continue;
                }

                var angle = Vector3.Angle(v0, v1);
                weights[i] = 1f + curvatureBias * (angle / 180f);
            }

            var cumulative = new float[n];
            cumulative[0] = weights[0];
            for (var i = 1; i < n; i++)
                cumulative[i] = cumulative[i - 1] + weights[i];

            var total = cumulative[n - 1];
            for (var o = 0; o < outputCount; o++)
            {
                var target = total * (o / (float)(outputCount - 1));
                var idx = FindCumulativeIndex(cumulative, target);
                output.Add(dense[idx]);
            }
        }

        static int FindCumulativeIndex(float[] cumulative, float target)
        {
            for (var i = 0; i < cumulative.Length; i++)
            {
                if (cumulative[i] >= target)
                    return i;
            }

            return cumulative.Length - 1;
        }

        static Vector3 SamplePolyline(IReadOnlyList<Vector3> points, float t01)
        {
            if (points == null || points.Count == 0)
                return Vector3.zero;
            if (points.Count == 1)
                return points[0];

            t01 = Mathf.Clamp01(t01);
            var f = t01 * (points.Count - 1);
            var i0 = Mathf.FloorToInt(f);
            var i1 = Mathf.Min(i0 + 1, points.Count - 1);
            var localT = f - i0;
            return Vector3.Lerp(points[i0], points[i1], localT);
        }

        static Vector3 SampleArc(Vector3 a, Vector3 b, float t, float sag, bool gravitySag = false)
        {
            var mid = (a + b) * 0.5f;
            var control = mid + ComputeArcBulgeDirection(a, b, gravitySag) * sag;
            return QuadraticBezier(a, control, b, t);
        }

        static Vector3 ComputeArcBulgeDirection(Vector3 a, Vector3 b, bool gravitySag = false)
        {
            var seg = b - a;
            if (seg.sqrMagnitude < 0.000001f)
                return gravitySag ? Vector3.down : Vector3.up;

            var segDir = seg.normalized;
            var reference = gravitySag ? Vector3.down : Vector3.up;
            var bulge = reference - segDir * Vector3.Dot(reference, segDir);
            if (bulge.sqrMagnitude < 0.000001f)
            {
                bulge = Vector3.forward - segDir * Vector3.Dot(Vector3.forward, segDir);
                if (bulge.sqrMagnitude < 0.000001f)
                    return reference;
            }

            return bulge.normalized;
        }

        static Vector3 QuadraticBezier(Vector3 a, Vector3 c, Vector3 b, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        void PushToLineRenderer()
        {
            if (_line == null)
                return;

            _line.positionCount = _positions.Count;
            for (var i = 0; i < _positions.Count; i++)
                _line.SetPosition(i, WorldToLineLocal(_positions[i]));
        }

        Vector3 WorldToLineLocal(Vector3 world) => transform.InverseTransformPoint(world);

        int ResolveSegmentCount()
        {
            var nodeCount = GetValidRodNodeCount();
            if (nodeCount <= 0) return 1;
            if (lineOrigin != null) return nodeCount;
            if (nodeCount >= 4) return 4;
            return Mathf.Max(1, nodeCount - 1);
        }

        static bool nodeCountIsFourWithFourSegments(Transform[] nodes, int segmentIndex) =>
            nodes.Length >= 4 && segmentIndex < 4;

        Vector3 GetSegmentStartWorld(int segmentIndex)
        {
            if (lineOrigin != null)
            {
                if (segmentIndex == 0)
                    return lineOrigin.position;
                var nodes = GetValidRodNodes();
                if (nodes.Length == 0)
                    return transform.position;
                return nodes[Mathf.Min(segmentIndex - 1, nodes.Length - 1)].position;
            }

            var rodNodes = GetValidRodNodes();
            if (rodNodes.Length == 0)
                return transform.position;
            return rodNodes[Mathf.Min(segmentIndex, rodNodes.Length - 1)].position;
        }

        Vector3 GetSegmentEndWorld(int segmentIndex)
        {
            var nodes = GetValidRodNodes();
            if (nodes.Length == 0)
                return transform.position + transform.forward;

            if (lineOrigin != null)
            {
                if (segmentIndex < nodes.Length)
                    return nodes[segmentIndex].position;
                return nodes[nodes.Length - 1].position;
            }

            if (nodeCountIsFourWithFourSegments(nodes, segmentIndex))
            {
                if (segmentIndex < nodes.Length - 1)
                    return nodes[segmentIndex + 1].position;
                var tip = nodes[nodes.Length - 1];
                return tip.position + tip.forward * 0.12f;
            }

            var endIndex = Mathf.Min(segmentIndex + 1, nodes.Length - 1);
            return nodes[endIndex].position;
        }

        Vector3 GetRodFirstWorld()
        {
            if (lineOrigin != null)
                return lineOrigin.position;
            var nodes = GetValidRodNodes();
            return nodes.Length > 0 ? nodes[0].position : transform.position;
        }

        Vector3 GetRodLastWorld()
        {
            var nodes = GetValidRodNodes();
            if (nodes.Length == 0)
                return transform.position + transform.forward * 0.5f;
            return nodes[nodes.Length - 1].position;
        }

        Vector3 ComputeSwingBackDirection(Vector3 rodTip)
        {
            var playerForward = transform.root.forward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.0001f)
                playerForward = Vector3.forward;

            var toTarget = _castTargetWorld - rodTip;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
                return (-toTarget.normalized + -playerForward.normalized).normalized;

            return -playerForward.normalized;
        }

        void RefreshRodNodeCache()
        {
            if (rodLineNodes == null || rodLineNodes.Length == 0)
            {
                _cachedRodNodes = System.Array.Empty<Transform>();
                return;
            }

            var list = new List<Transform>(rodLineNodes.Length);
            for (var i = 0; i < rodLineNodes.Length; i++)
            {
                if (rodLineNodes[i] != null)
                    list.Add(rodLineNodes[i]);
            }

            _cachedRodNodes = list.Count > 0 ? list.ToArray() : System.Array.Empty<Transform>();
        }

        Transform[] GetValidRodNodes()
        {
            if (_cachedRodNodes == null || _cachedRodNodes.Length == 0)
                RefreshRodNodeCache();
            return _cachedRodNodes;
        }

        int GetValidRodNodeCount() => GetValidRodNodes().Length;

        void EnsureLineRendererSetup()
        {
            if (_line == null)
                _line = GetComponent<LineRenderer>();

            _line.enabled = enableLineEffects;
            _line.useWorldSpace = false;
            _line.widthMultiplier = lineWidth;
            _line.numCornerVertices = 4;
            _line.numCapVertices = 4;
            if (_line.sharedMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    _line.sharedMaterial = new Material(shader);
            }
        }

        void ResolveRodNodesIfEmpty()
        {
            if (GetValidRodNodeCount() >= 2)
                return;

            var found = new Transform[4];
            var n = 0;
            for (var i = 0; i < 8 && n < 4; i++)
            {
                var t = transform.parent != null
                    ? transform.parent.Find($"LineNode{i}")
                    : null;
                if (t == null && transform.parent != null)
                    t = transform.parent.Find($"LineNode_{i}");
                if (t != null)
                    found[n++] = t;
            }

            if (n >= 2)
            {
                rodLineNodes = new Transform[n];
                for (var i = 0; i < n; i++)
                    rodLineNodes[i] = found[i];
            }

            RefreshRodNodeCache();
        }

        static float SmoothStep(float t) => t * t * (3f - 2f * t);

#if UNITY_EDITOR
        public void SetCastImmediate(Vector3 castTargetWorld) => SetFishingImmediate(castTargetWorld);

        /// <summary>编辑器：预览抛竿动画某一时刻（0=收线，1=末关键帧）。</summary>
        public void EditorPreviewCastAt(float normalizedTime, float waterSurfaceY, Vector3 castForwardPlanar, float forwardDistance)
        {
            if (!enableLineEffects)
                return;

            EnsureLineRendererSetup();
            RefreshRodNodeCache();
            SetupCastLanding(waterSurfaceY, castForwardPlanar, forwardDistance);
            _state = LineVisualState.CastAnimating;
            _catchRetrievalActive = false;
            _castSplashPlayed = true;
            if (_line != null)
                _line.enabled = true;

            var elapsed = Mathf.Clamp01(normalizedTime) * ResolveCastAnimEndTime();
            var pointBudget = Mathf.Clamp(castAnimPointCount, 8, 48);
            var denseBudget = Mathf.Clamp(castAnimDenseSampleCount, 8, 64);
            BuildCastAnimPathAtTime(elapsed, _denseScratch, denseBudget);
            ResampleByCurvature(_denseScratch, pointBudget, castCurvatureDensityBias, _positions);
            PushToLineRenderer();
        }

        /// <summary>编辑器：预览收线 morph 某一时刻。</summary>
        public void EditorPreviewRetractAt(float normalizedTime, float waterSurfaceY, Vector3 castForwardPlanar, float forwardDistance)
        {
            if (!enableLineEffects)
                return;

            EnsureLineRendererSetup();
            RefreshRodNodeCache();
            SetupCastLanding(waterSurfaceY, castForwardPlanar, forwardDistance);
            _hasLockedLineEnd = true;
            _state = LineVisualState.Fishing;
            _catchRetrievalActive = false;
            if (_line != null)
                _line.enabled = true;

            var pointBudget = Mathf.Clamp(castAnimPointCount, 8, 48);
            var denseBudget = Mathf.Clamp(castAnimDenseSampleCount, 8, 64);
            var t = SmoothStep(Mathf.Clamp01(normalizedTime));
            ApplyFishingLine();
            var fromLive = new List<Vector3>(_positions);
            BuildRetractedPath(_retractedScratch);
            BuildRetractMorphPath(fromLive, _retractedScratch, t, _denseScratch, denseBudget);
            ResampleByCurvature(_denseScratch, pointBudget, castCurvatureDensityBias, _positions);
            PushToLineRenderer();
        }

        void OnDrawGizmosSelected()
        {
            if (_hasLockedLineEnd || _state == LineVisualState.Fishing)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_castLandingWorld.sqrMagnitude > 0.01f ? _castLandingWorld : _lockedLineEndWorld, 0.15f);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(RodTipWorld, 0.06f);
        }
#else
        void OnDrawGizmosSelected()
        {
            if (_hasLockedLineEnd || _state == LineVisualState.Fishing)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_lockedLineEndWorld, 0.15f);
            }
        }
#endif
    }
}
