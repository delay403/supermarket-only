using Game.AI;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 黑影 AI：追踪并攻击玩家。
    /// 追踪移动复用 <see cref="NavMeshAIPatrolAgent.SetModeChase"/>（与钓鱼/病人同一套导航框架）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalShadowAi : MonoBehaviour
    {
        const string CharacterRootName = "CharacterRoot";

        [SerializeField] private Transform characterRoot;
        [SerializeField] private NavMeshAIPatrolAgent patrol;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private float attackRange = 1.4f;
        [SerializeField] private float attackCooldown = 1f;

        AnimalHospitalService _service;
        float _health = 10f;
        float _cooldown;
        bool _fromWard;

        public bool IsAlive => _health > 0f;
        public bool FromWard => _fromWard;
        public Transform ActorTransform => characterRoot != null ? characterRoot : transform;

        public void Initialize(AnimalHospitalService service, float maxHealth, bool fromWard)
        {
            _service = service;
            _health = maxHealth;
            _fromWard = fromWard;
            ResolveLocomotionComponents();

            var interactable = GetComponent<AnimalHospitalInteractable>()
                               ?? gameObject.AddComponent<AnimalHospitalInteractable>();
            interactable.Configure(AnimalHospitalInteractableKind.Shadow, "shadow", "黑影");
            AnimalHospitalInteractableLayers.ApplyAiLayer(gameObject);

            var player = _service != null ? _service.PlayerTransform : null;
            if (patrol != null && player != null)
            {
                patrol.enabled = true;
                patrol.SetModeChase(player);
            }
        }

        public void TakeDamage(float amount)
        {
            _health -= Mathf.Max(0f, amount);
            if (_health <= 0f)
            {
                _service?.NotifyShadowDefeated(this);
                Destroy(gameObject);
            }
        }

        void Awake() => ResolveLocomotionComponents();

        void Update()
        {
            if (_service == null || !IsAlive)
                return;

            var player = _service.PlayerTransform;
            if (player == null)
                return;

            if (patrol != null)
            {
                if (!patrol.enabled)
                    patrol.enabled = true;
                patrol.SetModeChase(player);
            }
            else if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(player.position);
            }

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f)
                return;

            if (Vector3.Distance(ActorTransform.position, player.position) > attackRange)
                return;

            _cooldown = attackCooldown;
            var loss = _service.Config != null ? _service.Config.shadowAttackSanityLoss : 5f;
            _service.Sanity.ApplyDamage(loss);
        }

        void ResolveLocomotionComponents()
        {
            if (characterRoot == null)
            {
                var found = transform.Find(CharacterRootName);
                if (found != null)
                    characterRoot = found;
            }

            if (patrol == null)
                patrol = GetComponentInChildren<NavMeshAIPatrolAgent>(true);
            if (agent == null)
                agent = patrol != null
                    ? patrol.GetComponent<NavMeshAgent>()
                    : GetComponentInChildren<NavMeshAgent>(true);

            if (characterRoot == null && agent != null)
                characterRoot = agent.transform;
        }
    }
}
