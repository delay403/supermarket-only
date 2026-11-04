using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>开局掉落物理：落地稳定后关闭 Rigidbody，减少持续碰撞开销。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketItemPhysicsSettle : MonoBehaviour
    {
        [SerializeField] private float settleSpeedThreshold = 0.12f;
        [SerializeField] private float settleAngularSpeedThreshold = 0.35f;
        [SerializeField] private float settleHoldSeconds = 0.12f;
        [SerializeField] private float maxDropDurationSeconds = 4f;
        [SerializeField] private float linearDrag = 1.2f;
        [SerializeField] private float angularDrag = 5f;
        [SerializeField] private float slowDampingBoost = 10f;
        [SerializeField] private float slowAngularDampingBoost = 16f;

        Rigidbody _body;
        float _calmTimer;
        float _dropElapsed;
        float _dropGravityScale = 1f;
        bool _settled = true;

        public bool IsSettled => _settled;

        public void BeginDrop(SupermarketOrganizeConfig config)
        {
            ApplyConfig(config);
            EnsureRigidbody();
            _settled = false;
            _calmTimer = 0f;
            _dropElapsed = 0f;
            _body.constraints = RigidbodyConstraints.None;
            _body.detectCollisions = true;
            _body.isKinematic = false;
            _body.useGravity = true;
            _body.velocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.drag = linearDrag;
            _body.angularDrag = angularDrag;
            _body.WakeUp();
            enabled = true;
        }

        public void FreezePhysics()
        {
            if (_body == null)
                _body = GetComponent<Rigidbody>();

            if (_body != null)
            {
                _body.velocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.isKinematic = true;
                _body.useGravity = false;
            }

            _settled = true;
            enabled = false;
        }

        void CompleteNaturalSettle()
        {
            if (!_settled && _dropElapsed >= maxDropDurationSeconds)
                TrySnapToGround();

            FreezePhysics();

            var item = GetComponent<SupermarketItem>();
            if (item != null && item.State == SupermarketItemState.OnGround)
                item.EnableGroundInteraction();
        }

        void TrySnapToGround()
        {
            var config = SupermarketOrganizeConfig.ResolveRuntime();
            if (config != null && config.useFixedSpawnFloorY)
            {
                var pos = transform.position;
                pos.y = config.spawnFloorWorldY + 0.05f;
                transform.position = pos;
                return;
            }

            var rayHeight = config != null ? config.spawnGroundRaycastHeight : 30f;
            var rayDistance = config != null ? config.spawnGroundRaycastDistance : 40f;
            var mask = config != null && config.spawnGroundMask.value != 0
                ? config.spawnGroundMask
                : (LayerMask)~0;

            var origin = new Vector3(transform.position.x, rayHeight, transform.position.z);
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                rayDistance,
                mask,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
                return;

            var best = hits[0];
            for (var i = 1; i < hits.Length; i++)
            {
                if (hits[i].point.y < best.point.y)
                    best = hits[i];
            }

            var snapped = transform.position;
            snapped.y = best.point.y + 0.05f;
            transform.position = snapped;
        }

        void ApplyConfig(SupermarketOrganizeConfig config)
        {
            if (config == null)
                return;

            settleSpeedThreshold = config.physicsSettleSpeedThreshold;
            settleAngularSpeedThreshold = config.physicsSettleAngularSpeedThreshold;
            settleHoldSeconds = config.physicsSettleHoldSeconds;
            maxDropDurationSeconds = config.physicsMaxDropDurationSeconds;
            linearDrag = config.physicsLinearDrag;
            angularDrag = config.physicsAngularDrag;
            slowDampingBoost = config.physicsSlowDampingBoost;
            slowAngularDampingBoost = config.physicsSlowAngularDampingBoost;
            _dropGravityScale = Mathf.Max(0f, config.dropGravityScale);
        }

        void EnsureRigidbody()
        {
            if (_body == null)
                _body = GetComponent<Rigidbody>();
            if (_body == null)
                _body = gameObject.AddComponent<Rigidbody>();

            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        void FixedUpdate()
        {
            if (_settled || _body == null || _body.isKinematic)
                return;

            _dropElapsed += Time.fixedDeltaTime;

            if (_dropGravityScale > 1f && _body != null)
            {
                _body.AddForce(
                    Physics.gravity * ((_dropGravityScale - 1f) * _body.mass),
                    ForceMode.Force);
            }

            var speed = _body.velocity.sqrMagnitude;
            var angular = _body.angularVelocity.sqrMagnitude;
            var linearThreshold = settleSpeedThreshold * settleSpeedThreshold;
            var angularThreshold = settleAngularSpeedThreshold * settleAngularSpeedThreshold;

            if (speed <= linearThreshold * 4f || angular <= angularThreshold * 4f)
            {
                var linearDamp = 1f - slowDampingBoost * Time.fixedDeltaTime;
                var angularDamp = 1f - slowAngularDampingBoost * Time.fixedDeltaTime;
                _body.velocity *= Mathf.Clamp01(linearDamp);
                _body.angularVelocity *= Mathf.Clamp01(angularDamp);
            }

            if (speed <= linearThreshold && angular <= angularThreshold)
                _calmTimer += Time.fixedDeltaTime;
            else
                _calmTimer = 0f;

            if (_calmTimer >= settleHoldSeconds || _dropElapsed >= maxDropDurationSeconds)
                CompleteNaturalSettle();
        }
    }
}
