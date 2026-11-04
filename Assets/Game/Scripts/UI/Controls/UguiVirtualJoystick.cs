using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.Controls
{
    /// <summary>
    /// Canvas 虚拟摇杆：拖拽区域一般为带 <see cref="UnityEngine.UI.Image"/> raycast 的 RectTransform，
    /// 输出归一化向量 [-1,1]，与 <see cref="Game.Player.PlayerJoystickInputBridge"/> 使用的 FairyGUI 版语义对齐。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-122)]
    public sealed class UguiVirtualJoystick : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler,
        IDragHandler
    {
        public enum ResponseCurveMode
        {
            Linear = 0,
            Exponential = 1,
        }

        public enum CenterMode
        {
            Fixed = 0,
            Dynamic = 1,
        }

        /// <summary>
        /// <see cref="AnalogNormalized"/>：杆位比例映射速度与输入模长。<br/>
        /// <see cref="RimDigitalDirection"/>：仅按「触控相对区域默认中心」的方位判定；base/knob 始终贴在可动范围内缘（外圈）；输出恒为单位向量（无微操调速）。
        /// </summary>
        public enum JoystickValueMode
        {
            AnalogNormalized = 0,
            RimDigitalDirection = 1,
        }

        [Header("Hierarchy")]
        [SerializeField] private RectTransform area;
        [SerializeField] private RectTransform knob;
        [Tooltip("可为空：动态摇杆背景的圆环位移；固定中心时可与 area 同色底图设为同一物体。")]
        [SerializeField] private RectTransform baseRing;

        [Header("Sizing")]
        [Tooltip("Analog：杆端最大位移（像素，Area 局部）。Rim：与区域可动内接半径取较小值，决定外圈贴边位置。")]
        [SerializeField] private float radiusPixels = 120f;

        [Header("Input")]
        [SerializeField] private JoystickValueMode valueMode = JoystickValueMode.AnalogNormalized;

        [Header("Feel")]
        [SerializeField, Range(0f, 1f)] private float deadZone = 0.1f;
        [SerializeField] private CenterMode centerMode = CenterMode.Dynamic;
        [Tooltip("Rim 满载方向模式下会忽略该项，始终以区域默认中心为方向原点。")]
        [SerializeField] private bool edgeSnap = true;
        [SerializeField] private float edgePadding = 6f;
        [SerializeField] private float reboundDuration = 0.12f;
        [Tooltip("Analog + Dynamic：松手后是否将占位中心拉回默认中心（Rim 模式始终回程）。")]
        [SerializeField] private bool returnCenterToDefaultOnRelease = true;
        [SerializeField] private ResponseCurveMode responseCurve = ResponseCurveMode.Linear;
        [SerializeField] private float responseExponent = 1.75f;
        [SerializeField] private bool axisLockAssist;
        [SerializeField, Range(0f, 1f)] private float axisLockThreshold = 0.2f;
        [SerializeField, Range(0f, 1f)] private float axisLockStrength = 0.85f;

        public Vector2 Value { get; private set; }

        /// <summary>当前是否有手指落在此摇杆上（含尚未收到抬起但仍在拖拽的情况）。</summary>
        public bool IsPressed => _pointerId.HasValue;

        public event Action<Vector2> ValueChanged;
        public event Action Pressed;
        public event Action Released;

        private Canvas _canvas;
        private Camera _canvasCamera;

        private int? _pointerId;
        private Vector2 _centerAnchoredInArea;
        private Vector2 _defaultCenterAnchored;
        /// <summary>Analog：相对逻辑中心的 knob 偏移（Area 本地）。</summary>
        private Vector2 _stickOffsetAnalogPx;

        /// <summary>设计态下控件几何中心相对 Area.local 原点（通常为 0,0）；用于松手回程与校准。</summary>
        private Vector2 _knobRestCenterAreaLocal;
        private Vector2 _baseRestCenterAreaLocal;

        /// <summary>回弹时 Area 本地的几何中心线性插值（与父子层级无关）。</summary>
        private Vector2 _reboundKnobStartAreaLocal;
        private Vector2 _reboundKnobTargetAreaLocal;
        private Vector2 _reboundBaseStartAreaLocal;
        private Vector2 _reboundBaseTargetAreaLocal;
        private Vector2 _touchDeltaNormalizedAccum;

        private float _lostTouchTimer;
        private const float LostTouchReleaseDelay = 0.15f;

        private bool _releasing;
        private float _reboundElapsed;

        /// <remarks>可由外部在每帧开始时调用以便与输入系统对齐；不传时内部仍会处理松手复原与失联检测。</remarks>
        public void Tick(float deltaTime)
        {
            TickWatchdog(deltaTime);
            TickRebound(deltaTime);
        }

        public Vector2 ConsumeTouchDeltaNormalized()
        {
            var v = _touchDeltaNormalizedAccum;
            _touchDeltaNormalizedAccum = Vector2.zero;
            return v;
        }

        private bool IsRimMode() => valueMode == JoystickValueMode.RimDigitalDirection;

        private bool ShouldResetCenterAfterRelease()
        {
            if (IsRimMode())
                return true;
            return centerMode == CenterMode.Dynamic && returnCenterToDefaultOnRelease;
        }

        public void CancelInteraction()
        {
            if (!IsPressed) return;
            ForceRelease();
        }

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (area == null)
                area = transform as RectTransform;
            ResolveCanvasCamera();
        }

        private void OnDisable()
        {
            var wasHeld = _pointerId.HasValue;
            _pointerId = null;
            Value = Vector2.zero;
            _touchDeltaNormalizedAccum = Vector2.zero;
            _lostTouchTimer = 0f;
            _releasing = false;
            if (wasHeld)
                Released?.Invoke();

            ForceImmediateVisualReset(resetCenterToFixed: true);
        }

        private void Update() => Tick(Time.deltaTime);

        private void ResolveCanvasCamera()
        {
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _canvasCamera = _canvas.worldCamera;
            else
                _canvasCamera = null;
        }

        private void LateUpdate()
        {
            if (IsPressed || _releasing || area == null || knob == null)
                return;
            MoveRectCenterToAreaLocal(knob, GetIdleKnobCenterAreaLocal());
            if (baseRing != null)
                MoveRectCenterToAreaLocal(baseRing, GetIdleBaseCenterAreaLocal());
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled) return;
            CaptureDesignAnchorsFromLayout();
            if (!IsPressed && !_releasing)
                ResetCenterAnchoredFromDesign();
        }

        private void OnValidate()
        {
            radiusPixels = Mathf.Max(1f, radiusPixels);
            deadZone = Mathf.Clamp01(deadZone);
            edgePadding = Mathf.Max(0f, edgePadding);
            reboundDuration = Mathf.Max(0f, reboundDuration);
            responseExponent = Mathf.Max(1f, responseExponent);
            axisLockThreshold = Mathf.Clamp01(axisLockThreshold);
            axisLockStrength = Mathf.Clamp01(axisLockStrength);
            if (valueMode == JoystickValueMode.RimDigitalDirection && !returnCenterToDefaultOnRelease)
                returnCenterToDefaultOnRelease = true;
        }

        [ContextMenu("Capture design anchors (idle centers in area space)")]
        private void CaptureDesignAnchorsFromLayout()
        {
            _defaultCenterAnchored = Vector2.zero;
            if (area == null)
                return;
            if (knob != null)
                _knobRestCenterAreaLocal = GetRectCenterInAreaLocal(knob);
            if (baseRing != null)
                _baseRestCenterAreaLocal = GetRectCenterInAreaLocal(baseRing);
        }

        /// <summary>将逻辑中心设为默认布局中心（area 假定 pivot 在中心时为 Vector2.zero）。</summary>
        private void ResetCenterAnchoredFromDesign() => _centerAnchoredInArea = _defaultCenterAnchored;

        private void Start()
        {
            CaptureDesignAnchorsFromLayout();
            ResetCenterAnchoredFromDesign();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ResolveCanvasCamera();

            if (_pointerId.HasValue || area == null || knob == null)
                return;

            if (!TryLocalPoint(eventData, out var localInArea))
                return;

            _pointerId = eventData.pointerId;
            _releasing = false;
            _touchDeltaNormalizedAccum = Vector2.zero;
            _stickOffsetAnalogPx = Vector2.zero;

            if (IsRimMode())
            {
                var rimValue = EvaluateRim(localInArea, out var rimCenter);
                _centerAnchoredInArea = rimCenter;
                MoveRimVisuals(rimCenter);
                Value = rimValue;
            }
            else
            {
                _centerAnchoredInArea = centerMode == CenterMode.Dynamic
                    ? ClampCenter(localInArea)
                    : _defaultCenterAnchored;
                PresentAnalogVisual();
                Value = Vector2.zero;
            }

            ValueChanged?.Invoke(Value);
            Pressed?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_pointerId.HasValue || eventData.pointerId != _pointerId.Value || area == null || knob == null)
                return;

            if (!TryLocalPoint(eventData, out var localInArea))
                return;

            var scale = Mathf.Max(1e-6f, GetMovableInsetRadius());
            _touchDeltaNormalizedAccum += eventData.delta / scale;

            if (IsRimMode())
            {
                var rimValue = EvaluateRim(localInArea, out var rimCenter);
                _centerAnchoredInArea = rimCenter;
                MoveRimVisuals(rimCenter);
                Value = rimValue;
            }
            else
            {
                var maxR = Mathf.Max(1e-4f, radiusPixels);
                var deltaAnchored = localInArea - _centerAnchoredInArea;
                _stickOffsetAnalogPx = Vector2.ClampMagnitude(deltaAnchored, maxR);
                var normalized = _stickOffsetAnalogPx / maxR;
                normalized = ApplyAxisLock(normalized);
                normalized = ApplyResponseCurve(normalized);

                if (normalized.magnitude < deadZone)
                    normalized = Vector2.zero;

                Value = normalized;
                PresentAnalogVisual();
            }

            ValueChanged?.Invoke(Value);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pointerId.HasValue || eventData.pointerId != _pointerId.Value)
                return;
            ForceRelease();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // WebGL / 移动端偶尔漏发 Up；仍依赖拖拽与失联 Watchdog。
        }

        private void TickWatchdog(float deltaTime)
        {
            if (!_pointerId.HasValue)
            {
                _lostTouchTimer = 0f;
                return;
            }

            if (!ShouldWatchdogTouch())
                return;

            if (HasSystemPointer(_pointerId.Value))
            {
                _lostTouchTimer = 0f;
                return;
            }

            _lostTouchTimer += Mathf.Max(0f, deltaTime);
            if (_lostTouchTimer >= LostTouchReleaseDelay)
                ForceRelease();
        }

        private void TickRebound(float deltaTime)
        {
            if (!_releasing || knob == null || area == null)
                return;

            _reboundElapsed += Mathf.Max(0f, deltaTime);
            var t = reboundDuration <= 1e-5f ? 1f : Mathf.Clamp01(_reboundElapsed / reboundDuration);
            var e = 1f - (1f - t) * (1f - t);

            var knobPos = Vector2.Lerp(_reboundKnobStartAreaLocal, _reboundKnobTargetAreaLocal, e);
            MoveRectCenterToAreaLocal(knob, knobPos);
            if (baseRing != null)
            {
                var basePos = Vector2.Lerp(_reboundBaseStartAreaLocal, _reboundBaseTargetAreaLocal, e);
                MoveRectCenterToAreaLocal(baseRing, basePos);
            }

            if (t >= 1f - 1e-5f)
            {
                _releasing = false;
                MoveRectCenterToAreaLocal(knob, _reboundKnobTargetAreaLocal);
                if (baseRing != null)
                    MoveRectCenterToAreaLocal(baseRing, _reboundBaseTargetAreaLocal);
            }
        }

        private void ForceRelease()
        {
            if (!_pointerId.HasValue)
                return;

            _pointerId = null;
            _lostTouchTimer = 0f;
            Value = Vector2.zero;
            _touchDeltaNormalizedAccum = Vector2.zero;
            _stickOffsetAnalogPx = Vector2.zero;
            ValueChanged?.Invoke(Value);
            Released?.Invoke();

            if (ShouldResetCenterAfterRelease())
                _centerAnchoredInArea = _defaultCenterAnchored;

            BeginRebound();
        }

        private void ForceImmediateVisualReset(bool resetCenterToFixed)
        {
            _releasing = false;
            _stickOffsetAnalogPx = Vector2.zero;
            if (resetCenterToFixed)
                _centerAnchoredInArea = _defaultCenterAnchored;
            ApplyImmediateIdleLayout();
        }

        private void ApplyImmediateIdleLayout()
        {
            if (area == null)
                return;
            if (knob != null)
                MoveRectCenterToAreaLocal(knob, ComputeKnobReleaseTargetAreaLocal());
            if (baseRing != null)
                MoveRectCenterToAreaLocal(baseRing, ComputeBaseReleaseTargetAreaLocal());
        }

        private void BeginRebound()
        {
            if (knob == null || area == null)
                return;

            _reboundElapsed = 0f;
            _reboundKnobStartAreaLocal = GetRectCenterInAreaLocal(knob);
            _reboundKnobTargetAreaLocal = ComputeKnobReleaseTargetAreaLocal();

            if (baseRing != null)
            {
                _reboundBaseStartAreaLocal = GetRectCenterInAreaLocal(baseRing);
                _reboundBaseTargetAreaLocal = ComputeBaseReleaseTargetAreaLocal();
            }

            var knobNeedsTween = reboundDuration > 1e-5f ||
                                 Vector2.Distance(_reboundKnobStartAreaLocal, _reboundKnobTargetAreaLocal) > 0.5f;

            var baseNeedsTween = baseRing != null && reboundDuration > 1e-5f &&
                                 Vector2.Distance(_reboundBaseStartAreaLocal, _reboundBaseTargetAreaLocal) > 0.5f;

            if (!knobNeedsTween && !baseNeedsTween)
            {
                MoveRectCenterToAreaLocal(knob, _reboundKnobTargetAreaLocal);
                if (baseRing != null)
                    MoveRectCenterToAreaLocal(baseRing, _reboundBaseTargetAreaLocal);
                _releasing = false;
                return;
            }

            _releasing = true;

            if (reboundDuration <= 1e-5f)
            {
                MoveRectCenterToAreaLocal(knob, _reboundKnobTargetAreaLocal);
                if (baseRing != null)
                    MoveRectCenterToAreaLocal(baseRing, _reboundBaseTargetAreaLocal);
                _releasing = false;
            }
        }

        private float GetMovableInsetRadius()
        {
            var r = area.rect;
            var inset = Mathf.Max(0f, edgePadding);
            var halfMin = Mathf.Min(Mathf.Abs(r.width), Mathf.Abs(r.height)) * 0.5f - inset;
            var byRect = Mathf.Max(halfMin, 4f);
            if (radiusPixels <= 1e-3f)
                return Mathf.Max(4f, byRect);
            return Mathf.Max(4f, Mathf.Min(byRect, radiusPixels));
        }

        private Vector2 EvaluateRim(Vector2 fingerLocal, out Vector2 rimCenterAreaLocal)
        {
            var origin = _defaultCenterAnchored;
            var rimR = GetMovableInsetRadius();
            var fromO = fingerLocal - origin;
            var dzPx = Mathf.Max(2f, rimR * Mathf.Clamp01(deadZone));
            if (fromO.magnitude <= dzPx)
            {
                rimCenterAreaLocal = origin;
                return Vector2.zero;
            }

            var dir = fromO.normalized;
            rimCenterAreaLocal = origin + dir * rimR;
            return dir;
        }

        private void MoveRimVisuals(Vector2 rimCenterAreaLocal)
        {
            if (baseRing != null)
                MoveRectCenterToAreaLocal(baseRing, rimCenterAreaLocal);
            MoveRectCenterToAreaLocal(knob, rimCenterAreaLocal);
        }

        private void PresentAnalogVisual()
        {
            if (baseRing != null)
                MoveRectCenterToAreaLocal(baseRing, ComputeBaseCenterAreaLocalForAnalog());
            MoveRectCenterToAreaLocal(knob, _centerAnchoredInArea + _stickOffsetAnalogPx);
        }

        private Vector2 ComputeBaseCenterAreaLocalForAnalog()
        {
            if (centerMode == CenterMode.Dynamic)
                return _centerAnchoredInArea;
            return _baseRestCenterAreaLocal;
        }

        private Vector2 ComputeKnobReleaseTargetAreaLocal()
        {
            if (!IsRimMode() && centerMode == CenterMode.Dynamic && !ShouldResetCenterAfterRelease())
                return _centerAnchoredInArea;
            return _knobRestCenterAreaLocal;
        }

        private Vector2 ComputeBaseReleaseTargetAreaLocal()
        {
            if (IsRimMode())
                return _baseRestCenterAreaLocal;
            if (centerMode == CenterMode.Fixed)
                return _baseRestCenterAreaLocal;
            if (ShouldResetCenterAfterRelease())
                return _baseRestCenterAreaLocal;
            return _centerAnchoredInArea;
        }

        private Vector2 GetIdleKnobCenterAreaLocal() => ComputeKnobReleaseTargetAreaLocal();

        private Vector2 GetIdleBaseCenterAreaLocal() => ComputeBaseReleaseTargetAreaLocal();

        private Vector2 GetRectCenterInAreaLocal(RectTransform rt)
        {
            if (rt == null || area == null)
                return Vector2.zero;

            var w = rt.TransformPoint(rt.rect.center);
            var inv = area.InverseTransformPoint(w);
            return new Vector2(inv.x, inv.y);
        }

        private void MoveRectCenterToAreaLocal(RectTransform rt, Vector2 centerInAreaLocal)
        {
            if (rt == null || area == null)
                return;

            var wantWorld = area.TransformPoint(new Vector3(centerInAreaLocal.x, centerInAreaLocal.y, 0f));
            var haveWorld = rt.TransformPoint(rt.rect.center);
            rt.position += wantWorld - haveWorld;
        }

        private bool TryLocalPoint(PointerEventData eventData, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                area, eventData.position, _canvasCamera, out localPoint);
        }

        private Vector2 ClampCenter(Vector2 proposed)
        {
            if (!edgeSnap)
                return proposed;
            var r = area.rect;
            var rp = Mathf.Max(0f, radiusPixels + edgePadding);
            var halfW = Mathf.Max(0f, r.width * 0.5f - rp);
            var halfH = Mathf.Max(0f, r.height * 0.5f - rp);
            proposed.x = Mathf.Clamp(proposed.x, -halfW, halfW);
            proposed.y = Mathf.Clamp(proposed.y, -halfH, halfH);
            return proposed;
        }

        private Vector2 ApplyAxisLock(Vector2 v)
        {
            if (!axisLockAssist || v.sqrMagnitude < 1e-8f)
                return v;

            var ax = Mathf.Abs(v.x);
            var ay = Mathf.Abs(v.y);
            var diff = Mathf.Abs(ax - ay);
            if (diff < axisLockThreshold)
                return v;

            if (ax > ay)
                v.y *= 1f - axisLockStrength;
            else
                v.x *= 1f - axisLockStrength;

            return Vector2.ClampMagnitude(v, 1f);
        }

        private Vector2 ApplyResponseCurve(Vector2 v)
        {
            if (responseCurve == ResponseCurveMode.Linear)
                return v;

            var mag = v.magnitude;
            if (mag <= 1e-5f)
                return Vector2.zero;

            var curvedMag = Mathf.Pow(Mathf.Clamp01(mag), responseExponent);
            return v.normalized * curvedMag;
        }

        private static bool ShouldWatchdogTouch()
        {
#if UNITY_EDITOR
            return false;
#else
            if (Application.isMobilePlatform) return true;
            return Application.platform == RuntimePlatform.WebGLPlayer;
#endif
        }

        private static bool HasSystemPointer(int pointerId)
        {
            // 鼠标等在 EventSystem 中常用负 id；需与 FingerId 路径区分。
            if (pointerId < 0)
                return Input.GetMouseButton(0);

            if (Input.touchSupported)
            {
                for (var i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.fingerId == pointerId)
                        return true;
                }
            }

            return false;
        }
    }
}
