using System.Collections;
using Game.Collectibles.Stats;
using Game.DestroyCity.Config;
using Game.VFX;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>核弹发射器：发射按钮、燃料放置区、核弹展示区。</summary>
    [DisallowMultipleComponent]
    public sealed class NukeLauncherController : MonoBehaviour
    {
        [SerializeField] private NukeLaunchButton launchButton;
        [SerializeField] private NukeFuelDepositZone fuelDepositZone;
        [SerializeField] private NukeLauncherInteractionHub interactionHub;
        [SerializeField] private GameObject fuelDepositZoneRoot;
        [SerializeField] private NukeLauncherXpBar nukeXpBar;
        [SerializeField] private Transform nukeVisualRoot;
        [SerializeField] private Transform cityTarget;
        [SerializeField] private Transform missileSpawnPoint;
        [SerializeField] private float launchCooldownSeconds = 2f;

        [Header("发射准备")]
        [SerializeField] private Transform lidTransform;
        [SerializeField] private float lidClosedAngleX = -98.3f;
        [SerializeField] private float lidCloseDuration = 0.75f;
        [SerializeField] private float alarmDuration = 2f;
        [SerializeField] private float lidOpenDuration = 0.75f;
        [SerializeField] private float missileRiseDistance = 2.5f;
        [SerializeField] private float missileRiseDuration = 0.65f;
        [SerializeField] private NukeLauncherAlarmEffects alarmEffects;
        [SerializeField] private NukeLandingMarker landingMarker;
        [SerializeField] private GameObject explosionEffectPrefab;
        [Tooltip("大爆炸范围内随机出现的小爆炸预制体；留空则复用主爆炸预制体（仅视觉，不摧毁房屋）。")]
        [SerializeField] private GameObject smallExplosionEffectPrefab;
        [Tooltip("≤0 时不覆盖，使用爆炸预制体 NukeExplosionEffect 上的 Grow Duration。")]
        [SerializeField] private float explosionGrowDuration = -1f;
        [Tooltip("≥0 时覆盖预制体 Lifetime After Grow；<0 时使用预制体配置。")]
        [SerializeField] private float explosionLifetimeAfterGrow = -1f;
        [Tooltip("爆炸视觉特效相对落点在 Y 轴上的抬高（米）。")]
        [SerializeField] private float explosionVerticalOffset = 1f;
        [SerializeField] private float postExplosionCameraWait = 5f;

        [Header("Editor Gizmo")]
        [SerializeField] private bool drawExplosionRangeGizmo = true;
        [SerializeField] private bool drawExplosionRangeOnlyWhenSelected = true;
        [Tooltip("未运行时预览的核弹等级；运行时读取存档等级。")]
        [SerializeField] private int editorPreviewNukeLevel = 1;
        [SerializeField] private Color gizmoConfiguredColor = new Color(1f, 0.85f, 0.1f, 0.95f);
        [SerializeField] private Color gizmoVisualColor = new Color(0.2f, 0.85f, 1f, 0.85f);
        [SerializeField] private Color gizmoHouseDestructionColor = new Color(1f, 0.25f, 0.15f, 0.95f);

        [Header("升级特效锚点")]
        [SerializeField] private Transform nukeUpgradeFxAnchor;
        [SerializeField] private Vector3 nukeUpgradeFxOffset = new Vector3(0f, -1.2f, 0f);

        [Header("导弹飞行")]
        [SerializeField] private NukeMissileFlightConfig flightConfig = NukeMissileFlightConfig.Default;
        [SerializeField] private NukeMissileFlightCamera flightCamera;
        [Tooltip("留空则复用发射井上的核弹模型。")]
        [SerializeField] private GameObject missileFlightPrefab;

        [Header("Diagnostics")]
        [Tooltip("勾选后在控制台输出初始化/经验/发射/导弹生成等诊断日志。过滤关键字：NukeLauncherDiag")]
        [SerializeField] private bool diagnosticLogs = true;

        Vector3 _lidOpenEuler;
        GameObject _nukeVisual;
        float _cooldownUntilUnscaled;
        bool _launchInProgress;
        bool _descentSteerActive;
        Coroutine _launchCo;
        NukeUpgradeService.LaunchSnapshot _activeLaunch;
        Vector3 _launchPlannedLanding;
        DestroyCityStatsAggregator _statsAggregator;

        public Transform CityTarget => cityTarget;
        public bool IsLaunching => _launchInProgress;
        public bool IsDescentSteeringActive => _descentSteerActive;
        public Vector3 LaunchPlannedLanding => _launchPlannedLanding;
        public float DescentSteerRadius => flightConfig.descentSteerRadius;

        void OnEnable()
        {
            NukeUpgradeService.FuelXpInfused += OnFuelXpInfused;
            NukeUpgradeService.NukeLevelIncreased += OnNukeLevelIncreased;
        }

        void OnDisable()
        {
            NukeUpgradeService.FuelXpInfused -= OnFuelXpInfused;
            NukeUpgradeService.NukeLevelIncreased -= OnNukeLevelIncreased;
            DestroyCityPresentationFx.StopMissileAlarm();
            DestroyCityPresentationFx.StopMissileFlightLoop();
            alarmEffects?.SetAlarmActive(false);
            NukeLaunchViewModeController.Instance?.EndLaunchSession();
        }

        void Start()
        {
            if (launchButton != null)
                launchButton.Bind(this);
            if (fuelDepositZone != null)
                fuelDepositZone.Bind(this);
            if (interactionHub == null)
                interactionHub = GetComponent<NukeLauncherInteractionHub>();
            interactionHub?.Bind(launchButton, fuelDepositZone);
            if (fuelDepositZoneRoot == null && fuelDepositZone != null)
                fuelDepositZoneRoot = fuelDepositZone.gameObject;
            EnsureNukeXpBar();
            if (missileSpawnPoint == null)
                missileSpawnPoint = nukeVisualRoot;
            if (flightCamera == null)
                flightCamera = FindObjectOfType<NukeMissileFlightCamera>();
            if (lidTransform == null)
                lidTransform = FindDeepChild(transform, "gaizi");
            if (alarmEffects == null)
                alarmEffects = GetComponentInChildren<NukeLauncherAlarmEffects>(true);
            if (landingMarker == null)
                landingMarker = GetComponentInChildren<NukeLandingMarker>(true);

            CacheLidOpenRotation();
            SetLidClosed01(0f);
            alarmEffects?.SetAlarmActive(false);
            landingMarker?.Hide();
            nukeXpBar?.Refresh(false);
            RefreshNukeVisual();
            SetFuelDepositZoneVisible(true);
            LogInitState("Start");
        }

        public bool CanLaunch() => CanLaunchInternal(logFailure: true);

        bool CanLaunchInternal(bool logFailure)
        {
            if (_launchInProgress)
            {
                if (logFailure)
                    LogDiag("CanLaunch=false: 发射进行中");
                return false;
            }

            if (Time.unscaledTime < _cooldownUntilUnscaled)
            {
                if (logFailure)
                    LogDiag($"CanLaunch=false: 冷却中，剩余={_cooldownUntilUnscaled - Time.unscaledTime:F1}s");
                return false;
            }

            if (!NukeUpgradeService.IsLoaded())
            {
                if (logFailure)
                    LogDiag("CanLaunch=false: NukeUpgradeService.IsLoaded()=false（存档或核弹配置未就绪）");
                return false;
            }

            return true;
        }

        public void OnLaunchButtonPressed()
        {
            LogDiag("OnLaunchButtonPressed");
            if (!CanLaunch())
                return;

            if (cityTarget == null)
            {
                NukeLauncherDiagnostics.LogWarning("发射失败: 未配置 cityTarget", this);
                Debug.LogWarning("[NukeLauncher] 未配置 cityTarget，无法发射。", this);
                return;
            }

            var snapshot = NukeUpgradeService.ConsumeForLaunch();
            if (!snapshot.success)
            {
                NukeLauncherDiagnostics.LogWarning(
                    $"发射失败: ConsumeForLaunch 返回 success=false | {NukeLauncherDiagnostics.DescribeNukeDef(snapshot.def)}",
                    this);
                return;
            }

            LogDiag(
                $"发射开始 | snapshot Lv{snapshot.level} xp={snapshot.xp:F0} " +
                $"def={NukeLauncherDiagnostics.DescribeNukeDef(snapshot.def)} " +
                $"target={cityTarget.position} spawn={DescribeSpawnPoint()}");

            _activeLaunch = snapshot;
            _launchPlannedLanding = cityTarget.position;
            nukeXpBar?.Refresh(true);
            RefreshNukeVisual();
            DestroyCityPresentationFx.PlayLaunchButton(
                launchButton != null ? launchButton.transform.position : transform.position);

            if (_launchCo != null)
                StopCoroutine(_launchCo);
            _launchCo = StartCoroutine(CoLaunchMissile(cityTarget.position, snapshot));
        }

        IEnumerator CoLaunchMissile(Vector3 targetPosition, NukeUpgradeService.LaunchSnapshot snapshot)
        {
            _launchInProgress = true;
            _descentSteerActive = false;
            landingMarker?.Hide();
            launchButton?.PlayLaunchFeedback();

            var spawn = missileSpawnPoint != null ? missileSpawnPoint.position : transform.position;
            var flightStart = spawn;

            if (_nukeVisual != null)
                _nukeVisual.SetActive(false);

            var missileGo = CreateFlightMissileHidden(spawn, targetPosition, snapshot);
            LogDiag(
                $"飞行导弹已创建 | go={(missileGo != null ? missileGo.name : "null")} " +
                $"active={missileGo != null && missileGo.activeSelf} spawn={spawn}",
                missileGo);
            Vector3 landedAt = targetPosition;

            try
            {
                yield return CoPreLaunchSequence(missileGo, spawn, targetPosition, snapshot);

                if (missileGo != null)
                {
                    flightStart = missileGo.transform.position;
                    DestroyCityPresentationFx.StartMissileFlightLoop();
                    NukeLaunchViewModeController.Instance?.BeginLaunchSession(this, missileGo.transform, cityTarget);

                    yield return NukeMissileFlight.CoFlyThreeStage(
                        missileGo.transform,
                        flightStart,
                        targetPosition,
                        cityTarget,
                        flightConfig,
                        pos => landedAt = pos,
                        OnFlightPhaseChanged,
                        predicted => landingMarker?.ShowAt(predicted));
                }

                landingMarker?.Hide();
                LogDiag($"导弹落地 | pos={landedAt}");
                CompleteLaunch(landedAt, snapshot);

                if (missileGo != null)
                {
                    NukeLaunchViewModeController.Instance?.UpdateMissileTransform(null);
                    Destroy(missileGo);
                }

                var viewController = NukeLaunchViewModeController.Instance;
                var wasMissile = viewController != null && viewController.Mode == NukeLaunchViewMode.Missile;

                if (wasMissile && flightCamera != null)
                {
                    viewController.BeginExplosionWatchPhase();
                    flightCamera.BeginExplosionWatch(landedAt, postExplosionCameraWait, snapshot.level);
                    yield return flightCamera.CoWaitExplosionWatch();
                    viewController.ForcePlayerModeAfterExplosion();
                }

                viewController?.EndLaunchSession();
            }
            finally
            {
                _launchInProgress = false;
                _launchCo = null;
                _activeLaunch = default;
                SetFuelDepositZoneVisible(true);
            }
        }

        IEnumerator CoPreLaunchSequence(
            GameObject missileGo,
            Vector3 spawn,
            Vector3 targetPosition,
            NukeUpgradeService.LaunchSnapshot snapshot)
        {
            SetFuelDepositZoneVisible(false);

            if (missileGo != null)
                missileGo.SetActive(false);

            yield return CoAnimateLid(0f, 1f, lidCloseDuration);

            alarmEffects?.SetAlarmActive(true);
            DestroyCityPresentationFx.StartMissileAlarm();
            yield return new WaitForSeconds(Mathf.Max(0.1f, alarmDuration));
            DestroyCityPresentationFx.StopMissileAlarm();
            alarmEffects?.SetAlarmActive(false);

            if (missileGo != null)
            {
                var hiddenPos = spawn + Vector3.down * Mathf.Max(0.1f, missileRiseDistance);
                missileGo.transform.SetPositionAndRotation(
                    hiddenPos,
                    NukeMissileFlight.ComputeLaunchRotation(spawn, targetPosition));
                missileGo.SetActive(true);
                NukeMissileTrailUtility.SetTrailActive(missileGo, false);

                yield return CoAnimateLid(1f, 0f, lidOpenDuration);
                yield return CoRiseMissile(missileGo.transform, hiddenPos, spawn);
                NukeMissileTrailUtility.SetTrailActive(missileGo, true);
            }
            else
            {
                yield return CoAnimateLid(1f, 0f, lidOpenDuration);
            }

            DestroyCityPresentationFx.PlayMissileLaunch(spawn);
        }

        IEnumerator CoAnimateLid(float fromClosed01, float toClosed01, float duration)
        {
            if (lidTransform == null)
                yield break;

            duration = Mathf.Max(0.05f, duration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                SetLidClosed01(Mathf.Lerp(fromClosed01, toClosed01, t));
                yield return null;
            }

            SetLidClosed01(toClosed01);
        }

        IEnumerator CoRiseMissile(Transform missile, Vector3 from, Vector3 to)
        {
            var duration = Mathf.Max(0.05f, missileRiseDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                missile.position = Vector3.Lerp(from, to, t);
                yield return null;
            }

            missile.position = to;
        }

        void CacheLidOpenRotation()
        {
            if (lidTransform != null)
                _lidOpenEuler = lidTransform.localEulerAngles;
        }

        void SetLidClosed01(float closed01)
        {
            if (lidTransform == null)
                return;

            var euler = _lidOpenEuler;
            euler.x = Mathf.LerpAngle(_lidOpenEuler.x, lidClosedAngleX, Mathf.Clamp01(closed01));
            lidTransform.localEulerAngles = euler;
        }

        void OnFlightPhaseChanged(NukeMissileFlight.FlightPhase phase)
        {
            if (phase == NukeMissileFlight.FlightPhase.Cruise)
                SetFuelDepositZoneVisible(true);

            var descent = phase == NukeMissileFlight.FlightPhase.Descent;
            _descentSteerActive = descent;
            flightCamera?.SetDescentSteerEnabled(descent);
            if (!descent)
                landingMarker?.Hide();
        }

        void SetFuelDepositZoneVisible(bool visible)
        {
            if (fuelDepositZoneRoot != null)
                fuelDepositZoneRoot.SetActive(visible);
        }

        public bool TrySteerLanding(Vector3 desiredWorldPosition)
        {
            if (!IsDescentSteeringActive || cityTarget == null)
                return false;

            cityTarget.position = ClampSteerLanding(desiredWorldPosition);
            return true;
        }

        public Vector3 ClampSteerLanding(Vector3 desiredWorldPosition)
        {
            var anchor = _launchPlannedLanding;
            var delta = desiredWorldPosition - anchor;
            delta.y = 0f;
            var radius = Mathf.Max(0f, flightConfig.descentSteerRadius);
            if (delta.sqrMagnitude > radius * radius && radius > 0f)
                delta = delta.normalized * radius;

            return new Vector3(anchor.x + delta.x, anchor.y, anchor.z + delta.z);
        }

        GameObject CreateFlightMissileHidden(
            Vector3 spawn,
            Vector3 targetPosition,
            NukeUpgradeService.LaunchSnapshot snapshot)
        {
            var inst = CreateFlightMissilePrefab(spawn, targetPosition, snapshot);
            if (inst != null)
                inst.SetActive(false);
            return inst;
        }

        GameObject CreateFlightMissilePrefab(
            Vector3 spawn,
            Vector3 targetPosition,
            NukeUpgradeService.LaunchSnapshot snapshot)
        {
            GameObject prefab = missileFlightPrefab;
            var prefabSource = "missileFlightPrefab(serialized)";
            if (prefab == null && snapshot.def != null && snapshot.def.launcherPrefab != null)
            {
                prefab = snapshot.def.launcherPrefab;
                prefabSource = "snapshot.def.launcherPrefab";
            }

            if (prefab == null && _nukeVisual != null)
            {
                prefab = _nukeVisual;
                prefabSource = "siloVisual(_nukeVisual)";
            }

            if (prefab == null)
            {
                LogDiag("CreateFlightMissilePrefab: 无可用预制体，使用 Capsule 占位", this);
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.transform.localScale = new Vector3(0.35f, 0.8f, 0.35f);
                fallback.transform.SetPositionAndRotation(spawn, NukeMissileFlight.ComputeLaunchRotation(spawn, targetPosition));
                return fallback;
            }

            LogDiag(
                $"CreateFlightMissilePrefab | source={prefabSource} prefab={NukeLauncherDiagnostics.DescribeObject(prefab)} spawn={spawn}",
                this);
            var inst = Instantiate(prefab, spawn, NukeMissileFlight.ComputeLaunchRotation(spawn, targetPosition));
            NukeMissileTrailUtility.SetTrailActive(inst, false);
            return inst;
        }

        void CompleteLaunch(Vector3 targetPosition, NukeUpgradeService.LaunchSnapshot snapshot)
        {
            LogDiag($"CompleteLaunch | pos={targetPosition} Lv{snapshot.level}");
            SpawnExplosion(targetPosition, snapshot);

            var stats = default(DestroyCityStatsAggregator.FinalDestroyCityStats);
            if (_statsAggregator == null)
                _statsAggregator = FindObjectOfType<DestroyCityStatsAggregator>();
            if (_statsAggregator != null)
            {
                _statsAggregator.Recompute();
                stats = _statsAggregator.Current;
            }

            var map = DestroyCityService.GetCurrentMapDef();
            NukeLaunchService.LaunchAtCity(map, stats, snapshot.def);

            _cooldownUntilUnscaled = Time.unscaledTime + launchCooldownSeconds;
            _launchInProgress = false;
            NukeUpgradeService.ResetToBaselineMissile(save: true);
            RefreshNukeVisual();
            nukeXpBar?.Refresh(true);
        }

        void SpawnExplosion(Vector3 position, NukeUpgradeService.LaunchSnapshot snapshot)
        {
            DestroyCityPresentationFx.StopMissileFlightLoop();

            if (explosionVerticalOffset != 0f)
                position += Vector3.up * explosionVerticalOffset;

            var def = snapshot.def ?? NukeUpgradeService.GetCurrentNukeDef();
            DestroyCityPresentationFx.PlayMissileExplosion(position);

            var radius = def != null ? def.explosionRadius : 50f;
            NukeHouseDestructionService.EnsureCached();
            var prefab = explosionEffectPrefab;
            if (prefab == null && def != null)
                prefab = def.explosionVfxPrefab;

            var smallPrefab = smallExplosionEffectPrefab != null ? smallExplosionEffectPrefab : prefab;
            var missileLevel = snapshot.level > 0 ? snapshot.level : (def != null ? def.level : 1);

            if (prefab != null)
            {
                LogDiag(
                    $"SpawnExplosion | prefab={NukeLauncherDiagnostics.DescribeObject(prefab)} " +
                    $"radius={radius:F1} pos={position} level={missileLevel}",
                    this);
                NukeExplosionEffect.Spawn(
                    prefab,
                    position,
                    radius,
                    explosionGrowDuration,
                    explosionLifetimeAfterGrow,
                    missileLevel,
                    smallPrefab);
                return;
            }

            LogDiag(
                "SpawnExplosion: def/launcher 未配置爆炸预制体，尝试 VfxManager 回退",
                this);
            var mgr = GameplayVfxManager.Instance;
            if (mgr != null && mgr.HasKind(VfxKind.DestroyCityMissileExplosion))
            {
                var fx = mgr.TryBorrowWorld(VfxKind.DestroyCityMissileExplosion, position, Quaternion.identity, Vector3.one, 4f);
                if (fx != null)
                {
                    var scaler = fx.GetComponent<NukeExplosionEffect>();
                    if (scaler == null)
                        scaler = fx.AddComponent<NukeExplosionEffect>();
                    scaler.Play(radius);
                    scaler.TryScheduleSmallExplosions(missileLevel, smallPrefab);
                }
                return;
            }

            if (mgr != null && mgr.HasKind(VfxKind.LandingDustStrong))
            {
                mgr.BorrowWorld(VfxKind.LandingDustStrong, position, Quaternion.identity, Vector3.one * 3f, 3f);
            }
        }

        public bool TryDepositFuel(PlayerFuelCarryController carry)
        {
            LogDiag($"TryDepositFuel | carry={(carry != null ? carry.name : "null")} isCarrying={carry?.IsCarrying}");
            if (carry == null)
                return false;
            if (!carry.TryDepositToLauncher(this))
            {
                LogDiag("TryDepositFuel 失败（详见 PlayerFuelCarry 日志）");
                return false;
            }

            LogDiag("TryDepositFuel 成功，刷新 XP 条与井内导弹");
            nukeXpBar?.Refresh(true);
            return true;
        }

        void OnFuelXpInfused(float amount)
        {
            LogDiag($"OnFuelXpInfused | +{amount:F0}xp");
            var pos = nukeXpBar != null
                ? nukeXpBar.transform.position
                : transform.position + Vector3.up * 2f;
            DestroyCityXpFloater.Show(pos, amount);
        }

        void OnNukeLevelIncreased(int level)
        {
            LogDiag($"OnNukeLevelIncreased | Lv{level}");
            DestroyCityPresentationFx.PlayNukeUpgrade(ResolveNukeUpgradeFxPosition());
        }

        Vector3 ResolveNukeUpgradeFxPosition()
        {
            if (nukeUpgradeFxAnchor != null)
                return nukeUpgradeFxAnchor.position + nukeUpgradeFxOffset;

            if (fuelDepositZone != null)
                return fuelDepositZone.transform.position + nukeUpgradeFxOffset;

            if (nukeXpBar != null)
                return nukeXpBar.transform.position + nukeUpgradeFxOffset;

            return transform.position + nukeUpgradeFxOffset;
        }

        public void RefreshNukeVisual()
        {
            if (_nukeVisual != null)
                Destroy(_nukeVisual);

            if (nukeVisualRoot == null)
            {
                LogDiag("RefreshNukeVisual 跳过: nukeVisualRoot=null");
                return;
            }

            var def = NukeUpgradeService.GetCurrentNukeDef();
            if (def == null)
            {
                LogDiag("RefreshNukeVisual 跳过: GetCurrentNukeDef()=null");
                return;
            }

            if (def.launcherPrefab == null)
            {
                LogDiag(
                    $"RefreshNukeVisual 跳过: launcherPrefab=null | {NukeLauncherDiagnostics.DescribeNukeDef(def)}");
                return;
            }

            _nukeVisual = Instantiate(def.launcherPrefab, nukeVisualRoot);
            _nukeVisual.transform.localPosition = Vector3.zero;
            _nukeVisual.transform.localRotation = Quaternion.identity;
            NukeMissileTrailUtility.SetTrailActive(_nukeVisual, false);
            LogDiag(
                $"RefreshNukeVisual 成功 | visual={_nukeVisual.name} parent={nukeVisualRoot.name} " +
                $"def={NukeLauncherDiagnostics.DescribeNukeDef(def)}",
                _nukeVisual);
        }

        /// <summary>启动后再次绑定 UI / 导弹展示（Bootstrap 或场景刷新时调用）。</summary>
        public void RefreshAfterBootstrap()
        {
            LogDiag("RefreshAfterBootstrap");
            NukeLauncherDiagnostics.LogBootstrapContext();
            EnsureNukeXpBar();
            RefreshNukeVisual();
            nukeXpBar?.Refresh(false);
            LogInitState("RefreshAfterBootstrap");
        }

        [ContextMenu("Dump Nuke Launcher Diagnostics")]
        public void DumpDiagnostics()
        {
            var previous = NukeLauncherDiagnostics.Enabled;
            NukeLauncherDiagnostics.Enabled = true;
            NukeLauncherDiagnostics.DumpLauncher(this);
            LogInitState("DumpDiagnostics");
            NukeLauncherDiagnostics.Enabled = previous;
        }

        void EnsureNukeXpBar()
        {
            var previous = nukeXpBar;
            if (nukeXpBar != null && nukeXpBar.IsWired)
            {
                LogDiag(
                    $"EnsureNukeXpBar: 已接线 | bar={nukeXpBar.name} score={nukeXpBar.WiringScore} wired={nukeXpBar.IsWired}",
                    nukeXpBar);
                return;
            }

            var bars = GetComponentsInChildren<NukeLauncherXpBar>(true);
            NukeLauncherXpBar best = null;
            var bestScore = int.MinValue;
            for (var i = 0; i < bars.Length; i++)
            {
                var bar = bars[i];
                if (bar == null)
                    continue;

                var score = bar.WiringScore;
                var canvas = bar.GetComponentInParent<Canvas>();
                var underFuelDeposit = canvas != null
                    && canvas.transform.parent != null
                    && canvas.transform.parent.name.IndexOf("FuelDeposit", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (underFuelDeposit)
                    score -= 100;

                LogDiag(
                    $"EnsureNukeXpBar 候选[{i}] | go={bar.name} score={score} wired={bar.IsWired} " +
                    $"underFuelDeposit={underFuelDeposit}",
                    bar);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = bar;
            }

            if (best != null)
                nukeXpBar = best;

            if (nukeXpBar != previous)
            {
                LogDiag(
                    nukeXpBar != null
                        ? $"EnsureNukeXpBar: 重选 | bar={nukeXpBar.name} score={nukeXpBar.WiringScore} wired={nukeXpBar.IsWired}"
                        : "EnsureNukeXpBar: 未找到可用 XP 条",
                    nukeXpBar != null ? nukeXpBar : this);
            }
            else if (nukeXpBar == null)
            {
                LogDiag("EnsureNukeXpBar: 场景中无 NukeLauncherXpBar 组件");
            }
        }

        void LogInitState(string phase)
        {
            if (!diagnosticLogs)
                return;

            LogDiag(
                $"{phase} | launchButton={NukeLauncherDiagnostics.DescribeObject(launchButton)} " +
                $"fuelDeposit={NukeLauncherDiagnostics.DescribeObject(fuelDepositZone)} " +
                $"nukeVisualRoot={NukeLauncherDiagnostics.DescribeTransform(nukeVisualRoot)} " +
                $"missileSpawn={DescribeSpawnPoint()} " +
                $"cityTarget={NukeLauncherDiagnostics.DescribeTransform(cityTarget)} " +
                $"flightCamera={NukeLauncherDiagnostics.DescribeObject(flightCamera)} " +
                $"nukeXpBar={(nukeXpBar != null ? $"{nukeXpBar.name}(wired={nukeXpBar.IsWired})" : "null")} " +
                $"siloVisual={(_nukeVisual != null ? _nukeVisual.name : "null")} " +
                $"canLaunch={CanLaunchInternal(logFailure: false)}");
        }

        string DescribeSpawnPoint() =>
            missileSpawnPoint != null ? $"{missileSpawnPoint.name}@{missileSpawnPoint.position}" : "null";

        void LogDiag(string message, Object context = null)
        {
            if (!diagnosticLogs)
                return;

            NukeLauncherDiagnostics.Log(message, context != null ? context : this);
        }

        static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
                return null;

            if (root.name == name)
                return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeepChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected() => DrawExplosionRangeGizmo();

        void OnDrawGizmos()
        {
            if (!drawExplosionRangeGizmo || drawExplosionRangeOnlyWhenSelected)
                return;

            DrawExplosionRangeGizmo();
        }

        void DrawExplosionRangeGizmo()
        {
            if (!drawExplosionRangeGizmo || cityTarget == null)
                return;

            var def = ResolvePreviewNukeDef();
            if (def == null)
                return;

            var vfxRoot = ResolvePreviewExplosionVfxRoot(def);
            var center = cityTarget.position + Vector3.up * explosionVerticalOffset;
            var radii = NukeExplosionGizmos.ComputeRadii(vfxRoot, def.explosionRadius);
            NukeExplosionGizmos.Draw(
                center,
                radii,
                gizmoConfiguredColor,
                gizmoVisualColor,
                gizmoHouseDestructionColor);
            NukeExplosionGizmos.DrawLabel(center, radii);
        }

        Game.DestroyCity.Config.NukeDef ResolvePreviewNukeDef()
        {
            if (Application.isPlaying)
                return NukeUpgradeService.GetCurrentNukeDef();

            var db = Game.Core.Config.ConfigRegistry.DestroyCityNukes;
            if (db != null && db.TryGetByLevel(Mathf.Max(1, editorPreviewNukeLevel), out var def))
                return def;

            return NukeUpgradeService.GetCurrentNukeDef();
        }

        GameObject ResolvePreviewExplosionVfxRoot(Game.DestroyCity.Config.NukeDef def)
        {
            if (explosionEffectPrefab != null)
                return explosionEffectPrefab;

            if (def != null && def.explosionVfxPrefab != null)
                return def.explosionVfxPrefab;

            return null;
        }
#endif
    }
}
