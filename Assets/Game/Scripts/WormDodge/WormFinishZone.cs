using UnityEngine;

namespace Game.WormDodge
{
    /// <summary>终点区：玩家到达后本局胜利。</summary>
    [DisallowMultipleComponent]
    public sealed class WormFinishZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (!TryGetPlayer(other, out var participant))
                return;

            WormDodgeDirector.Instance?.NotifyPlayerReachedFinish(participant);
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
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }
#endif
    }
}
