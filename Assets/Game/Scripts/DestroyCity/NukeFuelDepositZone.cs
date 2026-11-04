using Game.Interaction;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>核弹发射器燃料放置区：由独立 <see cref="WorldInteractionSlot"/> 引用后，通过 HUD 交互按钮灌注。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class NukeFuelDepositZone : MonoBehaviour, IWorldInteractionZoneHandler
    {
        [SerializeField] private float depositCooldownSeconds = 0.35f;
        [Tooltip("为 true 时仅显示交互按钮后手动灌注；为 false 时保留进入/停留自动灌注（旧逻辑）。")]
        [SerializeField] private bool useInteractionButton = true;
        [Tooltip("为 true 时站在区域内也会尝试灌注（进入时尚未拿起燃料的情况）。仅 useInteractionButton=false 时生效。")]
        [SerializeField] private bool depositWhileInside = true;
        [SerializeField] private int interactionPriority = 25;

        NukeLauncherController _launcher;
        float _nextDepositTime;

        public int InteractionPriority => interactionPriority;

        public void Bind(NukeLauncherController launcher) => _launcher = launcher;

        void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (useInteractionButton)
                return;

            TryDeposit(other);
        }

        void OnTriggerStay(Collider other)
        {
            if (useInteractionButton || !depositWhileInside)
                return;

            TryDeposit(other);
        }

        public bool CanInteract(Transform interactorRoot)
        {
            if (_launcher == null || interactorRoot == null)
                return false;
            if (Time.unscaledTime < _nextDepositTime)
                return false;

            var carry = interactorRoot.GetComponentInParent<PlayerFuelCarryController>();
            return carry != null && carry.IsCarrying;
        }

        public void Interact(Transform interactorRoot)
        {
            if (_launcher == null || interactorRoot == null)
                return;
            if (Time.unscaledTime < _nextDepositTime)
                return;

            var carry = interactorRoot.GetComponentInParent<PlayerFuelCarryController>();
            if (carry == null || !carry.IsCarrying)
                return;

            if (!_launcher.TryDepositFuel(carry))
                return;

            _nextDepositTime = Time.unscaledTime + Mathf.Max(0.05f, depositCooldownSeconds);
        }

        void TryDeposit(Collider other)
        {
            if (_launcher == null || other == null)
                return;

            Interact(WorldInteractionPromptController.ResolveInteractorRoot(other));
        }
    }
}
