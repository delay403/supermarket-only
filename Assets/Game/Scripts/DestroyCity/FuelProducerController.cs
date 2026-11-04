using System.Collections.Generic;
using Game.Interaction;
using Game.Save;
using UnityEngine;

namespace Game.DestroyCity
{
    /// <summary>
    /// 场景中的燃料生产器：绑定生产按钮、点位、自动 tick。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FuelProducerController : MonoBehaviour
    {
        [SerializeField] private string producerId = FuelProducerIds.Manual;
        [SerializeField] private Transform[] spawnPoints = System.Array.Empty<Transform>();
        [SerializeField] private FuelProducerProduceButton produceButton;
        [SerializeField] private FuelProducerUpgradeUi upgradeUi;
        [SerializeField] private FuelProducerUnlockUi unlockUi;
        [SerializeField] private GameObject lockedVisualRoot;
        [SerializeField] private GameObject activeVisualRoot;

        [Header("燃料实例")]
        [Tooltip("优先使用；留空则从 FuelDatabase 按等级读取 worldPrefab。")]
        [SerializeField] private FuelInstanceView fuelInstancePrefab;
        [SerializeField] private Transform fuelInstanceRoot;

        [Header("液滴动效")]
        [SerializeField] private FuelProducerDropletEmitter dropletEmitter;

        [Header("交互")]
        [SerializeField] private FuelProducerInteractionHub interactionHub;

        readonly Dictionary<string, FuelInstanceView> _views = new Dictionary<string, FuelInstanceView>();
        readonly Dictionary<string, int> _viewFuelLevels = new Dictionary<string, int>();
        bool _fuelViewsInitialized;
        float _autoAccumulator;
        string _displayInstanceId;

        public string ProducerId => producerId;

        void OnEnable()
        {
            FuelProducerService.ProductionApplied += OnProductionApplied;
            FuelProducerService.FuelInstancesChanged += RefreshFuelViews;
            FuelProducerService.ProducerUnlocked += OnProducerUnlocked;
            FuelProducerService.ProducerUpgraded += OnProducerUpgraded;
            FuelProducerService.RefreshWorldAreaUnlocks();
        }

        void OnDisable()
        {
            FuelProducerService.ProductionApplied -= OnProductionApplied;
            FuelProducerService.FuelInstancesChanged -= RefreshFuelViews;
            FuelProducerService.ProducerUnlocked -= OnProducerUnlocked;
            FuelProducerService.ProducerUpgraded -= OnProducerUpgraded;
        }

        void Start()
        {
            if (produceButton != null)
                produceButton.Bind(this);
            if (upgradeUi == null)
                upgradeUi = GetComponentInChildren<FuelProducerUpgradeUi>(true);
            if (upgradeUi != null)
                upgradeUi.Bind(this);
            if (unlockUi == null)
                unlockUi = lockedVisualRoot != null
                    ? lockedVisualRoot.GetComponentInChildren<FuelProducerUnlockUi>(true)
                    : GetComponentInChildren<FuelProducerUnlockUi>(true);
            if (unlockUi != null)
                unlockUi.Bind(this);
            if (dropletEmitter == null)
                dropletEmitter = GetComponentInChildren<FuelProducerDropletEmitter>(true);
            interactionHub?.Bind(produceButton);

            FuelProducerService.SyncProducerDisplayFromStoredXp(producerId);
            RefreshLockVisual();
            RefreshFuelViews();
        }

        void Update()
        {
            var def = FuelProducerService.ResolveDef(producerId);
            if (def == null || def.isManual || !FuelProducerService.IsUnlocked(producerId))
                return;

            _autoAccumulator += Time.deltaTime;
            var interval = Mathf.Max(0.1f, def.autoIntervalSeconds);
            var steps = 0;
            while (_autoAccumulator >= interval
                   && steps < FuelProducerService.AutoProduceStepsPerFrameLimit)
            {
                _autoAccumulator -= interval;
                steps++;
                if (FuelProducerService.TryAutoProduce(producerId))
                    PlayProduceFeedback(false);
            }
        }

        void LateUpdate()
        {
            if (producerId == FuelProducerIds.Manual)
                FuelProducerService.FlushPendingSave();
        }

        void RefreshFuelBarOnly()
        {
            if (!FuelProducerService.TryGetDisplayInstance(producerId, out var inst))
                return;

            if (_views.TryGetValue(inst.instanceId, out var view) && view != null)
            {
                view.Bind(inst);
                return;
            }

            RefreshFuelViews();
        }

        public void OnProduceButtonPressed()
        {
            if (!FuelProducerService.IsUnlocked(producerId))
            {
                TryPromptUnlock();
                return;
            }

            var produced = FuelProducerService.TryManualProduce(producerId);
            if (produced)
            {
                PlayProduceFeedback(true);
                return;
            }

            Debug.LogWarning(
                $"[FuelProducer] 手动生产失败 producer={producerId} unlocked={FuelProducerService.IsUnlocked(producerId)}",
                this);
        }

        public void OnUpgradeButtonPressed()
        {
            FuelProducerService.TryUpgradeProducer(producerId);
        }

        void TryPromptUnlock()
        {
            if (unlockUi != null)
                unlockUi.TryUnlockFromPrompt();
        }

        void OnProductionApplied(string id, float xp)
        {
            if (id != producerId)
                return;

            DestroyCityPresentationFx.PlayFuelXpGain(ResolveProducerFloaterPosition());
            DestroyCityXpFloater.Show(ResolveProducerFloaterPosition(), xp);
            RefreshFuelBarOnly();
        }
        void OnProducerUnlocked(string id)
        {
            if (id != producerId)
                return;
            RefreshLockVisual();
            upgradeUi?.Refresh();
        }

        void OnProducerUpgraded(string id, int _)
        {
            if (id != producerId)
                return;
            RefreshLockVisual();
            upgradeUi?.Refresh();
            DestroyCityPresentationFx.PlayFuelUpgrade(ResolveProducerUpgradeFxPosition());
        }

        Vector3 ResolveProducerUpgradeFxPosition()
        {
            if (upgradeUi != null)
                return upgradeUi.transform.position;

            return ResolveProducerFloaterPosition();
        }

        Vector3 ResolveProducerFloaterPosition()
        {
            if (dropletEmitter != null && dropletEmitter.SpawnPoint != null)
                return dropletEmitter.SpawnPoint.position;

            if (fuelInstanceRoot != null)
                return fuelInstanceRoot.position + Vector3.up * 1.5f;

            return transform.position + Vector3.up * 2f;
        }

        void RefreshLockVisual()
        {
            var unlocked = FuelProducerService.IsUnlocked(producerId);
            if (lockedVisualRoot != null) lockedVisualRoot.SetActive(!unlocked);
            if (activeVisualRoot != null) activeVisualRoot.SetActive(unlocked);
            unlockUi?.Refresh();
        }

        void RefreshFuelViews()
        {
            if (!FuelProducerService.TryGetDisplayInstance(producerId, out var inst))
            {
                ClearFuelViews();
                _displayInstanceId = null;
                return;
            }

            _displayInstanceId = inst.instanceId;

            if (_views.TryGetValue(inst.instanceId, out var view) && view != null)
            {
                if (_viewFuelLevels.TryGetValue(inst.instanceId, out var shownLevel) && shownLevel != inst.level)
                {
                    DestroyCityPresentationFx.PlayFuelUpgrade(view.transform.position);
                    Destroy(view.gameObject);
                    _views.Remove(inst.instanceId);
                    _viewFuelLevels.Remove(inst.instanceId);
                    view = CreateView(inst);
                }
                else
                {
                    ApplySpawnTransform(view.transform, 0);
                    view.Bind(inst);
                    _viewFuelLevels[inst.instanceId] = inst.level;
                    RemoveStaleViews(inst.instanceId);
                    _fuelViewsInitialized = true;
                    return;
                }
            }
            else
            {
                view = CreateView(inst);
            }

            if (view == null)
            {
                ClearFuelViews();
                return;
            }

            _views[inst.instanceId] = view;
            if (_fuelViewsInitialized)
                DestroyCityPresentationFx.PlayFuelUpgrade(view.transform.position);

            ApplySpawnTransform(view.transform, 0);
            view.Bind(inst);
            _viewFuelLevels[inst.instanceId] = inst.level;
            RemoveStaleViews(inst.instanceId);
            _fuelViewsInitialized = true;
        }

        void RemoveStaleViews(string keepInstanceId)
        {
            var remove = new List<string>();
            foreach (var kv in _views)
            {
                if (kv.Key != keepInstanceId)
                    remove.Add(kv.Key);
            }

            for (var i = 0; i < remove.Count; i++)
            {
                if (_views.TryGetValue(remove[i], out var v) && v != null)
                    Destroy(v.gameObject);
                _views.Remove(remove[i]);
                _viewFuelLevels.Remove(remove[i]);
            }
        }

        void ClearFuelViews()
        {
            foreach (var kv in _views)
            {
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            }

            _views.Clear();
            _viewFuelLevels.Clear();
        }

        FuelInstanceView CreateView(DestroyCityModeSaveData.FuelInstanceSaveEntry inst)
        {
            var level = Mathf.Max(1, inst.level);
            var prefabGo = FuelPrefabResolver.ResolveWorldPrefab(fuelInstancePrefab, level);
            if (prefabGo == null)
            {
                var db = Game.Core.Config.ConfigRegistry.DestroyCityFuels;
                Debug.LogError(
                    "[FuelProducer] 未配置燃料预制体：请在 FuelProducerController 指定 Fuel Instance Prefab，" +
                    $"或在 Resources/{Game.DestroyCity.Config.DestroyCityResourcePaths.FuelDatabase} 的 FuelDef 配置 worldPrefab。" +
                    $" (fuelLevel={level}, dbLoaded={db != null}, dbFuelCount={db?.fuels?.Count ?? 0})", this);
                return null;
            }

            var parent = fuelInstanceRoot != null ? fuelInstanceRoot : transform;
            var go = Instantiate(prefabGo, parent);
            var view = go.GetComponent<FuelInstanceView>();
            if (view == null)
            {
                Debug.LogError(
                    $"[FuelProducer] 燃料预制体 {prefabGo.name} 缺少 FuelInstanceView 组件。", go);
                Destroy(go);
                return null;
            }

            ApplySpawnTransform(view.transform, 0);
            EnsureInteractableLayer(view.gameObject);
            return view;
        }

        void ApplySpawnTransform(Transform target, int slotIndex)
        {
            var pos = ResolveSpawnPosition(slotIndex);
            target.SetPositionAndRotation(pos, Quaternion.identity);
        }

        Vector3 ResolveSpawnPosition(int slotIndex)
        {
            var lateralStep = 0.85f;
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var idx = slotIndex % spawnPoints.Length;
                var ring = slotIndex / spawnPoints.Length;
                var point = spawnPoints[idx];
                if (point != null)
                {
                    var pos = point.position;
                    if (ring > 0)
                        pos += point.right * (ring * lateralStep);
                    return pos;
                }
            }

            return transform.position + transform.right * (slotIndex * lateralStep);
        }

