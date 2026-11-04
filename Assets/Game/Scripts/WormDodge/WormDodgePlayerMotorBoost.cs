using Game.Collectibles.Stats;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// 将装备聚合的躲避虫子移速加成写入 <see cref="PlayerMotor"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgePlayerMotorBoost : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private WormDodgeStatsAggregator statsAggregator;

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();
            if (statsAggregator == null)
                statsAggregator = GetComponent<WormDodgeStatsAggregator>();
        }

        void OnEnable()
        {
            WormDodgeStatsAggregator.Changed += ApplyStats;
            ApplyStats();
        }

        void OnDisable()
        {
            WormDodgeStatsAggregator.Changed -= ApplyStats;
            if (motor != null)
                motor.SetWormDodgeMoveSpeedBonus(0f, 0f);
        }

        public void ApplyStats()
        {
            if (!GameProductSettings.IsWormDodgeContentEnabled || motor == null)
                return;

            var stats = statsAggregator != null ? statsAggregator.Current : WormDodgeStatsAggregator.Compute();
            motor.SetWormDodgeMoveSpeedBonus(stats.totalMoveSpeedAdd, stats.totalMoveSpeedMul);
        }
    }
}
