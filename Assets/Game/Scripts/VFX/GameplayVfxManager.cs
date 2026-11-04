using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Pooled spawning for particle-style gameplay FX. Assign prefabs in Inspector (or Bootstrapper Instantiate hook).
    /// One prefab instance can serve multiple logical <see cref="VfxKind"/> entries if you reuse the asset.
    /// </summary>
    public sealed class GameplayVfxManager : MonoBehaviour
    {
        [Serializable]
        public sealed class VfxPrefabBinding
        {
            public VfxKind kind;

            [Tooltip("含 ParticleSystem 的预制根；可被多个 kind 重复使用。")]
            public GameObject prefab;

            [Min(1)]
            public int poolSize = 6;
        }

        public static GameplayVfxManager Instance { get; private set; }

        [SerializeField] private List<VfxPrefabBinding> bindings = new List<VfxPrefabBinding>();

        [SerializeField]
        [Tooltip(
            "BorrowWorld 使用自动回收（releaseAfterSeconds &lt; 0）时，若预制体上存在任一 Looping 粒子，会与估算寿命取较大值后归还池中；避免 Loop 误开导致实例永不 Release。")]
        [Min(0.2f)]
        private float minAutoReleaseSecondsIfAnyLoopingParticle = 6f;

        private readonly Dictionary<VfxKind, int> _kindToLane = new Dictionary<VfxKind, int>();
        private PrefabLane[] _lanes;
        private Transform _poolParent;

#if UNITY_EDITOR
        [SerializeField] private bool logMissingPrefabsAtRuntime = true;
#endif

        public static GameplayVfxManager EnsureCreated()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject(nameof(GameplayVfxManager));
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameplayVfxManager>();
            return Instance;
        }

        /// <summary>注册来自代码或其它系统的绑定（例如在 Bootstrapper 中由 ScriptableObject 注入）。不会在运行期合并重复 kind。</summary>
        public static void ReplaceRuntimeBindings(IReadOnlyList<VfxPrefabBinding> list)
        {
            var inst = EnsureCreated();
            inst.bindings = list != null ? new List<VfxPrefabBinding>(list) : new List<VfxPrefabBinding>();
            inst.RebuildLanes();
        }

        /// <summary>按 <see cref="VfxKind"/> 合并绑定；同 kind 后者覆盖前者。</summary>
        public static void MergeRuntimeBindings(IReadOnlyList<VfxPrefabBinding> list)
        {
            if (list == null || list.Count == 0)
                return;

            var inst = EnsureCreated();
            for (var i = 0; i < list.Count; i++)
            {
                var binding = list[i];
                if (binding == null || binding.prefab == null)
                    continue;

                var replaced = false;
                for (var j = 0; j < inst.bindings.Count; j++)
                {
                    if (inst.bindings[j].kind != binding.kind)
                        continue;

                    inst.bindings[j] = binding;
                    replaced = true;
                    break;
                }

                if (!replaced)
                    inst.bindings.Add(binding);
            }

            inst.RebuildLanes();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            RebuildLanes();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void RebuildLanes()
        {
            _kindToLane.Clear();

            foreach (Transform c in transform)
                Destroy(c.gameObject);

            _poolParent = new GameObject("VfxPools").transform;
            _poolParent.SetParent(transform, worldPositionStays: false);

            var laneDefs = BuildLaneDefinitions(bindings, out var maxLane);
            _lanes = new PrefabLane[maxLane + 1];
            foreach (var d in laneDefs)
            {
                _lanes[d.laneIndex] = new PrefabLane
                {
                    prefab = d.prefab,
                    poolRoot = CreateLaneRoot($"Pool_{d.laneIndex}_{d.prefab.name}"),
                    free = new Queue<GameObject>(),
                };

                for (var i = 0; i < d.poolSize; i++)
                {
                    var go = Instantiate(d.prefab, _lanes[d.laneIndex].poolRoot);
                    go.name = d.prefab.name;
                    PrepInstance(go, d.laneIndex);
                    StopAndClear(go);
                    go.SetActive(false);
                    _lanes[d.laneIndex].free.Enqueue(go);
                }

                foreach (var k in d.kindsCoveringPrefab)
                    _kindToLane[k] = d.laneIndex;
            }

#if UNITY_EDITOR
            WarnMissingKinds();
#endif
        }

#if UNITY_EDITOR
        private void WarnMissingKinds()
        {
            if (!logMissingPrefabsAtRuntime)
                return;
            foreach (VfxKind v in Enum.GetValues(typeof(VfxKind)))
            {
                if (!_kindToLane.ContainsKey(v))
                    Debug.LogWarning($"[GameplayVfxManager] No prefab binding for VfxKind.{v}. Assign in Inspector。", this);
            }
        }
#endif

        private static List<LaneDef> BuildLaneDefinitions(List<VfxPrefabBinding> list, out int maxLane)
        {
            var result = new List<LaneDef>();
            maxLane = -1;

            if (list == null || list.Count == 0)
                return result;

            var prefabIdentity = new Dictionary<int, LaneDef>(); // prefab InstanceID → lane

            foreach (var b in list)
            {
                if (b == null || b.prefab == null || b.poolSize < 1)
                    continue;

                var pid = b.prefab.GetInstanceID();

                // Collapse lanes that share identical prefabs.
                if (prefabIdentity.TryGetValue(pid, out var existing))
                {
                    if (!existing.kindsCoveringPrefab.Contains(b.kind))
                        existing.kindsCoveringPrefab.Add(b.kind);
                    existing.poolSize = Mathf.Max(existing.poolSize, b.poolSize);
                    continue;
                }

                maxLane++;
                var laneIdx = maxLane;

                existing = new LaneDef
                {
                    laneIndex = laneIdx,
                    prefab = b.prefab,
                    poolSize = Mathf.Max(1, b.poolSize),
                    kindsCoveringPrefab = new List<VfxKind> { b.kind },
                };
                prefabIdentity.Add(pid, existing);
                result.Add(existing);
            }

            foreach (var d in prefabIdentity.Values)
                d.poolSize = Mathf.Max(1, d.poolSize);

            return result;
        }

        private Transform CreateLaneRoot(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_poolParent, worldPositionStays: false);
            return t;
        }

        /// <returns>借阅的实例；不用时务必 <see cref="Release"/>。</returns>
        /// <param name="autoReleaseAfterSeconds">
        /// 0：不自动回池（持续型跟随特效）。
        /// &lt; 0：按粒子寿命自动回池。
        /// &gt; 0：固定秒数后回池。
        /// </param>
        public GameObject BorrowFollow(
            VfxKind kind,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            float autoReleaseAfterSeconds = 0f)
        {
            var go = Dequeue(kind);
            if (go == null)
                return null;

            var t = go.transform;
            t.SetParent(parent, worldPositionStays: false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = localScale;

            ConfigureParticlesForFollow(go);
            PlayAll(go);

            var binder = go.GetComponent<PooledVfxInstance>();
            if (binder != null)
                binder.KindId = kind;

            if (autoReleaseAfterSeconds > 1e-4f)
                StartCoroutine(ReturnAfter(go, autoReleaseAfterSeconds));
            else if (autoReleaseAfterSeconds < -1e-4f)
                StartCoroutine(ReturnAfter(go, EstimateAutoReleaseSeconds(go)));

            return go;
        }

        /// <param name="releaseAfterSeconds">
        /// &lt; 0：根据子粒子估算寿命；若含 loop 粒子，会与 <see cref="minAutoReleaseSecondsIfAnyLoopingParticle"/> 取较大值后自动回池。
        /// 0：不调度回收（用于持续型世界特效）。
        /// &gt; 0：固定秒数后回池。
        /// </param>
        public void BorrowWorld(VfxKind kind, Vector3 worldPosition, Quaternion worldRotation, Vector3 scale, float releaseAfterSeconds = -1f)
        {
            TryBorrowWorld(kind, worldPosition, worldRotation, scale, releaseAfterSeconds);
        }

        /// <returns>成功借出的实例；池空或未绑定时为 null。</returns>
        public GameObject TryBorrowWorld(
            VfxKind kind,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 scale,
            float releaseAfterSeconds = -1f)
        {
            var go = Dequeue(kind);
            if (go == null)
                return null;

            var t = go.transform;
            t.SetParent(null, worldPositionStays: true);
            t.position = worldPosition;
            t.rotation = worldRotation;
            t.localScale = scale;

            PlayAll(go);

            var binder = go.GetComponent<PooledVfxInstance>();
            if (binder != null)
                binder.KindId = kind;

            if (releaseAfterSeconds > 1e-4f)
                StartCoroutine(ReturnAfter(go, releaseAfterSeconds));
            else if (releaseAfterSeconds < 0f)
            {
                var sec = EstimateNonLoopingFinishSeconds(go);
                if (HasLoopingParticle(go))
                    sec = Mathf.Max(sec, minAutoReleaseSecondsIfAnyLoopingParticle);
                StartCoroutine(ReturnAfter(go, sec));
            }

            return go;
        }

        /// <returns>需手动 <see cref="Release"/>；不自动回池（releaseAfterSeconds = 0）。</returns>
        public GameObject BorrowWorldPersistent(VfxKind kind, Vector3 worldPosition, Quaternion worldRotation, Vector3 scale) =>
            TryBorrowWorld(kind, worldPosition, worldRotation, scale, releaseAfterSeconds: 0f);

        public void Release(GameObject instance)
        {
            if (instance == null || _lanes == null)
                return;

            var binder = instance.GetComponent<PooledVfxInstance>();
            if (binder == null || binder.Owner != this || binder.PoolLaneIndex < 0 || binder.PoolLaneIndex >= _lanes.Length)
            {
                Destroy(instance);
                return;
            }

            StopAndClear(instance);
            instance.SetActive(false);
            instance.transform.SetParent(_lanes[binder.PoolLaneIndex].poolRoot, worldPositionStays: false);
            _lanes[binder.PoolLaneIndex].free.Enqueue(instance);
        }

        public bool HasKind(VfxKind kind) =>
            _kindToLane != null && _kindToLane.ContainsKey(kind);

        public float EstimateAutoReleaseSeconds(GameObject go)
        {
            if (go == null)
                return 2f;

            var sec = EstimateNonLoopingFinishSeconds(go);
            if (HasLoopingParticle(go))
                sec = Mathf.Max(sec, minAutoReleaseSecondsIfAnyLoopingParticle);
            return sec;
        }

        private GameObject Dequeue(VfxKind kind)
        {
            if (_lanes == null || !_kindToLane.TryGetValue(kind, out var lane))
                return null;

            GameObject instance;
            if (_lanes[lane].free.Count > 0)
                instance = _lanes[lane].free.Dequeue();
            else
            {
                instance = Instantiate(_lanes[lane].prefab, _lanes[lane].poolRoot);
                instance.name = _lanes[lane].prefab.name + "_grow";
                PrepInstance(instance, lane);
#if UNITY_EDITOR
                Debug.LogWarning($"[GameplayVfxManager] Pool lane {lane} grew at runtime。", this);
#endif
            }

            instance.SetActive(true);
            return instance;
        }

        private IEnumerator ReturnAfter(GameObject go, float sec)
        {
            yield return new WaitForSeconds(sec);
            Release(go);
        }

        private static bool HasLoopingParticle(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                if (systems[i].main.loop)
                    return true;
            }

            return false;
        }

        private static float EstimateNonLoopingFinishSeconds(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            var max = 0f;
            for (var i = 0; i < systems.Length; i++)
            {
                var m = systems[i].main;
                if (m.loop)
                    continue;

                var life = m.startLifetime.constantMax;
                var d = m.duration + life;
                max = Mathf.Max(max, d);
            }

            if (max <= 0.01f)
                max = 2f;
            return Mathf.Clamp(max, 0.2f, 45f);
        }

        private void PrepInstance(GameObject go, int laneIndex)
        {
            var binder = go.GetComponent<PooledVfxInstance>() ?? go.AddComponent<PooledVfxInstance>();
            binder.Owner = this;
            binder.PoolLaneIndex = laneIndex;

            EnsureChildParticleCollisionOffIfNeeded(go);
        }

        private static void EnsureChildParticleCollisionOffIfNeeded(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var c = systems[i].collision;
                if (c.enabled)
                    Debug.LogWarning(
                        $"Particle collision enabled on pooled FX '{systems[i].name}'. Consider disabling for pooled性能。",
                        systems[i]);
            }
        }

        private static void ConfigureParticlesForFollow(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        private static void PlayAll(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                if (!main.loop)
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].Play(true);
            }
        }

        private static void StopAndClear(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private struct PrefabLane
        {
            public GameObject prefab;
            public Transform poolRoot;
            public Queue<GameObject> free;
        }

        private sealed class LaneDef
        {
            public int laneIndex;
            public GameObject prefab;

            /// <summary>Max pool size amongst bindings sharing prefab。</summary>
            public int poolSize;

            public List<VfxKind> kindsCoveringPrefab = new List<VfxKind>();
        }
    }
}
