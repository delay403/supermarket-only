using Game.Collectibles.Equipment;
using Game.Features.StrengthTraining;
using Game.Player;
using System.Collections;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 钓鱼动画：放杆 / 收杆 / 放杆结束 / 收杆结束。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFishingAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private PlayerFishingController fishing;
        [SerializeField] private PlayerFishingLineDriver lineDriver;
        [SerializeField] private PlayerAnimatorDriver animatorDriver;
        [SerializeField] private EquipmentSpawner equipment;

        [Header("Animator 触发器（与状态机参数名一致）")]
        [SerializeField] private string castTriggerParam = "放杆";
        [SerializeField] private string castEndTriggerParam = "放杆结束";
        [SerializeField] private string reelTriggerParam = "收杆";
        [SerializeField] private string reelEndTriggerParam = "收杆结束";
        [SerializeField] private string catchShowcaseTriggerParam = "钓获展示";

        [Header("收竿挂点")]
        [Tooltip("触发「放杆结束」后等待多久再把竿挂回背部（与状态机片段时长对齐）。")]
        [SerializeField] private float castEndMountBackDelaySeconds = 0.55f;
        [Tooltip("触发「收杆结束」后等待多久再把竿挂回背部。")]
        [SerializeField] private float reelEndMountBackDelaySeconds = 0.65f;

        int _castTriggerId;
        int _castEndTriggerId;
        int _reelTriggerId;
        int _reelEndTriggerId;
        int _catchShowcaseTriggerId;
        bool _hasCastTrigger;
        bool _hasCastEndTrigger;
        bool _hasReelTrigger;
        bool _hasReelEndTrigger;
        bool _hasCatchShowcaseTrigger;
        bool _reelUsedThisSession;
        PlayerStrengthTrainingController _training;
        Coroutine _mountRodOnBackRoutine;

        void Awake()
        {
            if (fishing == null) fishing = GetComponent<PlayerFishingController>();
            if (lineDriver == null) lineDriver = GetComponent<PlayerFishingLineDriver>();
            if (animatorDriver == null) animatorDriver = GetComponent<PlayerAnimatorDriver>();
            if (equipment == null) equipment = GetComponent<EquipmentSpawner>();
            _training = GetComponent<PlayerStrengthTrainingController>();
            CacheTriggerIds();
        }

        bool IsStrengthTrainingActive =>
            _training != null && _training.IsSessionActive;

        void OnEnable()
        {
            if (fishing != null)
            {
                fishing.SessionStarted += OnSessionStarted;
                fishing.ReelApplied += OnReelApplied;
                fishing.SessionCancelled += OnSessionCancelled;
            }

            if (lineDriver != null)
                lineDriver.PostCatchPresentationEnded += OnPostCatchPresentationEnded;
        }

        void OnDisable()
        {
            CancelScheduledMountRodOnBack();

            if (fishing != null)
            {
                fishing.SessionStarted -= OnSessionStarted;
                fishing.ReelApplied -= OnReelApplied;
                fishing.SessionCancelled -= OnSessionCancelled;
            }

            if (lineDriver != null)
                lineDriver.PostCatchPresentationEnded -= OnPostCatchPresentationEnded;
        }

        void CacheTriggerIds()
        {
            _castTriggerId = Animator.StringToHash(castTriggerParam);
            _castEndTriggerId = Animator.StringToHash(castEndTriggerParam);
            _reelTriggerId = Animator.StringToHash(reelTriggerParam);
            _reelEndTriggerId = Animator.StringToHash(reelEndTriggerParam);
            _catchShowcaseTriggerId = Animator.StringToHash(catchShowcaseTriggerParam);
            _hasCastTrigger = HasTrigger(castTriggerParam);
            _hasCastEndTrigger = HasTrigger(castEndTriggerParam);
            _hasReelTrigger = HasTrigger(reelTriggerParam);
            _hasReelEndTrigger = HasTrigger(reelEndTriggerParam);
            _hasCatchShowcaseTrigger = HasTrigger(catchShowcaseTriggerParam);
        }

        bool HasTrigger(string paramName)
        {
            var anim = ResolveAnimator();
            if (anim == null || string.IsNullOrEmpty(paramName))
                return false;

            var ps = anim.parameters;
            for (var i = 0; i < ps.Length; i++)
            {
                if (ps[i].type == AnimatorControllerParameterType.Trigger &&
                    ps[i].name == paramName)
                    return true;
            }

            return false;
        }

        Animator ResolveAnimator()
        {
            if (animatorDriver != null)
                return animatorDriver.GetEffectiveAnimator();

            return GetComponentInChildren<Animator>(true);
        }

        void OnSessionStarted(FishingSessionState _)
        {
            if (IsStrengthTrainingActive)
                return;
            CancelScheduledMountRodOnBack();
            CacheTriggerIds();
            _reelUsedThisSession = false;
            ResetFishingTriggers();
            PlayCast();
        }

        void OnReelApplied(FishingService.ReelResult _)
        {
            _reelUsedThisSession = true;
            PlayReel();
        }

        void OnSessionCancelled()
        {
            if (IsStrengthTrainingActive)
                return;
            if (_reelUsedThisSession)
            {
                PlayReelEnd();
                ScheduleMountRodOnBack(reelEndMountBackDelaySeconds);
            }
            else
            {
                PlayCastEnd();
                ScheduleMountRodOnBack(castEndMountBackDelaySeconds);
            }
        }

        void OnPostCatchPresentationEnded(bool naturalComplete)
        {
            if (IsStrengthTrainingActive)
                return;
            if (!naturalComplete)
                return;

            PlayReelEnd();
            ScheduleMountRodOnBack(reelEndMountBackDelaySeconds);
        }

        void CancelScheduledMountRodOnBack()
        {
            if (_mountRodOnBackRoutine == null)
                return;

            StopCoroutine(_mountRodOnBackRoutine);
            _mountRodOnBackRoutine = null;
        }

        void ScheduleMountRodOnBack(float delaySeconds)
        {
            CancelScheduledMountRodOnBack();
            _mountRodOnBackRoutine = StartCoroutine(CoMountRodOnBackAfterDelay(delaySeconds));
        }

        IEnumerator CoMountRodOnBackAfterDelay(float delaySeconds)
        {
            if (delaySeconds > 0.0001f)
                yield return new WaitForSeconds(delaySeconds);

            _mountRodOnBackRoutine = null;

            if (fishing != null && fishing.IsSessionActive)
                yield break;

            if (lineDriver != null && lineDriver.IsPostCatchPresentationActive)
                yield break;

            equipment?.MountRodOnBack();
        }

        void ResetFishingTriggers()
        {
            var anim = ResolveAnimator();
            if (anim == null)
                return;

            if (_hasCastTrigger) anim.ResetTrigger(_castTriggerId);
            if (_hasCastEndTrigger) anim.ResetTrigger(_castEndTriggerId);
            if (_hasReelTrigger) anim.ResetTrigger(_reelTriggerId);
            if (_hasReelEndTrigger) anim.ResetTrigger(_reelEndTriggerId);
            if (_hasCatchShowcaseTrigger) anim.ResetTrigger(_catchShowcaseTriggerId);
        }

        public void PlayCast()
        {
            if (IsStrengthTrainingActive || !_hasCastTrigger)
                return;
            var anim = ResolveAnimator();
            if (anim == null) return;
            anim.SetTrigger(_castTriggerId);
        }

        /// <summary>未收杆即结束放杆（取消钓鱼、力量训练结束等）。</summary>
        public void PlayCastEnd()
        {
            if (IsStrengthTrainingActive || !_hasCastEndTrigger)
                return;
            var anim = ResolveAnimator();
            if (anim == null) return;
            anim.SetTrigger(_castEndTriggerId);
        }

        public void PlayReel()
        {
            if (!_hasReelTrigger)
                return;
            var anim = ResolveAnimator();
            if (anim == null) return;
            anim.SetTrigger(_reelTriggerId);
        }

        public void PlayCatchShowcase()
        {
            if (IsStrengthTrainingActive || !_hasCatchShowcaseTrigger)
                return;
            var anim = ResolveAnimator();
            if (anim == null) return;
            anim.SetTrigger(_catchShowcaseTriggerId);
        }

        public void PlayReelEnd()
        {
            if (!_hasReelEndTrigger)
                return;
            var anim = ResolveAnimator();
            if (anim == null) return;
            anim.SetTrigger(_reelEndTriggerId);
        }
    }
}
