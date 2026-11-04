using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>
    /// 楼层结算区：参与者到达后记录层数；玩家额外在本局结算中累计金币。
    /// 层号根据判定区世界高度自动计算（无需手填 floorIndex）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormFloorSettlementZone : MonoBehaviour
    {
        [SerializeField] private long coinReward = 100;
        [SerializeField] private bool triggerOncePerRun = true;

        readonly System.Collections.Generic.HashSet<int> _triggeredParticipantIds = new System.Collections.Generic.HashSet<int>();

        public int FloorIndex => ResolveFloorIndex();

        public long CoinReward => coinReward;

        public float SampleWorldHeight => WormFloorHeightIndex.SampleZoneWorldHeight(this);

        public bool ContainsWorldPoint(Vector3 worldPos)
        {
            var col = GetComponent<Collider>();
            if (col == null)
                return false;

            var closest = col.ClosestPoint(worldPos);
            return (closest - worldPos).sqrMagnitude < 0.05f;
        }

        void OnEnable() => WormDodgeDirector.RunReset += OnRunReset;

        void OnDisable() => WormDodgeDirector.RunReset -= OnRunReset;

        void OnRunReset() => _triggeredParticipantIds.Clear();

        void OnTriggerEnter(Collider other)
        {
            if (!TryGetParticipant(other, out var participant))
                return;

            var participantId = participant.GetInstanceID();
            if (triggerOncePerRun && _triggeredParticipantIds.Contains(participantId))
                return;

            if (triggerOncePerRun)
                _triggeredParticipantIds.Add(participantId);

            WormDodgeDirector.Instance?.NotifyParticipantReachedFloor(participant, this);
        }

        int ResolveFloorIndex()
        {
            var height = SampleWorldHeight;
            var director = WormDodgeDirector.Instance;
            if (director != null)
                return director.ResolveFloorIndexFromWorldHeight(height);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                return WormFloorHeightIndex.ResolveFloorIndexInEditor(this);
#endif
            return 1;
        }

        static bool TryGetParticipant(Collider other, out WormDodgeParticipant participant)
        {
            participant = other.GetComponent<WormDodgeParticipant>();
            if (participant == null)
                participant = other.GetComponentInParent<WormDodgeParticipant>();
            if (participant == null)
                participant = other.GetComponentInChildren<WormDodgeParticipant>();

            return participant != null;
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
