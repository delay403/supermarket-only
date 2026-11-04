using Game.WormDodge;
using UnityEngine;

namespace Game.EscapeSnake
{
    /// <summary>
    /// 多节虫捕获触发器：挂在虫体 trigger 碰撞体上，碰到玩家或 AI 时通知 <see cref="WormChainMotor"/> 捕获。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WormCatchTrigger : MonoBehaviour
    {
        [SerializeField] private WormChainMotor motor;

        void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;

            if (motor == null)
                motor = GetComponentInParent<WormChainMotor>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (motor == null || !motor.IsPlaying)
                return;

            var director = WormDodgeDirector.Instance;
            if (director != null && !director.IsRunActive)
                return;

            if (!TryResolveTarget(other, out var target, out var participant))
                return;

            if (participant != null && participant.IsInSafeZone)
                return;

            motor.NotifyTriggerCatch(target);
        }

        static bool TryResolveTarget(
            Collider other,
            out WormChaseTarget target,
            out WormDodgeParticipant participant)
        {
            target = other.GetComponent<WormChaseTarget>();
            if (target == null)
                target = other.GetComponentInParent<WormChaseTarget>();

            participant = other.GetComponent<WormDodgeParticipant>();
            if (participant == null)
                participant = other.GetComponentInParent<WormDodgeParticipant>();

            return target != null;
        }
    }
}
