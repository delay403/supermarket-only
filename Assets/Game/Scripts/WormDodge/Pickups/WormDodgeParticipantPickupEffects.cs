using Game.Player;
using Game.VFX;
using UnityEngine;
using UnityEngine.AI;

namespace Game.WormDodge.Pickups
{
    /// <summary>
    /// 挂在参与者（玩家或 AI）上，应用道具带来的移速/跳跃加成并在回合重置时清除。
    /// 拾取特效作为参与者子物体播放，并随角色移动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WormDodgeParticipantPickupEffects : MonoBehaviour
    {
        [SerializeField] private WormDodgeParticipant participant;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private PlayerJumpController jumpController;
        [SerializeField] private NavMeshAgent aiAgent;
        [SerializeField] private PlayerRig rig;
        [SerializeField] private Transform buffVfxAnchor;

        float _speedBoostEndTime;
        float _speedBoostMul;
        float _baseAiSpeed = -1f;

        float _jumpBoostEndTime = -1f;
        float _jumpBoostMul = 1f;

        GameObject _speedBoostVfx;
        GameObject _superJumpVfx;
        Transform _fallbackVfxAnchor;

        void Awake()
        {
            if (participant == null)
                participant = GetComponent<WormDodgeParticipant>();
            if (playerMotor == null)
                playerMotor = GetComponent<PlayerMotor>();
            if (jumpController == null)
                jumpController = GetComponent<PlayerJumpController>();
            if (aiAgent == null)
                aiAgent = GetComponent<NavMeshAgent>();
            if (rig == null)
                rig = GetComponent<PlayerRig>();
        }

        void OnEnable() => WormDodgeDirector.RunReset += ResetAll;

        void OnDisable()
        {
            WormDodgeDirector.RunReset -= ResetAll;
            ResetAll();
        }

        void Update()
        {
            if (Time.time >= _speedBoostEndTime && _speedBoostMul > 0f)
            {
                _speedBoostMul = 0f;
                ApplySpeedNow();
            }

            if (_jumpBoostEndTime > 0f && Time.time >= _jumpBoostEndTime)
            {
                _jumpBoostEndTime = -1f;
                _jumpBoostMul = 1f;
            }

            SyncBuffVfx();
        }

        void LateUpdate()
        {
            KeepVfxParented(_speedBoostVfx);
            KeepVfxParented(_superJumpVfx);
        }

        public void ApplySpeedBoost(float durationSeconds, float finalMoveSpeedMultiplierAdd)
        {
            durationSeconds = Mathf.Max(0.05f, durationSeconds);
            finalMoveSpeedMultiplierAdd = Mathf.Max(0f, finalMoveSpeedMultiplierAdd);
            _speedBoostMul = finalMoveSpeedMultiplierAdd;
            _speedBoostEndTime = Time.time + durationSeconds;
            ApplySpeedNow();
            EnsureFollowVfx(ref _speedBoostVfx, VfxKind.WormDodgeSpeedBoost);
        }

        public void ApplySuperJump(float durationSeconds, float jumpHeightMultiplierAdd)
        {
            if (participant != null && !participant.IsPlayer)
                return;

            durationSeconds = Mathf.Max(0.05f, durationSeconds);
            jumpHeightMultiplierAdd = Mathf.Max(0f, jumpHeightMultiplierAdd);
            _jumpBoostMul = 1f + jumpHeightMultiplierAdd;
            _jumpBoostEndTime = Time.time + durationSeconds;
            EnsureFollowVfx(ref _superJumpVfx, VfxKind.WormDodgeSuperJump);
        }

        /// <summary>金币拾取：挂在参与者脚下播放一次后自动回池。</summary>
        public void PlayCoinPickupVfx()
        {
            var mgr = GameplayVfxManager.Instance;
            if (mgr == null || !mgr.HasKind(VfxKind.WormDodgeCoin))
                return;

            var parent = ResolveVfxParent();
            if (parent == null)
                return;

            mgr.BorrowFollow(
                VfxKind.WormDodgeCoin,
                parent,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                autoReleaseAfterSeconds: -1f);
        }

        public float ResolveJumpVerticalSpeedMultiplier() =>
            _jumpBoostEndTime > 0f && Time.time < _jumpBoostEndTime ? _jumpBoostMul : 1f;

        void SyncBuffVfx()
        {
            if (IsSpeedBoostActive())
                EnsureFollowVfx(ref _speedBoostVfx, VfxKind.WormDodgeSpeedBoost);
            else
                ReleaseFollowVfx(ref _speedBoostVfx);

            if (IsSuperJumpActive())
                EnsureFollowVfx(ref _superJumpVfx, VfxKind.WormDodgeSuperJump);
            else
                ReleaseFollowVfx(ref _superJumpVfx);
        }

        bool IsSpeedBoostActive() => _speedBoostMul > 0f && Time.time < _speedBoostEndTime;

        bool IsSuperJumpActive() => _jumpBoostEndTime > 0f && Time.time < _jumpBoostEndTime;

        void EnsureFollowVfx(ref GameObject instance, VfxKind kind)
        {
            var parent = ResolveVfxParent();
            if (parent == null)
                return;

            if (instance != null)
            {
                KeepVfxParented(instance, parent);
                return;
            }

            var mgr = GameplayVfxManager.Instance;
            if (mgr == null || !mgr.HasKind(kind))
                return;

            instance = mgr.BorrowFollow(
                kind,
                parent,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one);
        }

        void KeepVfxParented(GameObject instance, Transform expectedParent = null)
        {
            if (instance == null || !instance.activeInHierarchy)
                return;

            expectedParent ??= ResolveVfxParent();
            if (expectedParent == null)
                return;

            var t = instance.transform;
            if (t.parent == expectedParent)
                return;

            t.SetParent(expectedParent, worldPositionStays: false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        void ReleaseFollowVfx(ref GameObject instance)
        {
            if (instance == null)
                return;

            GameplayVfxManager.Instance?.Release(instance);
            instance = null;
        }

        Transform ResolveVfxParent()
        {
            if (buffVfxAnchor != null)
                return buffVfxAnchor;

            ResolveFootAnchor();
            if (buffVfxAnchor != null)
                return buffVfxAnchor;

            if (_fallbackVfxAnchor == null)
            {
                var root = participant != null ? participant.transform : transform;
                var anchorGo = new GameObject("WormDodgePickupVfxAnchor");
                anchorGo.transform.SetParent(root, false);
                anchorGo.transform.localPosition = Vector3.zero;
                anchorGo.transform.localRotation = Quaternion.identity;
                anchorGo.transform.localScale = Vector3.one;
                _fallbackVfxAnchor = anchorGo.transform;
            }

            return _fallbackVfxAnchor;
        }

        void ResolveFootAnchor()
        {
            if (buffVfxAnchor != null)
                return;

            rig?.EnsureSceneSocketsResolved();
            if (rig != null && rig.footSocket != null)
            {
                buffVfxAnchor = rig.footSocket;
                return;
            }

            var characterRoot = rig != null ? rig.characterSocket : null;
            if (characterRoot == null)
                characterRoot = participant != null ? participant.transform : transform;

            buffVfxAnchor = CharacterHierarchySockets.FindDeepChildByName(
                characterRoot,
                CharacterHierarchySockets.FootRootTransformName);
        }

        void ApplySpeedNow()
        {
            if (participant != null && participant.IsPlayer)
            {
                playerMotor?.SetPickupMoveSpeedMultiplier(_speedBoostMul);
                return;
            }

            if (aiAgent == null)
                return;

            if (_baseAiSpeed < 0f)
                _baseAiSpeed = aiAgent.speed;

            aiAgent.speed = _baseAiSpeed * (1f + _speedBoostMul);
        }

        void ResetAll()
        {
            _speedBoostEndTime = 0f;
            _speedBoostMul = 0f;
            _jumpBoostEndTime = -1f;
            _jumpBoostMul = 1f;

            ReleaseFollowVfx(ref _speedBoostVfx);
            ReleaseFollowVfx(ref _superJumpVfx);

            playerMotor?.SetPickupMoveSpeedMultiplier(0f);

            if (aiAgent != null && _baseAiSpeed >= 0f)
                aiAgent.speed = _baseAiSpeed;
        }

        void OnDestroy()
        {
            if (_fallbackVfxAnchor != null)
                Destroy(_fallbackVfxAnchor.gameObject);
        }
    }
}
