using System.Collections;
using Game.AI;
using Game.AnimalHospital.Config;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 病人高层状态机（等候/接待/病房/离开/异常攻击）。
    /// 移动与 Idle/Walk 动画复用 <see cref="NavMeshAIPatrolAgent"/>（与钓鱼 AI 同一套 CharacterRoot 框架）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalPatient : MonoBehaviour
    {
        const string CharacterRootName = "CharacterRoot";

        [Header("CharacterRoot（与钓鱼 AI 一致）")]
        [SerializeField] private Transform characterRoot;
        [SerializeField] private NavMeshAIPatrolAgent patrol;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;

        [Header("Domain")]
        [SerializeField] private Transform admissionAttachPoint;
        [SerializeField] private GameObject fireVisual;

        [Header("Domain Anim Triggers（非移动）")]
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string lieTrigger = "Lie";

        [Header("Bed Lie Pose")]
        [Tooltip("躺在床上的欧拉角。默认与角色平躺姿态一致（X=-90, Y=0, Z=90）。")]
        [SerializeField] private Vector3 bedLieEuler = new Vector3(-90f, 0f, 90f);
        [Tooltip("勾选后欧拉相对 BedPoint；不勾选则使用世界空间欧拉（与手动摆好的 CharacterRoot 一致）。")]
        [SerializeField] private bool bedLieRelativeToBedPoint;

        [Header("Movement")]
        [SerializeField] private float arrivalExtraToleranceMeters = 0.35f;
        [SerializeField] private float attackRange = 1.6f;

        [Header("Debug")]
        [SerializeField] private bool aiDebugLogging = true;

        AnimalHospitalInteractable _interactable;
        string _lastMoveLogKey;
        float _nextMoveStatusLogTime;
        AnimalHospitalPatientEmoji _emoji;
        AnimalHospitalPatientCatalogEntry _def;
        AnimalHospitalService _service;
        string _illnessDescription = string.Empty;
        readonly System.Collections.Generic.List<string> _remainingMedicineIds =
            new System.Collections.Generic.List<string>();
        Transform _occupiedChair;
        Transform _chaseTarget;
        float _health = 10f;
        float _curtainWaitElapsed;
        float _anomalousBerserkTimer = -1f;
        bool _receptionCompletedForBerserk;
        float _onFireTimer = -1f;
        float _attackCooldown;
        SkinnedMeshRenderer[] _bodyRenderers;
        Material[][] _normalBodyMaterials;
        bool _anomalousVisualApplied;
        bool _movingToPoint;
        Vector3 _moveDestination;
        float _pathRetryCooldown;
        float _baseAgentSpeed = -1f;
        float _slowUntil = -1f;
        bool _lyingOnBed;
        Vector3 _liePosePosition;
        Quaternion _liePoseRotation;
        Coroutine _postTreatmentLeaveRoutine;

        public string PatientId => _def != null ? _def.patientId : name;
        public string DisplayName => _def != null ? _def.displayName : name;
        public string IllnessDescription => _illnessDescription ?? string.Empty;
        /// <summary>当前仍需使用的第一种药物（显示用）。</summary>
        public string RequiredMedicineId =>
            _remainingMedicineIds.Count > 0 ? _remainingMedicineIds[0] : string.Empty;
        public int RemainingMedicineCount => _remainingMedicineIds.Count;
        public bool HasRemainingMedicines => _remainingMedicineIds.Count > 0;
        public string PreferredWardId => _def != null ? _def.preferredWardId : string.Empty;
        public Sprite NormalPhoto => _def != null ? _def.normalPhoto : null;
        public Sprite AnomalousPhoto => _def != null ? _def.anomalousPhoto : null;
        public bool IsAnomalous { get; private set; }
        public bool IsOnFire { get; private set; }
        public bool IsAttacking { get; private set; }
        public AnimalHospitalPatientPhase Phase { get; private set; }
        public AnimalHospitalWardStep WardProgress { get; private set; } = AnimalHospitalWardStep.None;
        public AnimalHospitalWard AssignedWard { get; private set; }
        public AnimalHospitalInteractable Interactable => _interactable;
        public float Health => _health;
        public bool IsAlive => _health > 0f && Phase != AnimalHospitalPatientPhase.Gone;

        /// <summary>逻辑根位置：优先 CharacterRoot（与钓鱼 AI ActorTransform 一致）。</summary>
        public Transform ActorTransform => characterRoot != null ? characterRoot : transform;

        public void Initialize(
            AnimalHospitalService service,
            AnimalHospitalPatientCatalogEntry def,
            bool anomalous,
            float maxHealth)
        {
            _service = service;
            _def = def;
            IsAnomalous = anomalous;
            _health = maxHealth;
            ResolveLocomotionComponents();
            EnsureInteractable();
            EnsureEmojiController();
            EnsureInteractionColliderOnActor();
            CacheBodyRenderers();
            AnimalHospitalInteractableLayers.ApplyAiLayer(gameObject);
            Phase = AnimalHospitalPatientPhase.Spawning;
            StopAgentAndIdle();
            AiLog(
                $"Initialize anomalous={anomalous} health={maxHealth:F0} " +
                $"agent={(agent != null ? "ok" : "NULL")} onNavMesh={(agent != null && agent.isOnNavMesh)} " +
                $"patrol={(patrol != null ? "ok" : "NULL")}");
        }

        public void AssignIllness(AnimalHospitalIllnessCatalogEntry illness)
        {
            _remainingMedicineIds.Clear();
            if (illness == null)
            {
                _illnessDescription = "未知病症";
                return;
            }

            _illnessDescription = illness.illnessDescription ?? string.Empty;
            if (illness.requiredMedicineIds != null)
            {
                for (var i = 0; i < illness.requiredMedicineIds.Length; i++)
                {
                    var id = illness.requiredMedicineIds[i];
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    if (!_remainingMedicineIds.Contains(id))
                        _remainingMedicineIds.Add(id);
                }
            }

            AiLog(
                $"AssignIllness '{illness.illnessId}' desc='{_illnessDescription}' " +
                $"meds=[{string.Join(",", _remainingMedicineIds)}]");
        }

        public bool NeedsMedicine(string medicineId)
        {
            if (string.IsNullOrEmpty(medicineId))
                return false;
            return _remainingMedicineIds.Contains(medicineId);
        }

        /// <summary>消耗一种所需药物。若全部用完返回 true（可完成治疗）。</summary>
        public bool TryConsumeMedicine(string medicineId)
        {
            if (string.IsNullOrEmpty(medicineId))
                return false;
            if (!_remainingMedicineIds.Remove(medicineId))
                return false;
            AiLog($"TryConsumeMedicine '{medicineId}' remaining={_remainingMedicineIds.Count}");
            return _remainingMedicineIds.Count == 0;
        }

        public void BeginWaiting(AnimalHospitalWaitingZone waitingZone)
        {
            Phase = AnimalHospitalPatientPhase.Waiting;
            if (waitingZone != null && waitingZone.TryOccupyChair(out var chair))
            {
                _occupiedChair = chair;
                var dest = waitingZone.ResolveChairDestination(chair);
                AiLog($"BeginWaiting → chair '{chair.name}' dest={Fmt(dest)}");
                GoToPoint(dest);
            }
            else
            {
                var dest = waitingZone != null ? waitingZone.GetRandomWanderPoint() : ActorTransform.position;
                AiLog($"BeginWaiting → no chair, wander dest={Fmt(dest)}");
                GoToPoint(dest);
            }
        }

        public void WanderInWaiting(AnimalHospitalWaitingZone waitingZone)
        {
            if (Phase != AnimalHospitalPatientPhase.Waiting || waitingZone == null)
                return;

            if (_occupiedChair != null)
            {
                waitingZone.ReleaseChair(_occupiedChair);
                _occupiedChair = null;
            }

            var dest = waitingZone.GetRandomWanderPoint();
            AiLog($"WanderInWaiting dest={Fmt(dest)}");
            GoToPoint(dest);
        }

        public void GoToReception(Transform receptionPoint)
        {
            if (_service?.WaitingZone != null && _occupiedChair != null)
            {
                _service.WaitingZone.ReleaseChair(_occupiedChair);
                _occupiedChair = null;
            }

            Phase = AnimalHospitalPatientPhase.MovingToReception;

            Vector3 destination;
            if (_service?.ReceptionZone != null)
                destination = _service.ReceptionZone.ResolveReceptionPosition();
            else if (receptionPoint != null)
                destination = AnimalHospitalNavMeshUtility.SampleOrKeep(receptionPoint.position, 20f);
            else
                destination = ActorTransform.position;

            AiLog(
                $"GoToReception raw={(receptionPoint != null ? Fmt(receptionPoint.position) : "null")} " +
                $"resolved={Fmt(destination)}");
            GoToPoint(destination);
        }

        public void ArriveAtReception()
        {
            Phase = AnimalHospitalPatientPhase.AtReception;
            StopAgentAndIdle();
            _curtainWaitElapsed = 0f;
            _interactable?.SetInteractable(false);
            FaceReceptionCamera();
            AiLog("ArriveAtReception");
        }

        public void AssignWard(AnimalHospitalWard ward)
        {
            AssignedWard = ward;
            ward?.Occupy(this);
            Phase = AnimalHospitalPatientPhase.MovingToWard;
            _interactable?.SetInteractable(false);
            ClearMoveSpeedSlow();
            var dest = ward != null
                ? AnimalHospitalNavMeshUtility.SampleOrKeep(ward.EnterPoint.position, 20f)
                : ActorTransform.position;
            AiLog($"AssignWard '{ward?.WardId}' dest={Fmt(dest)}");
            GoToPoint(dest);
        }

        public void ArriveInWard()
        {
            Phase = AnimalHospitalPatientPhase.InWard;
            if (AssignedWard != null)
            {
                WarpTo(AssignedWard.BedPoint.position, AssignedWard.BedPoint.rotation);
            }

            StopAgentAndIdle();
            LieOnBed();
            _interactable?.SetInteractable(false);
            AiLog($"ArriveInWard '{AssignedWard?.WardId}'");
        }

        public void EnableTreatInteractable() => _interactable?.SetInteractable(true);

        public void SetWardProgress(AnimalHospitalWardStep step) => WardProgress = step;

        public void ResetWardProgress() => WardProgress = AnimalHospitalWardStep.None;

        /// <summary>病房手术完成后播放睡眠特效（挂在病人身上）。</summary>
        public void PlaySurgerySleepEffect()
        {
            var prefab = _service != null && _service.Config != null
                ? _service.Config.surgerySleepEffectPrefab
                : null;
            if (prefab == null)
            {
                AiWarn("PlaySurgerySleepEffect: surgerySleepEffectPrefab is null");
                return;
            }

            var actor = ActorTransform;
            var go = Instantiate(prefab, actor);
            go.transform.localPosition = Vector3.up * 0.9f;
            go.transform.localRotation = Quaternion.identity;
            go.name = $"{prefab.name}_SurgerySleepFx";

            var destroyAfter = _service.Config != null
                ? Mathf.Max(0.5f, _service.Config.surgerySleepEffectDestroySeconds)
                : 8f;
            Destroy(go, destroyAfter);
            AiLog($"PlaySurgerySleepEffect '{prefab.name}' destroyAfter={destroyAfter:F1}s");
        }

        public void AttachAdmissionForm(GameObject form)
        {
            if (form == null)
                return;

            var parent = admissionAttachPoint != null
                ? admissionAttachPoint
                : (characterRoot != null ? characterRoot : transform);
            form.transform.SetParent(parent, false);
            form.transform.localPosition = Vector3.zero;
            form.transform.localRotation = Quaternion.identity;

            // 入院单挂到病人后必须停用交互与碰撞，否则准星会一直打到纸上，无法点选病人。
            var formInteractable = form.GetComponent<AnimalHospitalInteractable>();
            formInteractable?.SetInteractable(false);

            var colliders = form.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }

        public void MarkPhotoTaken()
        {
        }

        /// <summary>完成接待（已安排病房）后调用；仅异常病人才会进入发狂倒计时。</summary>
        public void MarkReceptionCompleted()
        {
            if (!IsAnomalous || _receptionCompletedForBerserk)
                return;

            _receptionCompletedForBerserk = true;
            _anomalousBerserkTimer = 0f;
            AiLog("MarkReceptionCompleted → anomalous berserk timer started");
        }

        void CancelAnomalousBerserk()
        {
            _receptionCompletedForBerserk = false;
            _anomalousBerserkTimer = -1f;
        }

        public void LeaveHospital(Transform exitPoint)
        {
            Phase = AnimalHospitalPatientPhase.Leaving;
            ResetWardProgress();
            IsAttacking = false;
            _chaseTarget = null;
            CancelAnomalousBerserk();

            // 先记下床边落地点，再清空病房占用（床上无法寻路）。
            var floorStand = AssignedWard != null ? AssignedWard.EnterPoint : null;
            AssignedWard?.ClearOccupant();
            AssignedWard = null;
            _interactable?.SetInteractable(false);

            StandUpFromBed(floorStand);

            Vector3 destination;
            if (exitPoint != null)
            {
                var exit = exitPoint.GetComponentInParent<AnimalHospitalHospitalExit>();
                destination = exit != null
                    ? exit.ResolveLeavePosition()
                    : AnimalHospitalNavMeshUtility.SampleOrKeep(exitPoint.position, 10f);
            }
            else
            {
                destination = ActorTransform.position;
            }

            AiLog($"LeaveHospital dest={Fmt(destination)}");
            GoToPoint(destination);
        }

        public void SetOnFire(bool onFire)
        {
            IsOnFire = onFire;
            _onFireTimer = onFire ? 0f : -1f;
            if (fireVisual != null)
                fireVisual.SetActive(onFire);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive)
                return;

            _health -= Mathf.Max(0f, amount);
            EnsureEmojiController();
            _emoji?.ShowAttacked();
            AnimalHospitalAudio.Resolve()?.PlayPatientHurt();
            AiLog($"TakeDamage amount={amount:F1} health={_health:F1}");

            if (_health > 0f)
                return;

            DieFromCombat();
        }

        /// <summary>电击器/冷冻枪命中：移动速度降至原速的 30%（降低 70%）。</summary>
        public void ApplyMoveSpeedSlow(float speedMultiplier, float durationSeconds)
        {
            ResolveLocomotionComponents();
            if (agent == null)
                return;

            if (_baseAgentSpeed <= 0f)
                _baseAgentSpeed = Mathf.Max(0.1f, agent.speed);

            agent.speed = _baseAgentSpeed * Mathf.Clamp01(speedMultiplier);
            _slowUntil = Time.time + Mathf.Max(0.1f, durationSeconds);
        }

        void DieFromCombat()
        {
            if (Phase == AnimalHospitalPatientPhase.Gone
                || Phase == AnimalHospitalPatientPhase.Leaving)
                return;

            IsAttacking = false;
            ClearMoveSpeedSlow();
            _interactable?.SetInteractable(false);
            EnsureEmojiController();
            _emoji?.ShowAttackedPersistent();
            AiLog("DieFromCombat → leave hospital with persistent attacked emoji");

            // 不销毁：自行走到出口离开；表情一直保持。
            if (_service != null)
                _service.ForcePatientLeave(this, lost: true);
            else
                LeaveHospital(null);
        }

        void FaceReceptionCamera()
        {
            var cam = _service?.ReceptionZone?.CameraStation;
            if (cam == null)
                return;

            var actor = ActorTransform;
            var toCam = cam.transform.position - actor.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.001f)
                return;

            var rot = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.updateRotation = false;
                agent.transform.rotation = rot;
            }
            else
            {
                actor.rotation = rot;
            }
        }

        void LieOnBed()
        {
            PlayDomainTrigger(lieTrigger);
            var actor = ActorTransform;
            var bed = AssignedWard != null ? AssignedWard.BedPoint : null;
            if (bed == null || actor == null)
                return;

            // 完全停用 NavMesh 控制，否则 Agent 会强制保持直立并每帧改写旋转。
            if (patrol != null)
                patrol.enabled = false;

            if (agent != null)
            {
                if (agent.enabled)
                {
                    agent.isStopped = true;
                    if (agent.isOnNavMesh)
                        agent.ResetPath();
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                    agent.enabled = false;
                }
            }

            var rot = bedLieRelativeToBedPoint
                ? bed.rotation * Quaternion.Euler(bedLieEuler)
                : Quaternion.Euler(bedLieEuler);
            actor.SetPositionAndRotation(bed.position, rot);
            _lyingOnBed = true;
            _liePosePosition = bed.position;
            _liePoseRotation = rot;
            AiLog($"LieOnBed pos={Fmt(bed.position)} euler={bedLieEuler} relative={bedLieRelativeToBedPoint}");
        }

        /// <summary>离开病床：瞬移到床边地面点并恢复直立，再重新启用 NavMeshAgent。</summary>
        void StandUpFromBed(Transform floorPoint)
        {
            ResolveLocomotionComponents();
            var actor = ActorTransform;
            if (actor == null)
                return;

            var standPos = floorPoint != null
                ? AnimalHospitalNavMeshUtility.SampleOrKeep(floorPoint.position, 20f)
                : AnimalHospitalNavMeshUtility.SampleOrKeep(actor.position, 20f);

            var yaw = floorPoint != null
                ? floorPoint.rotation.eulerAngles.y
                : actor.eulerAngles.y;
            var upright = Quaternion.Euler(0f, yaw, 0f);

            if (patrol != null)
                patrol.enabled = false;

            if (agent != null)
            {
                agent.enabled = true;
                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.isStopped = true;
                if (!AnimalHospitalNavMeshUtility.TrySamplePathStart(agent, standPos, 20f, out standPos))
                    agent.Warp(standPos);
                agent.transform.rotation = upright;
            }
            else
            {
                actor.SetPositionAndRotation(standPos, upright);
            }

            _lyingOnBed = false;
            AiLog($"StandUpFromBed pos={Fmt(standPos)}");
        }

        void LateUpdate()
        {
            // 防止其它系统（动画/Agent 残帧）把躺姿改回直立。
            if (!_lyingOnBed || Phase != AnimalHospitalPatientPhase.InWard)
                return;

            var actor = ActorTransform;
            if (actor == null)
                return;

            if (agent != null && agent.enabled)
            {
                agent.updatePosition = false;
                agent.updateRotation = false;
                agent.enabled = false;
            }

            actor.SetPositionAndRotation(_liePosePosition, _liePoseRotation);
        }

        void ClearMoveSpeedSlow()
        {
            _slowUntil = -1f;
            if (agent != null && _baseAgentSpeed > 0f)
                agent.speed = _baseAgentSpeed;
        }

        void TickMoveSpeedSlow()
        {
            if (_slowUntil < 0f || agent == null)
                return;

            if (Time.time < _slowUntil)
                return;

            ClearMoveSpeedSlow();
        }

        public void NotifyTreatmentComplete()
        {
            _emoji?.ShowTreatmentComplete();
            PlayTreatmentEffect();
        }

        /// <summary>治疗后继续躺床，延迟再离开医院。</summary>
        public void BeginPostTreatmentLeave(Transform exitPoint, float bedRestSeconds)
        {
            if (_postTreatmentLeaveRoutine != null)
                StopCoroutine(_postTreatmentLeaveRoutine);
            _postTreatmentLeaveRoutine = StartCoroutine(PostTreatmentLeaveRoutine(exitPoint, bedRestSeconds));
        }

        IEnumerator PostTreatmentLeaveRoutine(Transform exitPoint, float bedRestSeconds)
        {
            var wait = Mathf.Max(0f, bedRestSeconds);
            if (wait > 0f)
                yield return new WaitForSeconds(wait);

            _postTreatmentLeaveRoutine = null;
            LeaveHospital(exitPoint);
        }

        void PlayTreatmentEffect()
        {
            var prefab = _service != null && _service.Config != null
                ? _service.Config.treatmentEffectPrefab
                : null;
            if (prefab == null)
                return;

            var actor = ActorTransform;
            var go = Instantiate(prefab, actor);
            go.transform.localPosition = Vector3.up * 0.9f;
            go.transform.localRotation = Quaternion.identity;
            go.name = $"{prefab.name}_TreatmentFx";

            var destroyAfter = _service.Config != null
                ? Mathf.Max(0.5f, _service.Config.treatmentEffectDestroySeconds)
                : 3.5f;
            Destroy(go, destroyAfter);
        }

        void Awake()
        {
            ResolveLocomotionComponents();
            EnsureEmojiController();
            EnsureInteractionColliderOnActor();
            AnimalHospitalInteractableLayers.ApplyAiLayer(gameObject);
        }

        void Update()
        {
            if (_service == null || Phase == AnimalHospitalPatientPhase.Gone)
                return;

            if (IsAttacking)
                TickAttackChase();
            else if (_movingToPoint)
                TickPointMove();

            TickMoveSpeedSlow();
            UpdateCurtainLeave();
            UpdateAnomalousAttack();
            UpdateOnFire();
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
            if (animator == null)
                animator = patrol != null
                    ? patrol.GetComponent<Animator>()
                    : GetComponentInChildren<Animator>(true);

            if (characterRoot == null && agent != null)
                characterRoot = agent.transform;

            // 医院病人默认不巡逻，由状态机下发目的地（与钓鱼 ApproachingSpot 相同）。
            if (patrol != null)
                patrol.enabled = false;
        }

        void EnsureInteractable()
        {
            _interactable = GetComponent<AnimalHospitalInteractable>()
                            ?? gameObject.AddComponent<AnimalHospitalInteractable>();
            _interactable.Configure(AnimalHospitalInteractableKind.Patient, PatientId, DisplayName);
            _interactable.SetInteractable(false);
        }

        /// <summary>
        /// NavMeshAgent 在 CharacterRoot 上移动，父物体碰撞体不会跟着走。
        /// 必须把交互碰撞体挂到 ActorTransform，否则准星对准模型却射线打不中。
        /// </summary>
        void EnsureInteractionColliderOnActor()
        {
            ResolveLocomotionComponents();
            var actor = ActorTransform;
            if (actor == null)
                return;

            // 禁用根节点上落后的物理碰撞体，避免打到旧位置。
            var rootColliders = GetComponents<Collider>();
            for (var i = 0; i < rootColliders.Length; i++)
            {
                if (rootColliders[i] != null && rootColliders[i].transform != actor)
                    rootColliders[i].enabled = false;
            }

            var capsule = actor.GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = actor.gameObject.AddComponent<CapsuleCollider>();

            capsule.enabled = true;
            capsule.isTrigger = false;
            capsule.direction = 1;
            capsule.height = 2f;
            capsule.radius = 0.4f;
            capsule.center = new Vector3(0f, 1f, 0f);
            AnimalHospitalInteractableLayers.ApplyAiLayer(actor.gameObject);
        }

        void EnsureEmojiController()
        {
            _emoji = GetComponent<AnimalHospitalPatientEmoji>();
            if (_emoji == null)
                _emoji = gameObject.AddComponent<AnimalHospitalPatientEmoji>();

            _emoji.ApplyConfig(_service != null ? _service.Config : null);
        }

        public void PlaceOnNavMeshNear(Vector3 worldPosition, float maxDistance = AnimalHospitalNavMeshUtility.DefaultSampleRadius)
        {
            ResolveLocomotionComponents();
            if (agent == null)
            {
                ActorTransform.position = AnimalHospitalNavMeshUtility.SampleOrKeep(worldPosition, maxDistance);
                return;
            }

            AnimalHospitalNavMeshUtility.TrySamplePathStart(agent, worldPosition, maxDistance, out _);
        }

        public void EnsureNavMeshNearOr(Vector3 fallbackNear, float maxDistance = AnimalHospitalNavMeshUtility.DefaultSampleRadius)
        {
            ResolveLocomotionComponents();
            if (agent == null || agent.isOnNavMesh)
                return;

            PlaceOnNavMeshNear(fallbackNear, maxDistance);
        }

        public bool IsAgentOnNavMesh => agent != null && agent.isOnNavMesh;

        /// <summary>点对点移动：禁用巡逻，直接 SetDestination（对齐 AIFakeFishingBehavior 接近钓点）。</summary>
        void GoToPoint(Vector3 worldPosition)
        {
            _movingToPoint = true;
            IsAttacking = false;
            _chaseTarget = null;
            _pathRetryCooldown = 0f;
            // 接待点/门口/椅子锚点可能不在 NavMesh 上，使用更大半径强制贴地以避免“永远 invalid”。
            _moveDestination = AnimalHospitalNavMeshUtility.SampleOrKeep(worldPosition, 20f);

            if (patrol != null)
                patrol.enabled = false;

            if (agent == null)
            {
                AiWarn($"GoToPoint no NavMeshAgent → teleport {Fmt(_moveDestination)}");
                ActorTransform.position = _moveDestination;
                _movingToPoint = false;
                patrol?.SetLocomotionAnimatorIdle();
                return;
            }

            AiLog(
                $"GoToPoint request={Fmt(worldPosition)} sampled={Fmt(_moveDestination)} " +
                $"onNavMesh={agent.isOnNavMesh} stopping={agent.stoppingDistance:F2}");

            if (!TryBeginAgentMove(_moveDestination))
            {
                AiWarn("GoToPoint SetDestination failed (not on NavMesh yet), will retry");
                _pathRetryCooldown = 0.35f;
            }
        }

        void TickPointMove()
        {
            if (agent == null)
            {
                _movingToPoint = false;
                return;
            }

            patrol?.RefreshLocomotionAnimator();
            LogMoveStatusIfNeeded();

            if (!agent.isOnNavMesh)
            {
                _pathRetryCooldown -= Time.deltaTime;
                if (_pathRetryCooldown <= 0f)
                {
                    _pathRetryCooldown = 0.35f;
                    AiWarn($"retry move: agent off NavMesh → rewarp+SetDestination {Fmt(_moveDestination)}");
                    TryBeginAgentMove(_moveDestination);
                }

                return;
            }

            // pathPending 时 pathStatus 常为 PathInvalid，不能在此 ResetPath，否则永远走不起来。
            if (agent.pathPending)
                return;

            // 空路径卡死：PathComplete + 无路径 + rem≈0，但水平距离仍很远。
            var planarToDest = ActorTransform.position - _moveDestination;
            planarToDest.y = 0f;
            var stuckEmptyPath = !agent.hasPath
                                 && agent.remainingDistance <= 0.05f
                                 && planarToDest.sqrMagnitude > 1f;
            if (stuckEmptyPath || agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                _pathRetryCooldown -= Time.deltaTime;
                if (_pathRetryCooldown <= 0f)
                {
                    _pathRetryCooldown = 0.5f;
                    _moveDestination = AnimalHospitalNavMeshUtility.SampleOrKeep(_moveDestination, 24f);
                    AiWarn(
                        $"retry move: {(stuckEmptyPath ? "empty-path stuck" : "PathInvalid")} → " +
                        $"resample dest={Fmt(_moveDestination)}");
                    TryBeginAgentMove(_moveDestination);
                }

                return;
            }

            if (!HasReachedDestination())
                return;

            _movingToPoint = false;
            OnReachedCurrentDestination();
        }

        bool TryBeginAgentMove(Vector3 destination)
        {
            if (agent == null)
                return false;

            if (!agent.enabled)
                agent.enabled = true;

            // 生成后同帧必须重新 Warp 绑定，否则 SetDestination 常会留下空路径：
            // PathComplete + hasPath=false + rem=0，表现为原地不动。
            if (!AnimalHospitalNavMeshUtility.TrySamplePathStart(
                    agent,
                    ActorTransform.position,
                    12f,
                    out _))
            {
                AiWarn("TryBeginAgentMove: failed to bind agent onto NavMesh");
                return false;
            }

            if (!agent.isOnNavMesh)
            {
                AiWarn("TryBeginAgentMove: still off NavMesh after Warp");
                return false;
            }

            if (AnimalHospitalNavMeshUtility.TrySetPath(agent, destination, out var status, out var corners))
            {
                AiLog($"TryBeginAgentMove OK pathStatus={status} corners={corners} dest={Fmt(destination)}");
                return true;
            }

            // 回退：异步 SetDestination（部分场景 CalculatePath 过严时仍可走）。
            agent.isStopped = false;
            agent.ResetPath();
            var set = agent.SetDestination(destination);
            AiWarn(
                $"TryBeginAgentMove CalculatePath failed (status={status}, corners={corners}) → " +
                $"SetDestination={set} dest={Fmt(destination)}");
            return set;
        }

        void OnReachedCurrentDestination()
        {
            var planar = ActorTransform.position - _moveDestination;
            planar.y = 0f;
            AiLog(
                $"ReachedDestination phase={Phase} distXZ={planar.magnitude:F2} " +
                $"remaining={(agent != null ? agent.remainingDistance : -1f):F2} " +
                $"hasPath={(agent != null && agent.hasPath)} pathStatus={(agent != null ? agent.pathStatus.ToString() : "n/a")}");

            switch (Phase)
            {
                case AnimalHospitalPatientPhase.MovingToReception:
                    ArriveAtReception();
                    _service.NotifyPatientArrivedReception(this);
                    break;
                case AnimalHospitalPatientPhase.MovingToWard:
                    ArriveInWard();
                    _service.NotifyPatientArrivedWard(this);
                    break;
                case AnimalHospitalPatientPhase.Leaving:
                    Phase = AnimalHospitalPatientPhase.Gone;
                    AiLog("Left hospital → Gone");
                    _service.NotifyPatientLeft(this);
                    Destroy(gameObject);
                    break;
                case AnimalHospitalPatientPhase.Waiting:
                    AiLog("Waiting move finished → idle");
                    StopAgentAndIdle();
                    break;
                default:
                    StopAgentAndIdle();
                    break;
            }
        }

        /// <summary>异常攻击：启用巡逻组件的 Chase 模式（对齐 NavMeshAIPatrolAgent.SetModeChase）。</summary>
        void BeginAttacking()
        {
            IsAttacking = true;
            _movingToPoint = false;
            CancelAnomalousBerserk();
            ApplyAnomalousVisuals();

            if (_lyingOnBed)
                StandUpFromBed(AssignedWard != null ? AssignedWard.EnterPoint : null);

            PlayDomainTrigger(attackTrigger);
            AiLog("BeginAttacking");

            if (patrol != null)
            {
                patrol.enabled = true;
                RefreshChaseTarget();
                if (_chaseTarget != null)
                    patrol.SetModeChase(_chaseTarget);
            }
        }

        void TickAttackChase()
        {
            if (!IsAlive)
                return;

            RefreshChaseTarget();
            if (_chaseTarget == null)
                return;

            if (patrol != null && patrol.enabled)
                patrol.SetModeChase(_chaseTarget);
            else if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(_chaseTarget.position);
                patrol?.RefreshLocomotionAnimator();
            }

            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown > 0f)
                return;

            var dist = Vector3.Distance(ActorTransform.position, _chaseTarget.position);
            if (dist > attackRange)
                return;

            _attackCooldown = ResolveAttackIntervalSeconds();
            ApplyAttackHit(_chaseTarget);
        }

        float ResolveAttackIntervalSeconds()
        {
            if (_service?.Config != null)
                return Mathf.Max(0.5f, _service.Config.anomalousAttackIntervalSeconds);
            return 5f;
        }

        void RefreshChaseTarget()
        {
            var player = _service.PlayerTransform;
            var nearestPatient = _service.FindNearestOtherPatient(this);
            Transform best = null;
            var bestDist = float.MaxValue;

            if (player != null)
            {
                bestDist = Vector3.Distance(ActorTransform.position, player.position);
                best = player;
            }

            if (nearestPatient != null)
            {
                var d = Vector3.Distance(ActorTransform.position, nearestPatient.ActorTransform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = nearestPatient.ActorTransform;
                }
            }

            _chaseTarget = best;
        }

        void ApplyAttackHit(Transform target)
        {
            var player = _service.PlayerTransform;
            if (player != null && target == player)
            {
                _service.Sanity.ApplyDamage(_service.Config != null
                    ? _service.Config.anomalousAttackSanityLoss
                    : 5f);
                return;
            }

            var other = target.GetComponentInParent<AnimalHospitalPatient>();
            if (other != null && other != this)
            {
                other.TakeDamage(_service.Config != null
                    ? _service.Config.propAttackDamage
                    : 5f);
            }
        }

        void UpdateCurtainLeave()
        {
            if (Phase != AnimalHospitalPatientPhase.AtReception)
                return;

            var reception = _service.ReceptionZone;
            if (reception == null || !reception.CurtainClosed)
            {
                _curtainWaitElapsed = 0f;
                return;
            }

            _curtainWaitElapsed += Time.deltaTime;
            var wait = _service.Config != null ? _service.Config.curtainLeaveWaitSeconds : 15f;
            if (_curtainWaitElapsed >= wait)
                _service.ForcePatientLeave(this, lost: true);
        }

        void UpdateAnomalousAttack()
        {
            if (!IsAnomalous || !_receptionCompletedForBerserk || _anomalousBerserkTimer < 0f || IsAttacking)
                return;

            if (Phase != AnimalHospitalPatientPhase.MovingToWard
                && Phase != AnimalHospitalPatientPhase.InWard)
                return;

            _anomalousBerserkTimer += Time.deltaTime;
            var delay = _service.Config != null
                ? _service.Config.anomalousAttackDelayAfterReceptionSeconds
                : 0f;
            if (_anomalousBerserkTimer >= delay)
                BeginAttacking();
        }

        void CacheBodyRenderers()
        {
            _bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (_bodyRenderers == null || _bodyRenderers.Length == 0)
                return;

            _normalBodyMaterials = new Material[_bodyRenderers.Length][];
            for (var i = 0; i < _bodyRenderers.Length; i++)
            {
                var renderer = _bodyRenderers[i];
                if (renderer == null)
                    continue;
                _normalBodyMaterials[i] = renderer.sharedMaterials;
            }
        }

        void ApplyAnomalousVisuals()
        {
            if (_anomalousVisualApplied)
                return;

            var mat = _def != null ? _def.anomalousMaterial : null;
            if (mat == null || _bodyRenderers == null)
                return;

            for (var i = 0; i < _bodyRenderers.Length; i++)
            {
                var renderer = _bodyRenderers[i];
                if (renderer == null)
                    continue;

                var mats = renderer.sharedMaterials;
                for (var j = 0; j < mats.Length; j++)
                    mats[j] = mat;
                renderer.sharedMaterials = mats;
            }

            _anomalousVisualApplied = true;
            AiLog($"ApplyAnomalousVisuals material='{mat.name}'");
        }

        void UpdateOnFire()
        {
            if (!IsOnFire || _onFireTimer < 0f)
                return;

            _onFireTimer += Time.deltaTime;
            var duration = _service.Config != null
                ? _service.Config.patientOnFireDurationSeconds
                : 10f;
            if (_onFireTimer >= duration)
            {
                SetOnFire(false);
                _service.ForcePatientLeave(this, lost: true);
            }
        }

        void StopAgentAndIdle()
        {
            _movingToPoint = false;
            if (patrol != null)
                patrol.enabled = false;

            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                if (agent.isOnNavMesh)
                    agent.ResetPath();
            }

            patrol?.SetLocomotionAnimatorIdle();
        }

        void WarpTo(Vector3 position, Quaternion rotation)
        {
            if (agent != null && agent.enabled)
            {
                EnsureAgentOnNavMesh();
                agent.Warp(position);
                agent.transform.rotation = rotation;
            }
            else
            {
                ActorTransform.SetPositionAndRotation(position, rotation);
            }
        }

        void EnsureAgentOnNavMesh()
        {
            if (agent == null)
                return;

            if (agent.isOnNavMesh)
                return;

            AnimalHospitalNavMeshUtility.TrySamplePathStart(
                agent,
                ActorTransform.position,
                12f,
                out _);
        }

        bool HasReachedDestination()
        {
            if (agent == null || !agent.enabled)
                return true;

            if (!agent.isOnNavMesh)
                return false;

            if (agent.pathPending)
                return false;

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                return false;

            var arrivalRadius = Mathf.Max(agent.stoppingDistance, 0.05f) +
                                Mathf.Max(0f, arrivalExtraToleranceMeters);

            // 必须以水平距离为准：无路径 + remainingDistance≈0 时不能当成“已到达”，
            // 否则病人会在原地被误判进接待态，后续排队只会闲逛。
            var toDest = ActorTransform.position - _moveDestination;
            toDest.y = 0f;
            if (toDest.sqrMagnitude > arrivalRadius * arrivalRadius)
                return false;

            if (agent.hasPath && agent.remainingDistance > arrivalRadius)
                return false;

            return agent.velocity.sqrMagnitude <= 0.05f;
        }

        void PlayDomainTrigger(string trigger)
        {
            if (animator == null || string.IsNullOrEmpty(trigger))
                return;

            animator.SetTrigger(trigger);
        }

        void LogMoveStatusIfNeeded()
        {
            if (!CanAiLog || !_movingToPoint || agent == null)
                return;

            if (Time.time < _nextMoveStatusLogTime)
                return;

            _nextMoveStatusLogTime = Time.time + 1f;
            var planar = ActorTransform.position - _moveDestination;
            planar.y = 0f;
            var key =
                $"{agent.isOnNavMesh}|{agent.pathPending}|{agent.pathStatus}|{agent.hasPath}|{(planar.magnitude > 2f ? "far" : "near")}";
            if (key == _lastMoveLogKey)
                return;

            _lastMoveLogKey = key;
            AiLog(
                $"move-status onNav={agent.isOnNavMesh} pending={agent.pathPending} " +
                $"status={agent.pathStatus} hasPath={agent.hasPath} rem={agent.remainingDistance:F2} " +
                $"distXZ={planar.magnitude:F2} vel={agent.velocity.magnitude:F2} dest={Fmt(_moveDestination)}");
        }

        bool CanAiLog => AnimalHospitalAiDebug.Enabled && aiDebugLogging;

        void AiLog(string message)
        {
            if (!CanAiLog)
                return;

            AnimalHospitalAiDebug.Log(this, message);
        }

        void AiWarn(string message)
        {
            if (!CanAiLog)
                return;

            AnimalHospitalAiDebug.Warn(this, message);
        }

        static string Fmt(Vector3 v) => $"({v.x:F1},{v.y:F1},{v.z:F1})";
    }
}
