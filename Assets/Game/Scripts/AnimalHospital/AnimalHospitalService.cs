using System;
using System.Collections.Generic;
using Game.AnimalHospital.Config;
using Game.Audio;
using Game.Save;
using Game.UI.Framework;
using Game.UI.ViewModels;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>动物医院玩法核心：日程、接待/病房流程、道具与事件。</summary>
    public sealed class AnimalHospitalService
    {
        public event Action GameStarted;
        public event Action DaySettlementRequested;
        public event Action SanityDepleted;
        public event Action<string> ToastRequested;
        public event Action<AnimalHospitalReceptionStep> ReceptionStepChanged;
        public event Action<AnimalHospitalWardStep> WardStepChanged;
        public event Action<string> HeldMedicineChanged;

        readonly AnimalHospitalConfig _config;
        readonly AnimalHospitalHighlightController _highlights;
        readonly List<AnimalHospitalPatient> _patients = new List<AnimalHospitalPatient>();
        readonly List<int> _spawnedArrivalIndices = new List<int>();
        readonly HashSet<AnimalHospitalWard> _playerWardCache = new HashSet<AnimalHospitalWard>();

        AnimalHospitalPatient _receptionPatient;
        /// <summary>已派往接待区但尚未到达的病人（占用接待名额，避免第二人同时被派去接待）。</summary>
        AnimalHospitalPatient _receptionClaimedPatient;
        AnimalHospitalPatient _focusWardPatient;
        AnimalHospitalReceptionStep _receptionStep = AnimalHospitalReceptionStep.None;
        AnimalHospitalWardStep _wardStep = AnimalHospitalWardStep.None;
        AnimalHospitalAdmissionForm _spawnedAdmission;
        GameObject _spawnedReport;
        string _heldMedicineId;
        float _dayElapsed;
        float _coffeeCooldownLeft;
        float _sanityPassiveDrainAccum;
        int _treatedToday;
        int _lostToday;
        int _anomalousPhotographedToday;
        int _anomalousHandledToday;
        int _anomalyEventsToday;
        int _plannedPatientCount;
        float[] _arrivalTimesSeconds = Array.Empty<float>();
        bool _running;
        bool _dayCompletedRaised;
        bool _sanityDepletedRaised;

        public AnimalHospitalConfig Config => _config;
        public AnimalHospitalSanityService Sanity { get; }
        public AnimalHospitalPropInventory Props { get; } = new AnimalHospitalPropInventory();
        public AnimalHospitalEventManager Events { get; private set; }
        public AnimalHospitalWaitingZone WaitingZone { get; private set; }
        public AnimalHospitalReceptionZone ReceptionZone { get; private set; }
        public AnimalHospitalWard[] Wards { get; private set; } = Array.Empty<AnimalHospitalWard>();
        public AnimalHospitalCorridorZone CorridorZone { get; private set; }
        public AnimalHospitalHospitalExit HospitalExit { get; private set; }
        public AnimalHospitalMedicineCabinet MedicineCabinet { get; private set; }
        public Transform PlayerTransform { get; private set; }
        public AnimalHospitalFirstPersonCamera FirstPersonCamera { get; private set; }
        public bool IsGameRunning => _running;
        public float DayElapsed => _dayElapsed;
        public string HeldMedicineId => _heldMedicineId;
        public AnimalHospitalReceptionStep ReceptionStep => _receptionStep;
        public AnimalHospitalWardStep WardStep => _wardStep;
        public AnimalHospitalPatient ReceptionPatient => _receptionPatient;
        public int TreatedToday => _treatedToday;
        public int LostToday => _lostToday;
        public int AnomalousPhotographedToday => _anomalousPhotographedToday;
        public int AnomalousHandledToday => _anomalousHandledToday;
        public int AnomalyEventsToday => _anomalyEventsToday;
        public int PlannedPatientCount => _plannedPatientCount;
        public float CoffeeCooldownLeft => _coffeeCooldownLeft;
        public int CurrentSurvivalDay =>
            Mathf.Max(1, SaveService.Data?.animalHospital?.currentDay ?? 1);

        public AnimalHospitalService(
            AnimalHospitalConfig config,
            AnimalHospitalHighlightController highlights,
            AnimalHospitalModeSaveData save)
        {
            _config = config;
            _highlights = highlights;
            Sanity = AnimalHospitalSanityService.FromSave(save, config);
            Events = new AnimalHospitalEventManager(this);
        }

        public void BindScene(
            AnimalHospitalWaitingZone waiting,
            AnimalHospitalReceptionZone reception,
            AnimalHospitalWard[] wards,
            AnimalHospitalCorridorZone corridor,
            AnimalHospitalHospitalExit exit,
            AnimalHospitalMedicineCabinet cabinet,
            Transform player,
            AnimalHospitalFirstPersonCamera fpCamera)
        {
            WaitingZone = waiting;
            ReceptionZone = reception;
            Wards = wards ?? Array.Empty<AnimalHospitalWard>();
            CorridorZone = corridor;
            HospitalExit = exit;
            MedicineCabinet = cabinet;
            PlayerTransform = player;
            FirstPersonCamera = fpCamera;
        }

        public void StartDay()
        {
            _running = true;
            _dayElapsed = 0f;
            _sanityPassiveDrainAccum = 0f;
            _treatedToday = 0;
            _lostToday = 0;
            _anomalousPhotographedToday = 0;
            _anomalousHandledToday = 0;
            _anomalyEventsToday = 0;
            _dayCompletedRaised = false;
            _sanityDepletedRaised = false;
            _spawnedArrivalIndices.Clear();
            _coffeeCooldownLeft = 0f;
            BuildArrivalSchedule();
            ClearReceptionFlow();
            // 每天开局按配置重置，避免存档里的旧理智（常为 100）覆盖 startingSanity。
            Sanity.Changed -= OnSanityChanged;
            Sanity.Changed += OnSanityChanged;
            Sanity.ResetForNewDay(_config);
            AnimalHospitalAudio.Resolve()?.BeginGameplayAudio();
            GameStarted?.Invoke();
            AnimalHospitalAiDebug.Log(
                $"StartDay survivalDay={CurrentSurvivalDay} plannedPatients={_plannedPatientCount} " +
                $"anomalyChance={ResolveAnomalyChance():P0}");
        }

        public void ShowToast(string message) => ToastRequested?.Invoke(message);

        public void StopDay()
        {
            _running = false;
            Sanity.Changed -= OnSanityChanged;
            AnimalHospitalAudio.Resolve()?.EndGameplayAudio();
            Sanity.Persist(SaveService.Data?.animalHospital);
            SaveService.Save();
        }

        void OnSanityChanged(float current, float max)
        {
            if (_sanityDepletedRaised || current > 0f)
                return;

            _sanityDepletedRaised = true;
            _running = false;
            SanityDepleted?.Invoke();
        }

        /// <summary>理智归零恢复后继续本日。</summary>
        public void ResumeAfterSanityRecover()
        {
            _sanityDepletedRaised = false;
            if (!_dayCompletedRaised)
                _running = true;
        }

        public AnimalHospitalResultViewModel BuildDayResultModel()
        {
            var treated = _treatedToday;
            var handled = _anomalousHandledToday;
            var coinsPerTreat = _config != null ? _config.coinsPerTreatedPatient : 10;
            var coinsPerAnomaly = _config != null ? _config.coinsPerHandledAnomaly : 10;
            return new AnimalHospitalResultViewModel
            {
                SurvivalDay = CurrentSurvivalDay,
                PlannedPatientCount = _plannedPatientCount,
                TreatedCount = treated,
                AnomalousPhotographedCount = _anomalousPhotographedToday,
                AnomalousHandledCount = handled,
                PerformanceGrade = ResolvePerformanceGrade(treated, handled),
                CoinsEarned = treated * coinsPerTreat + handled * coinsPerAnomaly,
            };
        }

        public static string ResolvePerformanceGrade(int treated, int anomaliesHandled)
        {
            if (treated >= 2 && anomaliesHandled >= 2)
                return "完美";
            if (treated >= 2)
                return "优秀";
            return "普通";
        }

        public float ResolveAnomalyChance()
        {
            var day = CurrentSurvivalDay;
            if (day <= 1)
                return 0f;
            // 第 2 天 20%，之后每天 +10%，最高 50%。
            return Mathf.Min(0.5f, 0.2f + (day - 2) * 0.1f);
        }

        public bool CanTriggerAnomalyEvent()
        {
            if (CurrentSurvivalDay <= 1)
                return false;
            var max = _config != null ? Mathf.Max(0, _config.maxAnomalyEventsPerDay) : 3;
            return _anomalyEventsToday < max;
        }

        public bool TryConsumeAnomalyEventSlot()
        {
            if (!CanTriggerAnomalyEvent())
                return false;
            if (UnityEngine.Random.value >= ResolveAnomalyChance())
                return false;
            _anomalyEventsToday++;
            return true;
        }

        public void NotifyAnomalousPhotoTaken() => _anomalousPhotographedToday++;

        void BuildArrivalSchedule()
        {
            var minCount = _config != null ? Mathf.Max(1, _config.patientsPerDayMin) : 4;
            var maxCount = _config != null ? Mathf.Max(minCount, _config.patientsPerDayMax) : 12;
            var intervalMin = _config != null
                ? Mathf.Max(1f, _config.patientArrivalIntervalMinSeconds)
                : 30f;
            var intervalMax = _config != null
                ? Mathf.Max(intervalMin, _config.patientArrivalIntervalMaxSeconds)
                : 60f;

            _plannedPatientCount = UnityEngine.Random.Range(minCount, maxCount + 1);
            _arrivalTimesSeconds = new float[_plannedPatientCount];
            var firstMax = _config != null
                ? Mathf.Max(0.1f, _config.firstPatientArrivalMaxSeconds)
                : 10f;
            var t = UnityEngine.Random.Range(0f, firstMax);
            for (var i = 0; i < _plannedPatientCount; i++)
            {
                _arrivalTimesSeconds[i] = t;
                if (i < _plannedPatientCount - 1)
                    t += UnityEngine.Random.Range(intervalMin, intervalMax);
            }
        }

        public void Tick(float dt)
        {
            if (!_running)
                return;

            _dayElapsed += dt;
            if (_coffeeCooldownLeft > 0f)
                _coffeeCooldownLeft = Mathf.Max(0f, _coffeeCooldownLeft - dt);

            TickPassiveSanityDrain(dt);
            TickLowSanityHeartbeat();
            TickArrivals();
            TickReceptionQueue();
            Events.Tick(dt);
            RefreshPlayerWardOccupancy();
        }

        void TickLowSanityHeartbeat()
        {
            var threshold = _config != null ? _config.lowSanityHeartbeatNormalized : 0.5f;
            var low = Sanity.Normalized < threshold;
            AnimalHospitalAudio.Resolve()?.SetLowSanityHeartbeat(low);
        }

        void TickPassiveSanityDrain(float dt)
        {
            var interval = _config != null ? _config.sanityPassiveDrainIntervalSeconds : 10f;
            var amount = _config != null ? _config.sanityPassiveDrainAmount : 1f;
            if (interval <= 0f || amount <= 0f)
                return;

            _sanityPassiveDrainAccum += dt;
            while (_sanityPassiveDrainAccum >= interval)
            {
                _sanityPassiveDrainAccum -= interval;
                Sanity.ApplyDamage(amount);
            }
        }

        public void HandleInteractableCompleted(AnimalHospitalInteractable interactable)
        {
            if (interactable == null || !_running)
                return;

            AnimalHospitalAudio.Resolve()?.PlayInteractComplete();

            switch (interactable.Kind)
            {
                case AnimalHospitalInteractableKind.Report:
                    AnimalHospitalAudio.Resolve()?.SetReceptionWaitLoop(false);
                    AdvanceReceptionTo(AnimalHospitalReceptionStep.Camera);
                    break;
                case AnimalHospitalInteractableKind.Camera:
                    HandleCameraInteract();
                    break;
                case AnimalHospitalInteractableKind.Computer:
                    HandleReceptionComputerInteract();
                    break;
                case AnimalHospitalInteractableKind.Printer:
                    HandlePrinterInteract();
                    break;
                case AnimalHospitalInteractableKind.AdmissionForm:
                    HandleAdmissionInteract(interactable as AnimalHospitalAdmissionForm);
                    break;
                case AnimalHospitalInteractableKind.Patient:
                    HandlePatientInteract(interactable);
                    break;
                case AnimalHospitalInteractableKind.Analyzer:
                    if (!TryExtinguishFromStation(interactable))
                        HandleAnalyzerInteract(interactable);
                    break;
                case AnimalHospitalInteractableKind.WardComputer:
                    if (!TryExtinguishFromStation(interactable))
                        HandleWardComputer(interactable);
                    break;
                case AnimalHospitalInteractableKind.WardDisplay:
                    if (!TryExtinguishFromStation(interactable))
                        HandleWardDisplayInteract(interactable);
                    break;
                case AnimalHospitalInteractableKind.Medicine:
                    if (interactable is AnimalHospitalMedicineInteractable med)
                        HandleMedicinePickup(med);
                    break;
                case AnimalHospitalInteractableKind.CoffeeMachine:
                    HandleCoffee();
                    break;
                case AnimalHospitalInteractableKind.DaySettlement:
                    HandleDaySettlement();
                    break;
                case AnimalHospitalInteractableKind.Prop:
                    if (interactable is AnimalHospitalPropInteractable prop)
                        HandlePropPickup(prop);
                    break;
                case AnimalHospitalInteractableKind.Curtain:
                    HandleCurtain();
                    break;
                case AnimalHospitalInteractableKind.FireTarget:
                    TryExtinguishNear();
                    break;
                case AnimalHospitalInteractableKind.Slime:
                    HandleSlime(interactable);
                    break;
                case AnimalHospitalInteractableKind.Shadow:
                    HandleShadowAttack(interactable);
                    break;
            }
        }

        public void NotifyPatientArrivedReception(AnimalHospitalPatient patient)
        {
            if (patient == null)
                return;

            _receptionPatient = patient;
            _receptionClaimedPatient = patient;
            AnimalHospitalAiDebug.Log(patient, "service: NotifyPatientArrivedReception → BeginReceptionFlow");
            ReceptionZone?.PlayReceptionBell();
            AnimalHospitalAudio.Resolve()?.SetReceptionWaitLoop(true);
            BeginReceptionFlow(patient);
        }

        public void NotifyPatientArrivedWard(AnimalHospitalPatient patient)
        {
            if (patient == null || patient.AssignedWard == null)
                return;

            patient.SetWardProgress(AnimalHospitalWardStep.Surgery);
            RefreshWardStationState(patient.AssignedWard);
            AnimalHospitalAiDebug.Log(patient, $"service: arrived ward '{patient.AssignedWard.WardId}'");

            if (ShouldAutoFocusNewWardPatient(patient))
                FocusWardPatient(patient, announce: true);
            else
                ToastRequested?.Invoke($"{patient.DisplayName} 已进入 {patient.AssignedWard.WardId}。");

            Events.OnPatientEnteredWard(patient, patient.AssignedWard);
        }

        bool ShouldAutoFocusNewWardPatient(AnimalHospitalPatient incoming)
        {
            if (_focusWardPatient == null || _focusWardPatient == incoming)
                return true;

            if (_focusWardPatient.Phase != AnimalHospitalPatientPhase.InWard)
                return true;

            return _focusWardPatient.WardProgress == AnimalHospitalWardStep.Completed;
        }

        public void NotifyPatientLeft(AnimalHospitalPatient patient)
        {
            if (patient != null && patient.IsAnomalous)
                _anomalousHandledToday++;

            _patients.Remove(patient);
            if (_receptionClaimedPatient == patient)
                _receptionClaimedPatient = null;
            if (_receptionPatient == patient)
                ClearReceptionFlow();
            if (_focusWardPatient == patient)
            {
                _focusWardPatient = null;
                _wardStep = AnimalHospitalWardStep.None;
                TryFocusNextWardPatient();
            }

            RefreshWardStationState(patient?.AssignedWard);
        }

        public void NotifyPatientDefeated(AnimalHospitalPatient patient)
        {
            _lostToday++;
            NotifyPatientLeft(patient);
        }

        public void ForcePatientLeave(AnimalHospitalPatient patient, bool lost)
        {
            if (patient == null)
                return;

            if (lost)
            {
                _lostToday++;
                var save = SaveService.Data?.animalHospital;
                if (save != null)
                    save.patientsLostTotal++;
            }

            if (HospitalExit != null)
                patient.LeaveHospital(HospitalExit.LeavePoint);
            else
            {
                NotifyPatientLeft(patient);
                UnityEngine.Object.Destroy(patient.gameObject);
            }
        }

        public void NotifyShadowDefeated(AnimalHospitalShadowAi shadow) => Events.NotifyShadowDefeated(shadow);

        public void NotifySlimeExpired(AnimalHospitalSlimeHazard slime) => Events.NotifySlimeExpired(slime);

        public AnimalHospitalPatient FindNearestOtherPatient(AnimalHospitalPatient self)
        {
            AnimalHospitalPatient best = null;
            var bestDist = float.MaxValue;
            for (var i = 0; i < _patients.Count; i++)
            {
                var p = _patients[i];
                if (p == null || p == self || !p.IsAlive)
                    continue;
                var d = Vector3.Distance(self.ActorTransform.position, p.ActorTransform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }

            return best;
        }

        /// <summary>准星射线未命中碰撞体时，用锥形检测辅助锁定病人（便于手枪等攻击）。</summary>
        public AnimalHospitalPatient FindPatientInAimCone(Ray ray, float maxDistance, float maxHalfAngleDegrees)
        {
            AnimalHospitalPatient best = null;
            var bestAngle = maxHalfAngleDegrees;
            for (var i = 0; i < _patients.Count; i++)
            {
                var patient = _patients[i];
                if (patient == null || !patient.IsAlive)
                    continue;

                var aimPoint = patient.ActorTransform.position + Vector3.up * 1f;
                var toTarget = aimPoint - ray.origin;
                var dist = toTarget.magnitude;
                if (dist <= 0.01f || dist > maxDistance)
                    continue;

                var angle = Vector3.Angle(ray.direction, toTarget / dist);
                if (angle > bestAngle)
                    continue;

                bestAngle = angle;
                best = patient;
            }

            return best;
        }

        public AnimalHospitalWard PickRandomOccupiedOrAnyWard()
        {
            if (Wards == null || Wards.Length == 0)
                return null;

            var occupied = new List<AnimalHospitalWard>();
            for (var i = 0; i < Wards.Length; i++)
            {
                if (Wards[i] != null && Wards[i].IsOccupied)
                    occupied.Add(Wards[i]);
            }

            var pool = occupied.Count > 0 ? occupied : new List<AnimalHospitalWard>(Wards);
            pool.RemoveAll(w => w == null);
            if (pool.Count == 0)
                return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        public bool IsPlayerInsideWard(AnimalHospitalWard ward, Vector3 playerPos) =>
            ward != null && ward.ContainsPoint(playerPos);

        public AnimalHospitalWard FindBurningWardContainingPlayer()
        {
            var burning = Events?.BurningWard;
            if (burning == null || !burning.IsOnFire || PlayerTransform == null)
                return null;

            return burning.ContainsPoint(PlayerTransform.position) ? burning : null;
        }

        void TickArrivals()
        {
            if (_arrivalTimesSeconds == null || _arrivalTimesSeconds.Length == 0)
                return;

            for (var i = 0; i < _arrivalTimesSeconds.Length; i++)
            {
                if (_spawnedArrivalIndices.Contains(i))
                    continue;
                if (_dayElapsed < _arrivalTimesSeconds[i])
                    continue;

                _spawnedArrivalIndices.Add(i);
                SpawnPatient();
            }

            if (_dayCompletedRaised)
                return;

            if (_spawnedArrivalIndices.Count >= _arrivalTimesSeconds.Length
                && _patients.Count == 0
                && _receptionPatient == null
                && _receptionClaimedPatient == null)
            {
                _dayCompletedRaised = true;
            }
        }

        /// <summary>换班台交互：至少接待 1 名治疗完成的病人方可打开结算。</summary>
        public bool TryRequestDaySettlement()
        {
            if (!_running)
                return false;

            if (_treatedToday < 1)
            {
                ShowToast("请开始今日的工作。");
                return false;
            }

            _running = false;
            DaySettlementRequested?.Invoke();
            return true;
        }

        void HandleDaySettlement() => TryRequestDaySettlement();

        void TickReceptionQueue()
        {
            RefreshReceptionClaim();

            if (IsReceptionSlotBusy())
            {
                for (var i = 0; i < _patients.Count; i++)
                {
                    var p = _patients[i];
                    if (p != null && p.Phase == AnimalHospitalPatientPhase.Waiting && UnityEngine.Random.value < 0.002f)
                        p.WanderInWaiting(WaitingZone);
                }

                return;
            }

            for (var i = 0; i < _patients.Count; i++)
            {
                var p = _patients[i];
                if (p == null || p.Phase != AnimalHospitalPatientPhase.Waiting)
                    continue;

                _receptionClaimedPatient = p;
                AnimalHospitalAiDebug.Log(
                    p,
                    "queue: reception slot free → claim + GoToReception");
                p.GoToReception(ReceptionZone != null ? ReceptionZone.ReceptionPoint : null);
                return;
            }
        }

        void RefreshReceptionClaim()
        {
            if (_receptionClaimedPatient == null)
                return;

            if (!_receptionClaimedPatient.IsAlive)
            {
                _receptionClaimedPatient = null;
                return;
            }

            var phase = _receptionClaimedPatient.Phase;
            if (phase != AnimalHospitalPatientPhase.MovingToReception
                && phase != AnimalHospitalPatientPhase.AtReception)
            {
                // 已被送去病房/离开等，释放名额。
                if (_receptionPatient != _receptionClaimedPatient)
                    _receptionClaimedPatient = null;
            }
        }

        bool IsReceptionSlotBusy()
        {
            if (_receptionPatient != null)
                return true;

            if (_receptionClaimedPatient != null
                && _receptionClaimedPatient.IsAlive
                && (_receptionClaimedPatient.Phase == AnimalHospitalPatientPhase.MovingToReception
                    || _receptionClaimedPatient.Phase == AnimalHospitalPatientPhase.AtReception))
                return true;

            // 兜底：扫描所有病人，避免 claim 引用丢失时重复派遣。
            for (var i = 0; i < _patients.Count; i++)
            {
                var p = _patients[i];
                if (p == null || !p.IsAlive)
                    continue;
                if (p.Phase == AnimalHospitalPatientPhase.MovingToReception
                    || p.Phase == AnimalHospitalPatientPhase.AtReception)
                    return true;
            }

            return false;
        }

        bool ShouldSpawnAnomalousPatient()
        {
            var chance = ResolveAnomalyChance();
            if (chance <= 0f)
                return false;

            var anomalousAliveOrQueued = 0;
            for (var i = 0; i < _patients.Count; i++)
            {
                var p = _patients[i];
                if (p != null && p.IsAlive && p.IsAnomalous)
                    anomalousAliveOrQueued++;
            }

            // 已生成但尚未统计离开的异常人数 ≈ 当前存活异常 + 已处理异常 + 已拍照未离开？
            // 上限：不超过本日总病人一半（含即将生成的这一人）。
            var spawnedSoFar = _spawnedArrivalIndices.Count;
            var maxAnomalous = Mathf.Max(0, _plannedPatientCount / 2);
            var anomalousTotalSoFar = anomalousAliveOrQueued + _anomalousHandledToday;
            if (anomalousTotalSoFar >= maxAnomalous)
                return false;

            return UnityEngine.Random.value < chance;
        }

        void SpawnPatient()
        {
            var def = _config != null ? _config.PickRandomPatient() : null;
            if (def == null || def.prefab == null || HospitalExit == null)
            {
                AnimalHospitalAiDebug.Log("SpawnPatient FAILED: missing patient catalog/prefab or HospitalExit");
                ToastRequested?.Invoke("缺少病人配置或出入口。");
                return;
            }

            var anomalous = ShouldSpawnAnomalousPatient();
            var spawnPosition = HospitalExit.ResolveSpawnPosition();
            AnimalHospitalAiDebug.Log(
                $"SpawnPatient id={def.patientId} anomalous={anomalous} spawn={spawnPosition} " +
                $"dayElapsed={_dayElapsed:F1}s patients={_patients.Count}");

            var go = UnityEngine.Object.Instantiate(
                def.prefab,
                spawnPosition,
                HospitalExit.SpawnPoint.rotation);
            var patient = go.GetComponent<AnimalHospitalPatient>() ?? go.AddComponent<AnimalHospitalPatient>();
            patient.Initialize(
                this,
                def,
                anomalous,
                _config != null ? _config.anomalousPatientMaxHealth : 10f);
            patient.AssignIllness(PickRandomIllness());
            patient.PlaceOnNavMeshNear(spawnPosition, 12f);
            if (!patient.IsAgentOnNavMesh && WaitingZone != null)
            {
                AnimalHospitalAiDebug.Log(patient, "spawn: still off NavMesh → fallback near WaitingZone");
                patient.EnsureNavMeshNearOr(WaitingZone.transform.position, 12f);
            }

            AnimalHospitalAiDebug.Log(
                patient,
                $"spawn placed onNavMesh={patient.IsAgentOnNavMesh}");
            _patients.Add(patient);
            patient.BeginWaiting(WaitingZone);
        }

        void BeginReceptionFlow(AnimalHospitalPatient patient)
        {
            ClearReceptionVisuals();
            _receptionStep = AnimalHospitalReceptionStep.Report;
            ReceptionStepChanged?.Invoke(_receptionStep);

            if (_config != null && _config.reportPrefab != null && ReceptionZone != null)
            {
                _spawnedReport = UnityEngine.Object.Instantiate(
                    _config.reportPrefab,
                    ReceptionZone.ReportSpawnPoint.position,
                    ReceptionZone.ReportSpawnPoint.rotation);
                var report = _spawnedReport.GetComponent<AnimalHospitalInteractable>()
                             ?? _spawnedReport.AddComponent<AnimalHospitalInteractable>();
                report.Configure(AnimalHospitalInteractableKind.Report, "report", "报告单");
                SetGuide(report);
            }
            else if (ReceptionZone?.Report != null)
            {
                ReceptionZone.Report.SetInteractable(true);
                SetGuide(ReceptionZone.Report);
            }
        }

        void AdvanceReceptionTo(AnimalHospitalReceptionStep step)
        {
            _receptionStep = step;
            ReceptionStepChanged?.Invoke(step);

            AnimalHospitalInteractable next = null;
            if (ReceptionZone != null)
            {
                next = step switch
                {
                    AnimalHospitalReceptionStep.Camera => ReceptionZone.CameraStation,
                    AnimalHospitalReceptionStep.Computer => ReceptionZone.Computer,
                    AnimalHospitalReceptionStep.Printer => ReceptionZone.Printer,
                    AnimalHospitalReceptionStep.AdmissionForm => _spawnedAdmission,
                    AnimalHospitalReceptionStep.Patient => _receptionPatient?.Interactable,
                    _ => null,
                };
            }

            if (_spawnedReport != null && step != AnimalHospitalReceptionStep.Report)
            {
                UnityEngine.Object.Destroy(_spawnedReport);
                _spawnedReport = null;
            }

            SetGuide(next);
            next?.SetInteractable(true);
        }

        void HandleCameraInteract()
        {
            var hud = UnityEngine.Object.FindObjectOfType<AnimalHospitalHudSection>();
            hud?.ScreenFlashUi?.PlayFlash();
            ReceptionZone?.PlayCameraFlash();
            AnimalHospitalAudio.Resolve()?.PlayCameraShutter();

            if (_receptionPatient != null)
            {
                var photo = _receptionPatient.IsAnomalous
                    ? _receptionPatient.AnomalousPhoto
                    : _receptionPatient.NormalPhoto;
                ReceptionZone?.ShowPhoto(photo);
                if (_receptionPatient.IsAnomalous)
                    NotifyAnomalousPhotoTaken();
                _receptionPatient.MarkPhotoTaken();
            }

            AdvanceReceptionTo(AnimalHospitalReceptionStep.Computer);
        }

        void HandleReceptionComputerInteract()
        {
            var ui = ReceptionZone?.GetReceptionComputerUi();
            if (ui == null)
            {
                AdvanceReceptionTo(AnimalHospitalReceptionStep.Printer);
                return;
            }

            ReceptionZone.Computer?.SetInteractable(false);
            ui.Play(
                "注册中",
                "已注册",
                stepIntervalSeconds: 0.4f,
                stepPercent: 0.2f,
                onCompleted: () => AdvanceReceptionTo(AnimalHospitalReceptionStep.Printer));
        }

        void HandlePrinterInteract()
        {
            ReceptionZone?.HidePhoto();
            if (ReceptionZone == null)
            {
                AdvanceReceptionTo(AnimalHospitalReceptionStep.Patient);
                return;
            }

            GameObject formGo;
            var endPos = ReceptionZone.AdmissionSpawnPoint.position;
            var endRot = ReceptionZone.AdmissionSpawnPoint.rotation;
            if (_config != null && _config.admissionFormPrefab != null)
            {
                formGo = UnityEngine.Object.Instantiate(
                    _config.admissionFormPrefab,
                    endPos,
                    endRot);
            }
            else
            {
                formGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                formGo.name = "AdmissionForm";
                formGo.transform.position = endPos;
                formGo.transform.localScale = new Vector3(0.3f, 0.02f, 0.4f);
            }

            ReceptionZone.PlayAdmissionFormPopIn(formGo.transform);

            _spawnedAdmission = formGo.GetComponent<AnimalHospitalAdmissionForm>()
                                ?? formGo.AddComponent<AnimalHospitalAdmissionForm>();
            _spawnedAdmission.BindPatient(_receptionPatient);
            _spawnedAdmission.SetInteractable(true);
            AdvanceReceptionTo(AnimalHospitalReceptionStep.AdmissionForm);
        }

        void HandleAdmissionInteract(AnimalHospitalAdmissionForm form)
        {
            if (form == null || _receptionPatient == null)
                return;

            _receptionPatient.AttachAdmissionForm(form.gameObject);
            _spawnedAdmission = null;
            form.SetInteractable(false);
            _receptionPatient.Interactable?.SetInteractable(true);
            AdvanceReceptionTo(AnimalHospitalReceptionStep.Patient);
            ToastRequested?.Invoke("入院单已交给病人，请与病人交互安排入院。");
            AnimalHospitalAiDebug.Log(_receptionPatient, "admission attached → waiting Patient interact to AssignWard");
        }

        void HandlePatientInteract(AnimalHospitalInteractable interactable)
        {
            var patient = interactable.GetComponentInParent<AnimalHospitalPatient>();
            if (patient == null)
                return;

            if (patient.Phase == AnimalHospitalPatientPhase.AtReception
                && _receptionStep == AnimalHospitalReceptionStep.Patient
                && patient == _receptionPatient)
            {
                var ward = FindAvailableWard(patient.PreferredWardId);
                if (ward == null)
                {
                    ToastRequested?.Invoke("没有空闲病房。请检查 Director 是否绑定了 Ward。");
                    AnimalHospitalAiDebug.Log(
                        patient,
                        $"AssignWard FAILED: no free ward (preferred='{patient.PreferredWardId}', wards={(Wards != null ? Wards.Length : 0)})");
                    return;
                }

                _receptionStep = AnimalHospitalReceptionStep.Completed;
                ReceptionStepChanged?.Invoke(_receptionStep);
                SetGuide(null);
                ClearReceptionVisuals();
                var finished = _receptionPatient;
                _receptionPatient = null;
                if (_receptionClaimedPatient == finished)
                    _receptionClaimedPatient = null;
                ToastRequested?.Invoke($"已安排 {finished.DisplayName} 前往 {ward.WardId}");
                AnimalHospitalAiDebug.Log(finished, $"dispatch to ward '{ward.WardId}' enter={ward.EnterPoint.position}");
                finished.MarkReceptionCompleted();
                finished.AssignWard(ward);
                return;
            }

            if (patient.Phase == AnimalHospitalPatientPhase.InWard)
            {
                FocusWardPatient(patient);

                var step = patient.WardProgress;
                if (step == AnimalHospitalWardStep.Surgery)
                {
                    HandleSurgeryInteract(patient);
                    return;
                }

                if (step == AnimalHospitalWardStep.TreatPatient)
                {
                    TryTreatPatient(patient);
                    return;
                }

                ToastRequested?.Invoke(GetWardStepHint(step));
                return;
            }

            if (patient.IsOnFire && Props.HasExtinguisherOrFreeze)
            {
                patient.SetOnFire(false);
                ToastRequested?.Invoke("已为病人灭火。");
                return;
            }

            if (patient.IsAttacking && Props.HeldProp != AnimalHospitalPropId.None)
            {
                patient.TakeDamage(Props.AttackDamage(_config));
                return;
            }

            AnimalHospitalAiDebug.Log(
                patient,
                $"Patient interact ignored: phase={patient.Phase} receptionStep={_receptionStep} isReceptionPatient={patient == _receptionPatient}");
        }

        void FocusWardPatient(AnimalHospitalPatient patient, bool announce = false)
        {
            if (patient == null || patient.AssignedWard == null)
                return;

            _focusWardPatient = patient;
            _wardStep = patient.WardProgress;
            WardStepChanged?.Invoke(_wardStep);
            SetGuideForPatientStep(patient, patient.WardProgress);

            if (announce && patient.WardProgress == AnimalHospitalWardStep.Surgery)
                ToastRequested?.Invoke("请先为病人进行手术。");
        }

        void TryFocusNextWardPatient()
        {
            for (var i = 0; i < _patients.Count; i++)
            {
                var candidate = _patients[i];
                if (candidate == null || !candidate.IsAlive)
                    continue;
                if (candidate.Phase != AnimalHospitalPatientPhase.InWard)
                    continue;
                if (candidate.WardProgress == AnimalHospitalWardStep.Completed)
                    continue;

                FocusWardPatient(candidate);
                return;
            }
        }

        static void DisableWardStationInteractables(AnimalHospitalWard ward)
        {
            if (ward == null)
                return;

            ward.Analyzer?.SetInteractable(false);
            ward.WardComputer?.SetInteractable(false);
            ward.WardDisplay?.SetInteractable(false);

            var occupant = ward.Occupant;
            occupant?.Interactable?.SetInteractable(false);
        }

        void RefreshWardStationState(AnimalHospitalWard ward)
        {
            if (ward == null)
                return;

            DisableWardStationInteractables(ward);

            var patient = ward.Occupant;
            if (patient == null || !patient.IsAlive || patient.Phase != AnimalHospitalPatientPhase.InWard)
                return;

            var step = patient.WardProgress;
            if (step == AnimalHospitalWardStep.None || step == AnimalHospitalWardStep.Completed)
                return;

            ResolveWardStepInteractable(patient, step)?.SetInteractable(true);
        }

        static AnimalHospitalInteractable ResolveWardStepInteractable(
            AnimalHospitalPatient patient,
            AnimalHospitalWardStep step)
        {
            if (patient?.AssignedWard == null)
                return null;

            var ward = patient.AssignedWard;
            return step switch
            {
                AnimalHospitalWardStep.Surgery => patient.Interactable,
                AnimalHospitalWardStep.Analyzer => ward.Analyzer,
                AnimalHospitalWardStep.WardComputer => ward.WardComputer,
                AnimalHospitalWardStep.WardDisplay => ward.WardDisplay,
                AnimalHospitalWardStep.TreatPatient => patient.Interactable,
                _ => null,
            };
        }

        void SetGuideForPatientStep(AnimalHospitalPatient patient, AnimalHospitalWardStep step) =>
            SetGuide(ResolveWardStepInteractable(patient, step));

        static string GetWardStepHint(AnimalHospitalWardStep step) => step switch
        {
            AnimalHospitalWardStep.Analyzer => "请使用分析机。",
            AnimalHospitalWardStep.WardComputer => "请使用病房电脑。",
            AnimalHospitalWardStep.WardDisplay => "请查看病房显示屏。",
            AnimalHospitalWardStep.TreatPatient => "请为病人用药。",
            _ => "请继续当前病房流程。",
        };

        void HandleSurgeryInteract(AnimalHospitalPatient patient)
        {
            if (patient == null || patient.WardProgress != AnimalHospitalWardStep.Surgery)
            {
                if (patient != null)
                    ToastRequested?.Invoke(GetWardStepHint(patient.WardProgress));
                return;
            }

            patient.Interactable?.SetInteractable(false);
            patient.PlaySurgerySleepEffect();
            ToastRequested?.Invoke($"{patient.DisplayName} 手术完成，请使用分析机。");
            AdvancePatientWardStep(patient, AnimalHospitalWardStep.Analyzer);
        }

        void AdvancePatientWardStep(AnimalHospitalPatient patient, AnimalHospitalWardStep step)
        {
            if (patient == null)
                return;

            patient.SetWardProgress(step);
            RefreshWardStationState(patient.AssignedWard);

            if (_focusWardPatient == patient)
            {
                _wardStep = step;
                WardStepChanged?.Invoke(step);
                SetGuideForPatientStep(patient, step);
            }
        }

        bool TryResolveWardPatientFromStation(
            AnimalHospitalInteractable interactable,
            out AnimalHospitalPatient patient,
            out AnimalHospitalWard ward)
        {
            patient = null;
            ward = interactable != null ? interactable.GetComponentInParent<AnimalHospitalWard>() : null;
            if (ward?.Occupant != null)
                patient = ward.Occupant;

            return patient != null && ward != null && patient.IsAlive;
        }

        void HandleAnalyzerInteract(AnimalHospitalInteractable interactable)
        {
            if (!TryResolveWardPatientFromStation(interactable, out var patient, out var ward))
            {
                ToastRequested?.Invoke("该病房没有待处理病人。");
                return;
            }

            if (patient.WardProgress != AnimalHospitalWardStep.Analyzer)
            {
                ToastRequested?.Invoke(GetWardStepHint(patient.WardProgress));
                return;
            }

            ward.Analyzer?.SetInteractable(false);
            var ui = ward.GetAnalyzerUi();
            var duration = UnityEngine.Random.Range(3f, 5f);
            if (ui == null)
            {
                AdvancePatientWardStep(patient, AnimalHospitalWardStep.WardComputer);
                return;
            }

            ui.Play(duration, () => AdvancePatientWardStep(patient, AnimalHospitalWardStep.WardComputer));
        }

        void HandleWardComputer(AnimalHospitalInteractable interactable)
        {
            if (!TryResolveWardPatientFromStation(interactable, out var patient, out var ward))
                return;

            if (patient.WardProgress != AnimalHospitalWardStep.WardComputer)
            {
                ToastRequested?.Invoke(GetWardStepHint(patient.WardProgress));
                return;
            }

            var illness = patient.IllnessDescription;
            ward.WardComputer?.SetInteractable(false);

            var ui = ward.GetWardComputerUi();
            if (ui == null)
            {
                AdvancePatientWardStep(patient, AnimalHospitalWardStep.WardDisplay);
                return;
            }

            ui.Play(
                "正在分析结果",
                string.IsNullOrWhiteSpace(illness) ? "未知病症" : illness,
                stepIntervalSeconds: 0.2f,
                stepPercent: 0.1f,
                onCompleted: () => AdvancePatientWardStep(patient, AnimalHospitalWardStep.WardDisplay));
        }

        void HandleWardDisplayInteract(AnimalHospitalInteractable interactable)
        {
            if (!TryResolveWardPatientFromStation(interactable, out var patient, out var ward))
                return;

            if (patient.WardProgress != AnimalHospitalWardStep.WardDisplay)
            {
                ToastRequested?.Invoke(GetWardStepHint(patient.WardProgress));
                return;
            }

            var medName = ResolveMedicineName(patient.RequiredMedicineId);
            var icon = ResolveMedicineIcon(patient.RequiredMedicineId);
            ward.GetWardDisplayUi()?.ShowMedicine(medName, icon);

            AdvancePatientWardStep(patient, AnimalHospitalWardStep.TreatPatient);
        }

        void TryTreatPatient(AnimalHospitalPatient patient)
        {
            if (patient == null)
                return;

            if (patient.WardProgress != AnimalHospitalWardStep.TreatPatient)
            {
                ToastRequested?.Invoke(GetWardStepHint(patient.WardProgress));
                return;
            }

            if (string.IsNullOrEmpty(_heldMedicineId) || !patient.NeedsMedicine(_heldMedicineId))
            {
                ToastRequested?.Invoke("药物不正确，请前往药柜取药。");
                return;
            }

            var fullyCured = patient.TryConsumeMedicine(_heldMedicineId);
            _heldMedicineId = null;
            HeldMedicineChanged?.Invoke(null);

            if (!fullyCured)
            {
                var nextId = patient.RequiredMedicineId;
                var nextName = ResolveMedicineName(nextId);
                ToastRequested?.Invoke($"已用药，仍需：{nextName}");
                var ward = patient.AssignedWard;
                if (ward != null)
                    ward.GetWardDisplayUi()?.ShowMedicine(nextName, ResolveMedicineIcon(nextId));
                return;
            }

            _treatedToday++;
            var save = SaveService.Data?.animalHospital;
            if (save != null)
                save.patientsTreatedTotal++;

            Events.OnWardPatientCured(patient.AssignedWard);
            patient.NotifyTreatmentComplete();
            patient.SetWardProgress(AnimalHospitalWardStep.Completed);
            RefreshWardStationState(patient.AssignedWard);
            ToastRequested?.Invoke($"{patient.DisplayName} 治疗完成。");
            if (_focusWardPatient == patient)
            {
                _wardStep = AnimalHospitalWardStep.Completed;
                WardStepChanged?.Invoke(_wardStep);
                SetGuide(null);
            }

            var rest = _config != null ? _config.treatmentBedRestSeconds : 3f;
            var exit = HospitalExit != null ? HospitalExit.LeavePoint : null;
            patient.BeginPostTreatmentLeave(exit, rest);
        }

        void HandleMedicinePickup(AnimalHospitalMedicineInteractable med)
        {
            if (med == null)
                return;

            _heldMedicineId = med.MedicineId;
            HeldMedicineChanged?.Invoke(_heldMedicineId);
            ToastRequested?.Invoke($"已拿取：{ResolveMedicineName(med.MedicineId)}");
        }

        AnimalHospitalIllnessCatalogEntry PickRandomIllness()
        {
            var pick = _config != null ? _config.PickRandomIllness() : null;
            if (pick == null)
                ToastRequested?.Invoke("病症库为空：请配置 Illness Catalog。");
            return pick;
        }

        public AnimalHospitalMedicineDef FindMedicineDef(string medicineId) =>
            _config != null ? _config.FindMedicine(medicineId) : null;

        void HandleCoffee()
        {
            if (_coffeeCooldownLeft > 0f)
            {
                UiRouter.OpenAnimalHospitalCoffeeOffer(
                    "快捷咖啡",
                    "咖啡未充好，是否立即获得。",
                    success =>
                    {
                        if (!success)
                            return;
                        Sanity.Restore(_config != null ? _config.coffeeSanityRestore : 10f);
                        ToastRequested?.Invoke("恢复了理智。");
                    });
                return;
            }

            Sanity.Restore(_config != null ? _config.coffeeSanityRestore : 10f);
            _coffeeCooldownLeft = _config != null ? _config.coffeeCooldownSeconds : 300f;
            ToastRequested?.Invoke("喝了一杯咖啡，理智恢复。");
        }

        void HandlePropPickup(AnimalHospitalPropInteractable prop)
        {
            var save = SaveService.Data?.animalHospital;
            if (save == null)
                return;

            if (!save.IsPropUnlocked(prop.PropId) && prop.PropId != AnimalHospitalPropId.StunBaton)
            {
                var cost = _config != null ? _config.ResolvePropUnlockCost(prop.PropId) : 0;
                if (ModeSaveAccess.Coins < cost)
                {
                    AudioGameplayFeedback.PlayUiError();
                    UiRouter.OpenCoinsHub();
                    ToastRequested?.Invoke($"金币不足，解锁需要 {cost} 金币。");
                    return;
                }

                if (!Props.TryUnlock(prop.PropId, _config, save))
                    return;

                ToastRequested?.Invoke($"已解锁 {prop.DisplayName}");
            }

            // 先清高亮再装备：TryEquip 会立即生成手持模型，若此时仍带 outline/高光会被克隆进去。
            _highlights?.ClearVisuals(prop);
            _highlights?.SetAimTarget(null);
            Props.TryEquip(prop.PropId, save);
            ToastRequested?.Invoke($"已装备 {prop.DisplayName}");
        }

        void HandleCurtain()
        {
            if (ReceptionZone == null)
                return;

            ReceptionZone.SetCurtainClosed(!ReceptionZone.CurtainClosed);
            ToastRequested?.Invoke(ReceptionZone.CurtainClosed ? "窗帘已拉下：暂停接待" : "窗帘已拉开：继续接待");
        }

        void TryExtinguishNear()
        {
            if (!Props.HasExtinguisherOrFreeze)
            {
                ToastRequested?.Invoke("需要灭火器或冷冻枪。");
                return;
            }

            if (Events.BurningWard != null && Events.TryExtinguishWard(Events.BurningWard))
                ToastRequested?.Invoke("病房火势已扑灭。");
        }

        bool TryExtinguishFromStation(AnimalHospitalInteractable interactable)
        {
            if (!Props.HasExtinguisherOrFreeze || Events.BurningWard == null)
                return false;

            var ward = interactable != null
                ? interactable.GetComponentInParent<AnimalHospitalWard>()
                : null;
            if (ward == null || ward != Events.BurningWard)
                return false;

            if (Events.TryExtinguishWard(ward))
            {
                ToastRequested?.Invoke("病房火势已扑灭。");
                return true;
            }

            return false;
        }

        void HandleSlime(AnimalHospitalInteractable interactable)
        {
            if (!Props.HasFreezeGun)
            {
                ToastRequested?.Invoke("需要冷冻枪才能消除粘液。");
                return;
            }

            var slime = interactable.GetComponentInParent<AnimalHospitalSlimeHazard>();
            if (slime != null && Events.TryRemoveSlime(slime))
                ToastRequested?.Invoke("粘液已清除。");
        }

        void HandleShadowAttack(AnimalHospitalInteractable interactable)
        {
            if (Props.HeldProp == AnimalHospitalPropId.None)
            {
                ToastRequested?.Invoke("请先装备攻击道具。");
                return;
            }

            var shadow = interactable.GetComponentInParent<AnimalHospitalShadowAi>();
            shadow?.TakeDamage(Props.AttackDamage(_config));
        }

        AnimalHospitalWard FindAvailableWard(string preferredId)
        {
            if (Wards == null)
                return null;

            if (!string.IsNullOrWhiteSpace(preferredId))
            {
                for (var i = 0; i < Wards.Length; i++)
                {
                    var w = Wards[i];
                    if (w != null && !w.IsOccupied && w.WardId == preferredId)
                        return w;
                }
            }

            for (var i = 0; i < Wards.Length; i++)
            {
                var w = Wards[i];
                if (w != null && !w.IsOccupied)
                    return w;
            }

            return null;
        }

        string ResolveMedicineName(string medicineId)
        {
            var med = FindMedicineDef(medicineId);
            if (med != null && !string.IsNullOrWhiteSpace(med.displayName))
                return med.displayName;
            return medicineId;
        }

        UnityEngine.Sprite ResolveMedicineIcon(string medicineId)
        {
            var med = FindMedicineDef(medicineId);
            return med != null ? med.medicineIcon : null;
        }

        void SetGuide(AnimalHospitalInteractable target) => _highlights?.SetGuideTarget(target);

        void ClearReceptionFlow()
        {
            _receptionPatient = null;
            _receptionClaimedPatient = null;
            _receptionStep = AnimalHospitalReceptionStep.None;
            ClearReceptionVisuals();
            AnimalHospitalAudio.Resolve()?.SetReceptionWaitLoop(false);
            SetGuide(null);
        }

        void ClearReceptionVisuals()
        {
            ReceptionZone?.HidePhoto();
            if (_spawnedReport != null)
            {
                UnityEngine.Object.Destroy(_spawnedReport);
                _spawnedReport = null;
            }

            if (_spawnedAdmission != null)
            {
                UnityEngine.Object.Destroy(_spawnedAdmission.gameObject);
                _spawnedAdmission = null;
            }
        }

        void RefreshPlayerWardOccupancy()
        {
            if (PlayerTransform == null || Wards == null)
                return;

            for (var i = 0; i < Wards.Length; i++)
            {
                var ward = Wards[i];
                if (ward == null || !ward.IsOnFire)
                    continue;

                // 灭火交互：瞄准着火病房分析机/电脑也可尝试灭火（由 FireTarget 或交互病房）
                if (Props.HasExtinguisherOrFreeze && IsPlayerInsideWard(ward, PlayerTransform.position))
                {
                    // 标记缓存避免每帧 Toast；实际灭火在 Hold 完成 FireTarget 时触发
                    _playerWardCache.Add(ward);
                }
            }
        }
    }
}
