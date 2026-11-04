using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// 楼层奖励弹窗触发区：到达特定楼层时弹出可领取奖励（与普通楼层标记区分离）。
    /// 层号根据世界高度自动计算。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeFloorRewardZone : MonoBehaviour
    {
        [SerializeField] private long coinReward = 500;
        [SerializeField] private bool triggerOncePerRun = true;

        bool _triggeredThisRun;

        public int FloorIndex => ResolveFloorIndex();

        public long CoinReward => coinReward;

        public float SampleWorldHeight => WormFloorHeightIndex.SampleZoneWorldHeight(this);

        void OnEnable() => WormDodgeDirector.RunReset += OnRunReset;

        void OnDisable() => WormDodgeDirector.RunReset -= OnRunReset;

        void OnRunReset() => _triggeredThisRun = false;

        void OnTriggerEnter(Collider other)
        {
            if (!TryGetPlayer(other, out _))
                return;

            if (triggerOncePerRun && _triggeredThisRun)
                return;

            _triggeredThisRun = true;
            WormDodgeDirector.Instance?.NotifyFloorRewardPopup(this);
        }

        int ResolveFloorIndex()
        {
            var height = SampleWorldHeight;
            var director = WormDodgeDirector.Instance;
            if (director != null)
                return director.ResolveFloorIndexFromWorldHeight(height);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                return WormFloorHeightIndex.ResolveRewardFloorIndexInEditor(this);
#endif
            return 1;
        }

        static bool TryGetPlayer(Collider other, out WormDodgeParticipant participant)
        {
            participant = other.GetComponent<WormDodgeParticipant>();
            if (participant == null)
                participant = other.GetComponentInParent<WormDodgeParticipant>();

            return participant != null && participant.IsPlayer;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (coinReward < 0L)
                coinReward = 0L;
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }
#endif
    }
}
