using UnityEngine;
using Game.Core.Config;

namespace Game.Tower
{
    public sealed class Stamina : MonoBehaviour
    {
        [SerializeField] private float max = 10f;
        [SerializeField] private float current = 10f;
        [SerializeField] private float drainPerSecond = 1f;

        public float Max => max;
        public float Current => current;
        public float Current01 => max <= 0.0001f ? 0f : Mathf.Clamp01(current / max);

        private void Awake()
        {
            var bal = ConfigRegistry.Balance;
            if (bal == null) return;
            max = bal.staminaMax;
            current = Mathf.Min(current, max);
            drainPerSecond = bal.staminaDrainPerSecond;
        }

        public void ResetFull()
        {
            current = max;
        }

        /// <summary>仍在体力区间（>0）时为 true；耗尽后为 false，但允许攀爬逻辑改用低速档。</summary>
        public bool HasReserve => current > 0.0001f;

        /// <summary>攀爬中每帧调用：有体力时消耗；已耗尽则不再扣减。</summary>
        public void TickDrainWhileMoving(bool isMoving)
        {
            if (!isMoving || current <= 0f)
                return;
            current -= drainPerSecond * Time.deltaTime;
            if (current < 0f)
                current = 0f;
        }

        public bool ConsumeIfMoving(bool isMoving)
        {
            TickDrainWhileMoving(isMoving);
            return HasReserve;
        }
    }
}

