using Game.Player;
using UnityEngine;

namespace Game.Features.StrengthTraining
{
    /// <summary>
    /// 力量训练专用动画：与 <see cref="Fishing.PlayerFishingAnimatorDriver"/> 分离，避免共用「放杆 / 放杆结束」触发器。
    /// 请在 Animator 中增加 Bool（默认名 StrengthTraining）驱动训练循环态；可选单独 Trigger（TrainingCast）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStrengthTrainingAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private PlayerAnimatorDriver locomotion;
        [SerializeField] private string activeBoolParam = "StrengthTraining";
        [SerializeField] private string castTriggerParam = "TrainingCast";

        int _activeBoolId;
        int _castTriggerId;
        bool _hasActiveBool;
        bool _hasCastTrigger;
        bool _presentationActive;

        public bool IsPresentationActive => _presentationActive;

        void Awake()
        {
            if (locomotion == null)
                locomotion = GetComponentInChildren<PlayerAnimatorDriver>();
            CacheParamIds();
        }

        void CacheParamIds()
        {
            var anim = ResolveAnimator();
            _hasActiveBool = HasParam(anim, activeBoolParam, AnimatorControllerParameterType.Bool);
            _hasCastTrigger = HasParam(anim, castTriggerParam, AnimatorControllerParameterType.Trigger);
            _activeBoolId = Animator.StringToHash(activeBoolParam);
            _castTriggerId = Animator.StringToHash(castTriggerParam);
        }

        public void BeginPresentation(float locomotionSpeedMetersPerSecond)
        {
            CacheParamIds();
            _presentationActive = true;

            locomotion?.SetForcedLocomotionSpeed(Mathf.Max(0f, locomotionSpeedMetersPerSecond));

            var anim = ResolveAnimator();
            if (anim == null)
                return;

            if (_hasActiveBool)
                anim.SetBool(_activeBoolId, true);

            if (_hasCastTrigger)
            {
                anim.ResetTrigger(_castTriggerId);
                anim.SetTrigger(_castTriggerId);
            }
        }

        public void EndPresentation()
        {
            if (!_presentationActive)
                return;

            _presentationActive = false;
            locomotion?.SetForcedLocomotionSpeed(null);

            var anim = ResolveAnimator();
            if (anim == null)
                return;

            if (_hasActiveBool)
                anim.SetBool(_activeBoolId, false);

            if (_hasCastTrigger)
                anim.ResetTrigger(_castTriggerId);
        }

        Animator ResolveAnimator()
        {
            if (locomotion != null)
                return locomotion.GetEffectiveAnimator();
            return GetComponentInChildren<Animator>(true);
        }

        static bool HasParam(Animator anim, string name, AnimatorControllerParameterType type)
        {
            if (anim == null || string.IsNullOrEmpty(name))
                return false;

            var ps = anim.parameters;
            for (var i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (p.type == type && p.name == name)
                    return true;
            }

            return false;
        }
    }
}
