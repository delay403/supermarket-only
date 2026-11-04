using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Player
{
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private float maxMoveSpeed = 5f;
        [SerializeField] private bool writeLegacyMoveSpeed01 = false;

        [Header("Animator Params (match your controller)")]
        [SerializeField] private string velocityParam = "Velocity";
        [SerializeField] private string climbBoolParam = "Climb";
        [SerializeField] private string glideBoolParam = "IsGliding";
        [SerializeField] private string climbSpeedParam = "ClimbSpeed";
        [Tooltip("Animator 上 Victory 为 Bool 时的参数名；与 Trigger 不能同名。")]
        [SerializeField] private string victoryBoolName = "Victory";
        [FormerlySerializedAs("victoryBoolParam")]
        [SerializeField] private string victoryTriggerParam = "Victory";
        [Tooltip("为 true 时 Victory Bool 在 victoryBoolHoldRealtimeSeconds 后自动 false；为 false 时保持庆祝姿态直至 SetVictory(false)（结算关闭/重生时会清除）。")]
        [SerializeField] private bool victoryBoolAutoClearWithTimer = false;
        [Tooltip("与 victoryBoolAutoClearWithTimer 配合：自动清除前的保持时长（实时秒）。")]
        [SerializeField] private float victoryBoolHoldRealtimeSeconds = 3f;
        [SerializeField] private string jumpTriggerParam = "Jump";
        [SerializeField] private string isGroundBoolParam = "IsGround";
        [Tooltip("相对塔底的攀爬高度（米），在结束攀爬时写入一次。")]
        [SerializeField] private string heightFloatParam = "Height";
        [Tooltip("相对塔底高度减去「最低滑翔高度」后的差值；Animator 用 Greater 0 / Less 0 与常数 0 比较即可对齐 GlideController.minGlideHeightMeters。")]
        [SerializeField] private string heightOverMinGlideFloatParam = "HeightOverMinGlide";

        private Animator _anim;

        private static readonly int MoveSpeed01 = Animator.StringToHash("MoveSpeed");

        private int _velocityId;
        private int _climbId;
        private int _glideId;
        private int _climbSpeedId;
        private int _victoryBoolId;
        private int _victoryTriggerId;
        private int _jumpTriggerId;
        private int _isGroundId;
        private int _heightId;
        private int _heightOverMinGlideId;

        private bool _hasVelocity;
        private bool _hasClimb;
        private bool _hasGlide;
        private bool _hasClimbSpeed;
        private bool _hasVictoryBool;
        private bool _hasVictoryTrigger;
        private bool _hasJumpTrigger;
        private bool _hasIsGround;
        private bool _hasHeight;
        private bool _hasHeightOverMinGlide;

        private Coroutine _victoryBoolResetRoutine;
        private float? _forcedLocomotionSpeed;

        /// <summary>跑步机等非位移状态下覆盖 Velocity 参数（null = 恢复跟随 Motor）。</summary>
        public void SetForcedLocomotionSpeed(float? xzSpeedMetersPerSecond)
        {
            _forcedLocomotionSpeed = xzSpeedMetersPerSecond.HasValue
                ? Mathf.Max(0f, xzSpeedMetersPerSecond.Value)
                : (float?)null;
        }

        private void Awake()
        {
            _anim = ResolveAnimatorFromHierarchy();
            DisableUnusedAnimatorOnDriverGameObject();
            ResolveMotorReference();
            CacheParamIds();
        }

        /// <summary>
        /// 供滑翔/攀爬等取骨骼上的 Animator（可能与 <see cref="PlayerAnimatorDriver"/> 不在同一物体）。
        /// </summary>
        public Animator GetEffectiveAnimator() => _anim;

        /// <summary>
        /// 运行时 Instantiate 角色后调用：Motor + 子物体 Animator + 参数缓存。
        /// </summary>
        public void PrepareAfterRuntimeSpawn(Transform playerRoot)
        {
            EnsureMotorResolved(playerRoot);
            _anim = ResolveAnimatorFromHierarchy();
            DisableUnusedAnimatorOnDriverGameObject();
            CacheParamIds();
        }

        /// <summary>
        /// RequireComponent 可能在 Driver 所在物体上生成<strong>无 Controller</strong>的 Animator，而真正带 Controller 的在子网格上。
        /// </summary>
        Animator ResolveAnimatorFromHierarchy()
        {
            var direct = GetComponent<Animator>();
            if (direct != null && direct.runtimeAnimatorController != null)
                return direct;

            var list = GetComponentsInChildren<Animator>(true);
            Animator fallback = null;
            for (var i = 0; i < list.Length; i++)
            {
                var a = list[i];
                if (a == null || a.runtimeAnimatorController == null) continue;
                var av = a.avatar;
                if (av != null && av.isHuman)
                    return a;
                if (fallback == null) fallback = a;
            }

            return fallback ?? direct;
        }

        /// <summary>同一物体上多个 Animator 时关掉无 Controller 的占位组件，避免 Humanoid 双实例冲突。</summary>
        void DisableUnusedAnimatorOnDriverGameObject()
        {
            var list = GetComponents<Animator>();
            if (list.Length <= 1 || _anim == null) return;

            for (var i = 0; i < list.Length; i++)
            {
                var a = list[i];
                if (a == null || a == _anim) continue;
                if (a.runtimeAnimatorController == null)
                    a.enabled = false;
            }
        }

        /// <summary>
        /// Awake 时调用一次；换装后也应在解析玩家根后再调用（预制体内 Motor 常为空，且 Motor 常在 CharacterRoot 的兄弟节点上）。
        /// </summary>
        void ResolveMotorReference()
        {
            if (motor == null)
                motor = GetComponentInParent<PlayerMotor>();
        }

        /// <summary>
        /// 从玩家实体根（如 Player）向下查找 <see cref="PlayerMotor"/>；用于运行时生成的角色预制体未序列化 Motor 的情况。
        /// </summary>
        public void EnsureMotorResolved(Transform playerRoot)
        {
            if (motor != null) return;
            if (playerRoot != null)
                motor = playerRoot.GetComponentInChildren<PlayerMotor>(true);
            ResolveMotorReference();
        }

        public void BindMotor(PlayerMotor m) => motor = m;

        private void OnDisable()
        {
            StopVictoryBoolResetRoutine();
        }

        private void CacheParamIds()
        {
            _velocityId = Animator.StringToHash(velocityParam);
            _climbId = Animator.StringToHash(climbBoolParam);
            _glideId = Animator.StringToHash(glideBoolParam);
            _climbSpeedId = Animator.StringToHash(climbSpeedParam);
            _victoryBoolId = Animator.StringToHash(victoryBoolName);
            _victoryTriggerId = Animator.StringToHash(victoryTriggerParam);
            _jumpTriggerId = Animator.StringToHash(jumpTriggerParam);
            _isGroundId = Animator.StringToHash(isGroundBoolParam);
            _heightId = Animator.StringToHash(heightFloatParam);
            _heightOverMinGlideId = Animator.StringToHash(heightOverMinGlideFloatParam);

            _hasVelocity = HasParam(velocityParam, AnimatorControllerParameterType.Float);
            _hasClimb = HasParam(climbBoolParam, AnimatorControllerParameterType.Bool);
            _hasGlide = HasParam(glideBoolParam, AnimatorControllerParameterType.Bool);
            _hasClimbSpeed = HasParam(climbSpeedParam, AnimatorControllerParameterType.Float);
            _hasVictoryBool = !string.IsNullOrEmpty(victoryBoolName) &&
                              HasParam(victoryBoolName, AnimatorControllerParameterType.Bool);
            _hasVictoryTrigger = !string.IsNullOrEmpty(victoryTriggerParam) &&
                                 HasParam(victoryTriggerParam, AnimatorControllerParameterType.Trigger);
            _hasJumpTrigger = HasParam(jumpTriggerParam, AnimatorControllerParameterType.Trigger);
            _hasIsGround = HasParam(isGroundBoolParam, AnimatorControllerParameterType.Bool);
            _hasHeight = HasParam(heightFloatParam, AnimatorControllerParameterType.Float);
            _hasHeightOverMinGlide = HasParam(heightOverMinGlideFloatParam, AnimatorControllerParameterType.Float);
        }

        private bool HasParam(string name, AnimatorControllerParameterType type)
        {
            if (_anim == null || string.IsNullOrEmpty(name)) return false;
            var ps = _anim.parameters;
            for (var i = 0; i < ps.Length; i++)
            {
                if (ps[i].type == type && ps[i].name == name)
                    return true;
            }
            return false;
        }

        public void SetClimb(bool climbing)
        {
            if (_anim != null && _hasClimb) _anim.SetBool(_climbId, climbing);
        }

        public void SetGlide(bool gliding)
        {
            if (_anim != null && _hasGlide) _anim.SetBool(_glideId, gliding);
        }

        public void TriggerJump()
        {
            if (_anim != null && _hasJumpTrigger) _anim.SetTrigger(_jumpTriggerId);
        }

        public void ResetJumpTrigger()
        {
            if (_anim != null && _hasJumpTrigger) _anim.ResetTrigger(_jumpTriggerId);
        }

        /// <summary>优先写 Victory Bool；若开启 <see cref="victoryBoolAutoClearWithTimer"/> 则延时清除；否则使用 Trigger。</summary>
        public void TriggerVictory()
        {
            if (_anim == null) return;
            if (_hasVictoryBool)
            {
                if (victoryBoolAutoClearWithTimer && victoryBoolHoldRealtimeSeconds > 0f)
                    SetVictoryBoolForSeconds(true, victoryBoolHoldRealtimeSeconds);
                else
                    SetVictoryBool(true);
            }
            else if (_hasVictoryTrigger) _anim.SetTrigger(_victoryTriggerId);
        }

        public void ResetVictoryTrigger()
        {
            if (_anim != null && _hasVictoryTrigger) _anim.ResetTrigger(_victoryTriggerId);
        }

        /// <summary>Victory Bool 为 true，并在 <paramref name="holdRealtimeSeconds"/> 秒（实时）后置 false；会取消尚未执行的自动复位。</summary>
        public void SetVictoryBoolForSeconds(bool value, float holdRealtimeSeconds)
        {
            if (_anim == null || !_hasVictoryBool) return;
            StopVictoryBoolResetRoutine();
            _anim.SetBool(_victoryBoolId, value);
            if (value && holdRealtimeSeconds > 0f)
                _victoryBoolResetRoutine = StartCoroutine(ResetVictoryBoolAfterRealtimeDelay(holdRealtimeSeconds));
        }

        /// <summary>立即写入 Victory Bool；不启动延时复位。</summary>
        public void SetVictoryBool(bool value)
        {
            if (_anim == null || !_hasVictoryBool) return;
            StopVictoryBoolResetRoutine();
            _anim.SetBool(_victoryBoolId, value);
        }

        public void ClearVictoryBoolState()
        {
            StopVictoryBoolResetRoutine();
            if (_anim != null && _hasVictoryBool) _anim.SetBool(_victoryBoolId, false);
        }

        private void StopVictoryBoolResetRoutine()
        {
            if (_victoryBoolResetRoutine == null) return;
            StopCoroutine(_victoryBoolResetRoutine);
            _victoryBoolResetRoutine = null;
        }

        private IEnumerator ResetVictoryBoolAfterRealtimeDelay(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, seconds));
            _victoryBoolResetRoutine = null;
            if (_hasVictoryBool && _anim != null) _anim.SetBool(_victoryBoolId, false);
        }

        /// <summary>兼容旧调用：true 时写入 Victory；false 时清除 Bool 与 Trigger。</summary>
        public void SetVictory(bool victory)
        {
            if (victory)
            {
                if (_anim == null) return;
                if (_hasVictoryBool)
                {
                    if (victoryBoolAutoClearWithTimer && victoryBoolHoldRealtimeSeconds > 0f)
                        SetVictoryBoolForSeconds(true, victoryBoolHoldRealtimeSeconds);
                    else
                        SetVictoryBool(true);
                }
                else if (_hasVictoryTrigger) _anim.SetTrigger(_victoryTriggerId);
            }
            else
            {
                ClearVictoryBoolState();
                ResetVictoryTrigger();
            }
        }

        /// <summary>
        /// 写入 Animator 的 <c>ClimbSpeed</c>（Climb 状态 Motion 的 <b>Speed → Multiplier</b>）。
        /// 数值由调用方决定；通常为「竖直攀爬 m/s ÷ 比例系数」。0 = 暂停；越大越快。最终片段速度 ≈ 状态基础 Speed × 本乘子。
        /// </summary>
        public void SetClimbSpeed(float climbSpeedMultiplierOrScaledValue)
        {
            if (_anim != null && _hasClimbSpeed) _anim.SetFloat(_climbSpeedId, Mathf.Max(0f, climbSpeedMultiplierOrScaledValue));
        }

        /// <summary>
        /// 结束攀爬时调用：写入绝对高度，并写入「高度 − minGlideHeight」（与滑翔组件上的最低滑翔高度一致）。
        /// Animator 建议：<b>Climb→空翻</b> 条件为 <c>HeightOverMinGlide Greater 0</c>；<b>Climb→Jump</b> 为 <c>HeightOverMinGlide Less 0</c>（阈值填 0）。
        /// 仍可在别处用 <c>Height</c> 读取绝对米数。须在本帧 <see cref="SetClimb"/>（false）之前调用。
        /// </summary>
        public void ApplyClimbExitHeightForAnimator(float absoluteMetersAboveTowerBase, float minGlideHeightMeters)
        {
            if (_anim == null) return;
            var abs = Mathf.Max(0f, absoluteMetersAboveTowerBase);
            if (_hasHeight) _anim.SetFloat(_heightId, abs);

            if (!_hasHeightOverMinGlide) return;
            var min = Mathf.Max(0f, minGlideHeightMeters);
            var delta = abs - min;
            // 与阈值相等时视为「达到最低滑翔高度」，走空翻侧（Animator 的 Greater 0 为严格大于）
            if (delta >= 0f && delta < 0.0001f) delta = 0.0001f;
            _anim.SetFloat(_heightOverMinGlideId, delta);
        }

        private void Update()
        {
            if (_anim == null) return;

            if (motor != null && _hasIsGround) _anim.SetBool(_isGroundId, motor.IsGrounded);

            if (_forcedLocomotionSpeed.HasValue)
            {
                var forcedSpeed = _forcedLocomotionSpeed.Value;
                if (_hasVelocity) _anim.SetFloat(_velocityId, forcedSpeed);
                if (writeLegacyMoveSpeed01)
                {
                    var forcedMax = Mathf.Max(0.0001f, maxMoveSpeed);
                    _anim.SetFloat(MoveSpeed01, Mathf.Clamp01(forcedSpeed / forcedMax));
                }

                return;
            }

            if (motor == null) return;
            var speed = new Vector2(motor.VelocityWorld.x, motor.VelocityWorld.z).magnitude;
            var max = Mathf.Max(0.0001f, maxMoveSpeed);
            var v = Mathf.Clamp(speed, 0f, max);
            if (_hasVelocity) _anim.SetFloat(_velocityId, v);

            if (writeLegacyMoveSpeed01)
                _anim.SetFloat(MoveSpeed01, Mathf.Clamp01(v / max));
        }
    }
}

