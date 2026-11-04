using System;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>单次燃料经验液滴动效（由 <see cref="FuelProducerDropletEmitter"/> 池化复用）。</summary>
    public sealed class FuelProductionDroplet : MonoBehaviour
    {
        Vector3 _target;
        float _speed;
        Action _onArrived;
        Action<FuelProductionDroplet> _onFinished;

        public void LaunchToTarget(
            Vector3 worldStart,
            Vector3 worldTarget,
            float speed,
            Action onArrived,
            Action<FuelProductionDroplet> onFinished)
        {
            transform.position = worldStart;
            _target = worldTarget;
            _speed = Mathf.Max(0.01f, speed);
            _onArrived = onArrived;
            _onFinished = onFinished;
            gameObject.SetActive(true);
        }

        void Update()
        {
            var toTarget = _target - transform.position;
            var distance = toTarget.magnitude;
            if (distance <= 0.001f)
            {
                transform.position = _target;
                Finish();
                return;
            }

            var step = _speed * Time.deltaTime;
            if (step >= distance)
            {
                transform.position = _target;
                Finish();
                return;
            }

            transform.position += toTarget / distance * step;
        }

        void Finish()
        {
            _onArrived?.Invoke();
            _onArrived = null;
            gameObject.SetActive(false);
            _onFinished?.Invoke(this);
            _onFinished = null;
        }

        void OnDisable()
        {
            _onArrived = null;
            _onFinished = null;
        }
    }
}
