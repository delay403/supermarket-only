using Game.Features.Treadmill;
using UnityEngine;

namespace Game.UI.ViewModels
{
    public sealed class TreadmillResultViewModel
    {
        public TreadmillTier Tier;
        public float DurationSeconds;
        public float SessionRunningGain;
        public float SessionInstantGain;
        public float SessionClimbSpeedGain;
        public float PermanentBonusAtSessionStart;
        public float TotalPermanentClimbSpeedBonus;
        /// <summary>结算时刻：配置基础爬塔速度 + 跑步机永久加成（不含地图修正与装备乘区）。</summary>
        public float CoreClimbSpeed;

        public string TierLabel => TreadmillService.GetTierDisplayName(Tier);

        public float PreviousPermanentBonus => PermanentBonusAtSessionStart;

        public string FormattedDuration
        {
            get
            {
                var mins = (int)(DurationSeconds / 60f);
                var secs = (int)(DurationSeconds % 60f);
                return $"{mins:D2}:{secs:D2}";
            }
        }

        /// <summary>按本次实际获得的 tick 数计算箭头数量（至少 1，至多 <paramref name="maxCount"/>）。</summary>
        public int GetGainArrowCount(int maxCount = 8)
        {
            if (SessionClimbSpeedGain <= 0.0001f)
                return 0;

            var perTick = TreadmillService.GetBonusPerInterval(Tier);
            if (perTick <= 0.0001f)
                return 1;

            var count = Mathf.RoundToInt(SessionClimbSpeedGain / perTick);
            return Mathf.Clamp(Mathf.Max(1, count), 1, Mathf.Max(1, maxCount));
        }

        public string ToBodyText()
        {
            var instantLine = SessionInstantGain > 0.0001f
                ? $"\n  （跑步 +{SessionRunningGain:0.###} / 立即领取 +{SessionInstantGain:0.###}）"
                : string.Empty;

            return
                $"档位：{TierLabel}\n" +
                $"本次时长：{FormattedDuration}\n" +
                $"本次爬塔速度提升：+{SessionClimbSpeedGain:0.###} m/s{instantLine}\n" +
                $"跑步机永久加成：+{TotalPermanentClimbSpeedBonus:0.###} m/s\n" +
                $"当前爬塔速度：{CoreClimbSpeed:0.###} m/s";
        }
    }
}
