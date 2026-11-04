using System;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>物品移动：手持区用本地坐标，货架/地面用世界坐标。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketItemMotion : MonoBehaviour
    {
        enum MoveSpace
        {
            World = 0,
            Local = 1,
        }

        Func<Vector3> _targetProvider;
        Action _onArrived;
        float _speed;
        float _maxDuration;
        float _elapsed;
        float _arcHeight;
        Vector3 _startPosition;
        MoveSpace _space = MoveSpace.World;
        bool _active;
        bool _useArc;

        public bool IsMoving => _active;

        public void BeginMoveWorld(
            Func<Vector3> worldTargetProvider,
            float speed,
            Action onArrived,
            float maxDurationSeconds = 0f,
            float arcHeight = 0f)
        {
            _space = MoveSpace.World;
            BeginMoveInternal(worldTargetProvider, speed, onArrived, maxDurationSeconds, arcHeight);
        }

        public void BeginMoveLocal(
            Func<Vector3> localTargetProvider,
            float speed,
            Action onArrived,
            float maxDurationSeconds = 0f,
            float arcHeight = 0f)
        {
            _space = MoveSpace.Local;
            BeginMoveInternal(localTargetProvider, speed, onArrived, maxDurationSeconds, arcHeight);
        }

        void BeginMoveInternal(
            Func<Vector3> targetProvider,
            float speed,
            Action onArrived,
            float maxDurationSeconds,
            float arcHeight)
        {
            _targetProvider = targetProvider;
            _speed = Mathf.Max(0.01f, speed);
            _onArrived = onArrived;
            _maxDuration = Mathf.Max(0f, maxDurationSeconds);
            _arcHeight = Mathf.Max(0f, arcHeight);
            _useArc = _arcHeight > 0f && _maxDuration > 0f;
            _elapsed = 0f;
            _startPosition = _space == MoveSpace.Local ? transform.localPosition : transform.position;
            _active = true;
            enabled = true;
        }

        public void Cancel()
        {
            _active = false;
            _useArc = false;
            _targetProvider = null;
            _onArrived = null;
            enabled = false;
        }

        void Update()
        {
            if (!_active || _targetProvider == null)
                return;

            var handleItem = GetComponent<SupermarketItem>();
            if (handleItem != null && handleItem.State != SupermarketItemState.Moving)
            {
                Cancel();
                return;
            }

            _elapsed += Time.deltaTime;
            var target = _targetProvider.Invoke();

            if (_useArc)
            {
                UpdateArcMove(target);
                return;
            }

            if (_maxDuration > 0f && _elapsed >= _maxDuration)
            {
                ApplyPosition(target);
                Finish();
                return;
            }

            var current = _space == MoveSpace.Local ? transform.localPosition : transform.position;
            var toTarget = target - current;
            var distance = toTarget.magnitude;
            if (distance <= 0.001f)
            {
                ApplyPosition(target);
                Finish();
                return;
            }

            var step = _speed * Time.deltaTime;
            if (step >= distance)
            {
                ApplyPosition(target);
                Finish();
                return;
            }

            var next = current + toTarget / distance * step;
            ApplyPosition(next);
        }

        void UpdateArcMove(Vector3 target)
        {
            var t = _maxDuration > 0f
                ? Mathf.Clamp01(_elapsed / _maxDuration)
                : 1f;

            var pos = Vector3.Lerp(_startPosition, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * _arcHeight;
            ApplyPosition(pos);

            if (t >= 1f || (_maxDuration > 0f && _elapsed >= _maxDuration))
            {
                ApplyPosition(target);
                Finish();
            }
        }

        void ApplyPosition(Vector3 value)
        {
            if (_space == MoveSpace.Local)
                transform.localPosition = value;
            else
                transform.position = value;
        }

        void Finish()
        {
            _active = false;
            _useArc = false;
            var callback = _onArrived;
            _onArrived = null;
            _targetProvider = null;
            enabled = false;
            callback?.Invoke();
        }

        void OnDisable()
        {
            _active = false;
            _useArc = false;
            _targetProvider = null;
            _onArrived = null;
        }
    }
}
