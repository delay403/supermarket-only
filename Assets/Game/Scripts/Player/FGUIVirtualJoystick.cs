using System;
using FairyGUI;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Reusable FairyGUI virtual joystick driver.
    /// Input range is normalized to [-1, 1] on both axes.
    /// </summary>
    public sealed class FGUIVirtualJoystick : IDisposable
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

        private readonly GObject _touchArea;
        private readonly GObject _baseVisual;
        private readonly GObject _knobVisual;
        private readonly float _radius;
        private readonly float _deadZone;
        private readonly CenterMode _centerMode;
        private readonly bool _edgeSnap;
        private readonly float _edgePadding;
        private readonly float _reboundDuration;
        private readonly bool _returnCenterToDefaultOnRelease;
        private readonly bool _hideWhenIdle;
        private readonly ResponseCurveMode _responseCurveMode;
        private readonly float _responseExponent;
        private readonly bool _axisLockAssist;
        private readonly float _axisLockThreshold;
        private readonly float _axisLockStrength;

        private readonly float _baseStartX;
        private readonly float _baseStartY;
        private readonly float _knobStartX;
        private readonly float _knobStartY;
        private readonly Vector2 _defaultCenterTouchLocal;

        private int _touchId = -1;
        private Vector2 _centerTouchLocal;
        private Vector2 _lastTouchLocal;
        private Vector2 _touchDeltaNormalizedAccum;
        private GTweener _baseTween;
        private GTweener _knobTween;
        private float _lostTouchTimer;
        private const float LostTouchReleaseDelay = 0.15f;

        public Vector2 Value { get; private set; }
        public bool IsPressed => _touchId != -1;

        public event Action<Vector2> ValueChanged;
        public event Action Pressed;
        public event Action Released;

        /// <summary>
        /// Call once per frame to guard against missing touch-end events
        /// (common on WebGL mini-games when focus changes or touch is cancelled).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_touchId == -1)
            {
                _lostTouchTimer = 0f;
                return;
            }

            if (!ShouldWatchdogTouch())
                return;

            if (HasSystemTouch(_touchId))
            {
                _lostTouchTimer = 0f;
                return;
            }

            _lostTouchTimer += Mathf.Max(0f, deltaTime);
            if (_lostTouchTimer >= LostTouchReleaseDelay)
            {
                ForceRelease();
            }
        }

        /// <summary>
        /// Raw touch movement since last consume, normalized by joystick radius.
        /// Use ConsumeTouchDeltaNormalized() to avoid using stale values.
        /// </summary>
        public Vector2 TouchDeltaNormalized => _touchDeltaNormalizedAccum;

        public Vector2 ConsumeTouchDeltaNormalized()
        {
            var v = _touchDeltaNormalizedAccum;
            _touchDeltaNormalizedAccum = Vector2.zero;
            return v;
        }

        public FGUIVirtualJoystick(
            GObject touchArea,
            GObject baseVisual,
            GObject knobVisual,
            float radius,
            float deadZone = 0.1f,
            CenterMode centerMode = CenterMode.Dynamic,
            bool edgeSnap = true,
            float edgePadding = 6f,
            float reboundDuration = 0.12f,
            bool returnCenterToDefaultOnRelease = false,
            ResponseCurveMode responseCurveMode = ResponseCurveMode.Linear,
            float responseExponent = 1.75f,
            bool axisLockAssist = false,
            float axisLockThreshold = 0.2f,
            float axisLockStrength = 0.85f,
            bool hideWhenIdle = false)
        {
            _touchArea = touchArea ?? throw new ArgumentNullException(nameof(touchArea));
            _baseVisual = baseVisual ?? throw new ArgumentNullException(nameof(baseVisual));
            _knobVisual = knobVisual ?? throw new ArgumentNullException(nameof(knobVisual));
            _radius = Mathf.Max(1f, radius);
            _deadZone = Mathf.Clamp01(deadZone);
            _centerMode = centerMode;
            _edgeSnap = edgeSnap;
            _edgePadding = Mathf.Max(0f, edgePadding);
            _reboundDuration = Mathf.Max(0f, reboundDuration);
            _returnCenterToDefaultOnRelease = returnCenterToDefaultOnRelease;
            _responseCurveMode = responseCurveMode;
            _responseExponent = Mathf.Max(1f, responseExponent);
            _axisLockAssist = axisLockAssist;
            _axisLockThreshold = Mathf.Clamp01(axisLockThreshold);
            _axisLockStrength = Mathf.Clamp01(axisLockStrength);
            _hideWhenIdle = hideWhenIdle;

            _baseStartX = _baseVisual.x;
            _baseStartY = _baseVisual.y;
            _knobStartX = _knobVisual.x;
            _knobStartY = _knobVisual.y;
            _centerTouchLocal = GetDefaultCenterInTouchLocal();
            _defaultCenterTouchLocal = _centerTouchLocal;

            _touchArea.onTouchBegin.Add(OnTouchBegin);
            _touchArea.onTouchMove.Add(OnTouchMove);
            _touchArea.onTouchEnd.Add(OnTouchEnd);

            if (_hideWhenIdle)
            {
                _baseVisual.visible = false;
                _knobVisual.visible = false;
            }
        }

        public void Dispose()
        {
            KillTweens();
            _touchArea.onTouchBegin.Remove(OnTouchBegin);
            _touchArea.onTouchMove.Remove(OnTouchMove);
            _touchArea.onTouchEnd.Remove(OnTouchEnd);
        }

        public void ResetToDefaultVisual()
        {
            KillTweens();
            _touchId = -1;
            Value = Vector2.zero;
            _baseVisual.SetXY(_baseStartX, _baseStartY);
            _knobVisual.SetXY(_knobStartX, _knobStartY);
            _centerTouchLocal = _defaultCenterTouchLocal;
            ValueChanged?.Invoke(Value);
            if (_hideWhenIdle)
            {
                _baseVisual.visible = false;
                _knobVisual.visible = false;
            }
        }

        private void OnTouchBegin(EventContext context)
        {
            if (_touchId != -1) return;
            KillTweens();

            var evt = (InputEvent)context.data;
            _touchId = evt.touchId;

            var touchLocal = _touchArea.GlobalToLocal(new Vector2(evt.x, evt.y));
            _lastTouchLocal = touchLocal;
            _touchDeltaNormalizedAccum = Vector2.zero;
            var targetCenter = _centerMode == CenterMode.Dynamic ? touchLocal : _defaultCenterTouchLocal;
            if (_edgeSnap)
                targetCenter = ClampCenterToArea(targetCenter);

            _centerTouchLocal = targetCenter;
            if (_centerMode == CenterMode.Dynamic)
                SetVisualCenter(_baseVisual, _centerTouchLocal);
            SetVisualCenter(_knobVisual, _centerTouchLocal);

            if (_hideWhenIdle)
            {
                _baseVisual.visible = true;
                _knobVisual.visible = true;
            }

            Value = Vector2.zero;
            ValueChanged?.Invoke(Value);
            Pressed?.Invoke();
            context.CaptureTouch();
        }

        private void OnTouchMove(EventContext context)
        {
            var evt = (InputEvent)context.data;
            if (_touchId == -1 || evt.touchId != _touchId) return;

            var touchLocal = _touchArea.GlobalToLocal(new Vector2(evt.x, evt.y));
            var touchDelta = touchLocal - _lastTouchLocal;
            _touchDeltaNormalizedAccum += touchDelta / Mathf.Max(1e-6f, _radius);
            _lastTouchLocal = touchLocal;

            var delta = touchLocal - _centerTouchLocal;
            var clamped = Vector2.ClampMagnitude(delta, _radius);
            var normalized = clamped / _radius;
            normalized = ApplyAxisLock(normalized);
            normalized = ApplyResponseCurve(normalized);

            if (normalized.magnitude < _deadZone)
                normalized = Vector2.zero;

            Value = normalized;

            var knobCenterTouchLocal = _centerTouchLocal + clamped;
            SetVisualCenter(_knobVisual, knobCenterTouchLocal);
            ValueChanged?.Invoke(Value);
        }

        private Vector2 ApplyAxisLock(Vector2 v)
        {
            if (!_axisLockAssist || v == Vector2.zero)
                return v;

            var ax = Mathf.Abs(v.x);
            var ay = Mathf.Abs(v.y);
            var diff = Mathf.Abs(ax - ay);
            if (diff < _axisLockThreshold)
                return v;

            if (ax > ay)
                v.y *= 1f - _axisLockStrength;
            else
                v.x *= 1f - _axisLockStrength;

            return Vector2.ClampMagnitude(v, 1f);
        }

        private Vector2 ApplyResponseCurve(Vector2 v)
        {
            if (_responseCurveMode == ResponseCurveMode.Linear)
                return v;

            var mag = v.magnitude;
            if (mag <= 1e-5f)
                return Vector2.zero;

            var curvedMag = Mathf.Pow(Mathf.Clamp01(mag), _responseExponent);
            return v.normalized * curvedMag;
        }

        private void OnTouchEnd(EventContext context)
        {
            var evt = (InputEvent)context.data;
            if (_touchId == -1 || evt.touchId != _touchId) return;

            ForceRelease();
        }

        private void ForceRelease()
        {
            if (_touchId == -1) return;
            _touchId = -1;
            _lostTouchTimer = 0f;
            Value = Vector2.zero;
            _touchDeltaNormalizedAccum = Vector2.zero;
            ValueChanged?.Invoke(Value);
            Released?.Invoke();
            PlayReleaseAnimation();
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

        private static bool HasSystemTouch(int touchId)
        {
            // Unity touch fingerId is generally aligned with FairyGUI InputEvent.touchId on touch platforms.
            // If touch is cancelled/lost and FGUI doesn't dispatch TouchEnd, this lets us self-heal.
            if (!Input.touchSupported)
                return false;
            var count = Input.touchCount;
            if (count <= 0)
                return false;
            for (var i = 0; i < count; i++)
            {
                var t = Input.GetTouch(i);
                if (t.fingerId == touchId)
                    return true;
            }
            return false;
        }

        private Vector2 GetDefaultCenterInTouchLocal()
        {
            var baseCenterLocal = new Vector2(_baseVisual.width * 0.5f, _baseVisual.height * 0.5f);
            var baseCenterGlobal = _baseVisual.LocalToGlobal(baseCenterLocal);
            return _touchArea.GlobalToLocal(baseCenterGlobal);
        }

        private void PlayReleaseAnimation()
        {
            KillTweens();

            var targetCenterTouchLocal = _centerTouchLocal;
            if (_centerMode == CenterMode.Dynamic && _returnCenterToDefaultOnRelease)
            {
                _centerTouchLocal = _defaultCenterTouchLocal;
                targetCenterTouchLocal = _defaultCenterTouchLocal;
            }

            var knobTarget = ToVisualTopLeftInParent(_knobVisual, targetCenterTouchLocal);
            var baseTarget = ToVisualTopLeftInParent(_baseVisual, targetCenterTouchLocal);

            if (_reboundDuration <= 0f)
            {
                _knobVisual.SetXY(knobTarget.x, knobTarget.y);
                if (_centerMode == CenterMode.Dynamic && _returnCenterToDefaultOnRelease)
                    _baseVisual.SetXY(baseTarget.x, baseTarget.y);
                HideIfNeeded();
                return;
            }

            _knobTween = _knobVisual.TweenMove(knobTarget, _reboundDuration).SetEase(EaseType.QuadOut).OnComplete(() =>
            {
                _knobTween = null;
                HideIfNeeded();
            });

            if (_centerMode == CenterMode.Dynamic && _returnCenterToDefaultOnRelease)
                _baseTween = _baseVisual.TweenMove(baseTarget, _reboundDuration).SetEase(EaseType.QuadOut).OnComplete(() => _baseTween = null);
        }

        private Vector2 ClampCenterToArea(Vector2 center)
        {
            var minX = _radius + _edgePadding;
            var maxX = Mathf.Max(minX, _touchArea.width - _radius - _edgePadding);
            var minY = _radius + _edgePadding;
            var maxY = Mathf.Max(minY, _touchArea.height - _radius - _edgePadding);

            center.x = Mathf.Clamp(center.x, minX, maxX);
            center.y = Mathf.Clamp(center.y, minY, maxY);
            return center;
        }

        private void SetVisualCenter(GObject visual, Vector2 centerTouchLocal)
        {
            var topLeft = ToVisualTopLeftInParent(visual, centerTouchLocal);
            visual.SetXY(topLeft.x, topLeft.y);
        }

        private Vector2 ToVisualTopLeftInParent(GObject visual, Vector2 centerTouchLocal)
        {
            var centerGlobal = _touchArea.LocalToGlobal(centerTouchLocal);
            var centerInParent = visual.parent != null
                ? visual.parent.GlobalToLocal(centerGlobal)
                : centerGlobal;
            return new Vector2(centerInParent.x - visual.width * 0.5f, centerInParent.y - visual.height * 0.5f);
        }

        private void HideIfNeeded()
        {
            if (!_hideWhenIdle || _touchId != -1) return;
            _baseVisual.visible = false;
            _knobVisual.visible = false;
        }

        private void KillTweens()
        {
            _baseTween?.Kill();
            _baseTween = null;
            _knobTween?.Kill();
            _knobTween = null;
        }
    }
}
