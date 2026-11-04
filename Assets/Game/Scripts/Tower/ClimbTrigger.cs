using UnityEngine;

namespace Game.Tower
{
    [RequireComponent(typeof(Collider))]
    public sealed class ClimbTrigger : MonoBehaviour
    {
        [Header("Filter")]
        [SerializeField] private LayerMask playerMask = ~0;

        [Header("Behavior")]
        [SerializeField] private bool autoExitOnLeave = true;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerMask) == 0)
                return;

            var climb = other.GetComponentInParent<ClimbController>();
            if (climb != null) climb.EnterClimb();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!autoExitOnLeave) return;
            if (((1 << other.gameObject.layer) & playerMask) == 0)
                return;

            var climb = other.GetComponentInParent<ClimbController>();
            if (climb != null) climb.ExitClimb();
        }
    }
}