        static void EnsureInteractableLayer(GameObject go)
        {
            var layer = LayerMask.NameToLayer(DestroyCityPlayerSetup.InteractableLayerName);
            if (layer < 0)
                return;

            SetLayerRecursive(go, layer);
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (var i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        void PlayProduceFeedback(bool manual)
        {
            if (manual)
            {
                if (produceButton != null)
                    produceButton.PlayPressAnimation();
                DestroyCityPresentationFx.PlayFuelProduceButton(ResolveProducerFloaterPosition());
            }

            EmitDropletsToCurrentFuels();
        }

        static readonly List<FuelInstanceView> DropletTargetScratch = new List<FuelInstanceView>(1);

        void EmitDropletsToCurrentFuels()
        {
            if (dropletEmitter == null)
                return;

            if (string.IsNullOrWhiteSpace(_displayInstanceId))
                return;

            if (!_views.TryGetValue(_displayInstanceId, out var view) || view == null)
                return;

            DropletTargetScratch.Clear();
            DropletTargetScratch.Add(view);
            dropletEmitter.EmitToFuelTargets(
                DropletTargetScratch,
                ResolveDropletTargetPosition,
                OnDropletArrivedForInstance);
        }

        void OnDropletArrivedForInstance(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return;
            if (!_views.TryGetValue(instanceId, out var view) || view == null)
                return;
            view.NotifyDropletArrived();
        }

        Vector3 ResolveDropletTargetPosition(FuelInstanceView view)
        {
            if (view == null)
                return Vector3.zero;
            return ResolveDropletTargetPosition(view.transform);
        }

        static Vector3 ResolveDropletTargetPosition(Transform fuelTransform)
        {
            if (fuelTransform == null)
                return Vector3.zero;

            var renderers = fuelTransform.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return fuelTransform.position;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.center;
        }
    }
}
