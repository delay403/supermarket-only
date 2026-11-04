using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.DestroyCity
{
    /// <summary>
    /// 爆炸波及的 House 层物体：按包围盒与当前爆炸半径判定，平滑下沉/分帧复位。
    /// </summary>
    public static class NukeHouseDestructionService
    {
        const string HouseLayerName = "House";
        const float SinkDistance = 5f;
        const float SinkDuration = 0.35f;
        const float ResetDelayBeforeStart = 3f;
        const float ResetDuration = 0.5f;
        const int ResetStartsPerFrame = 8;
        public const long CoinsPerHouseDestroyed = 500L;

        struct HouseEntry
        {
            public Transform Transform;
            public float BaselineY;
            public bool Sunk;
            public bool Resetting;
        }

        static readonly List<HouseEntry> Houses = new List<HouseEntry>();
        static readonly List<Renderer> BoundsScratch = new List<Renderer>(8);
        static NukeHouseDestructionRunner s_runner;
        static int s_houseLayer = int.MinValue;
        static bool s_cached;

        public static void EnsureCached()
        {
            if (s_cached)
                return;

            EnsureRunner();
            s_houseLayer = LayerMask.NameToLayer(HouseLayerName);
            Houses.Clear();
            if (s_houseLayer < 0)
            {
                s_cached = true;
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                s_cached = true;
                return;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                CollectHouseRoots(roots[i].transform);

            s_cached = true;
        }

        public static void InvalidateCache()
        {
            s_cached = false;
            Houses.Clear();
        }

        static void EnsureRunner()
        {
            if (s_runner != null)
                return;

            s_runner = Object.FindObjectOfType<NukeHouseDestructionRunner>();
            if (s_runner != null)
                return;

            var go = new GameObject(nameof(NukeHouseDestructionRunner));
            s_runner = go.AddComponent<NukeHouseDestructionRunner>();
        }

        static void CollectHouseRoots(Transform node)
        {
            if (node == null)
                return;

            if (node.gameObject.layer == s_houseLayer && HasRenderableBounds(node))
            {
                Houses.Add(new HouseEntry
                {
                    Transform = node,
                    BaselineY = node.position.y,
                });
                return;
            }

            for (var i = 0; i < node.childCount; i++)
                CollectHouseRoots(node.GetChild(i));
        }

        static bool HasRenderableBounds(Transform root)
        {
            BoundsScratch.Clear();
            root.GetComponentsInChildren(false, BoundsScratch);
            for (var i = 0; i < BoundsScratch.Count; i++)
            {
                if (BoundsScratch[i] != null && BoundsScratch[i].enabled)
                    return true;
            }

            return false;
        }

        static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            BoundsScratch.Clear();
            root.GetComponentsInChildren(false, BoundsScratch);

            var found = false;
            for (var i = 0; i < BoundsScratch.Count; i++)
            {
                var renderer = BoundsScratch[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        public static void ProcessExplosionWave(Vector3 center, float currentWorldRadius)
        {
            if (currentWorldRadius <= 0f)
                return;

            EnsureCached();
            if (Houses.Count == 0)
                return;

            EnsureRunner();
            var radiusSqr = currentWorldRadius * currentWorldRadius;
            for (var i = 0; i < Houses.Count; i++)
            {
                var entry = Houses[i];
                if (entry.Sunk || entry.Resetting || entry.Transform == null)
                    continue;

                if (!TryGetCombinedBounds(entry.Transform, out var bounds))
                    continue;

                var closest = bounds.ClosestPoint(center);
                if ((closest - center).sqrMagnitude > radiusSqr)
                    continue;

                entry.Sunk = true;
                Houses[i] = entry;
                s_runner.AnimateMoveY(entry.Transform, entry.BaselineY - SinkDistance, SinkDuration);
                DestroyCityCoinFlyPresenter.TryPlayFromHouseDestroyed(bounds.center, CoinsPerHouseDestroyed, Camera.main);
            }
        }

        public static void ScheduleResetSunkHouses()
        {
            EnsureCached();
            EnsureRunner();

            var batch = new List<(Transform transform, float baselineY)>();
            for (var i = 0; i < Houses.Count; i++)
            {
                var entry = Houses[i];
                if (!entry.Sunk || entry.Resetting || entry.Transform == null)
                    continue;

                entry.Resetting = true;
                Houses[i] = entry;
                batch.Add((entry.Transform, entry.BaselineY));
            }

            if (batch.Count == 0)
                return;

            s_runner.StartResetBatch(batch, ResetDelayBeforeStart, ResetDuration, ResetStartsPerFrame, OnHouseResetFinished);
        }

        static void OnHouseResetFinished(Transform transform)
        {
            for (var i = 0; i < Houses.Count; i++)
            {
                if (Houses[i].Transform != transform)
                    continue;

                var entry = Houses[i];
                entry.Sunk = false;
                entry.Resetting = false;
                Houses[i] = entry;
                return;
            }
        }

        public static float ComputeWorldRadiusFromScale(float currentUniformScale) =>
            Mathf.Max(0f, currentUniformScale) * NukeExplosionEffect.HouseDestructionScaleDivisor;
    }

    sealed class NukeHouseDestructionRunner : MonoBehaviour
    {
        readonly Dictionary<Transform, Coroutine> _active = new Dictionary<Transform, Coroutine>();

        public void AnimateMoveY(Transform target, float targetY, float duration)
        {
            if (target == null)
                return;

            if (_active.TryGetValue(target, out var existing) && existing != null)
                StopCoroutine(existing);

            _active[target] = StartCoroutine(CoMoveY(target, target.position.y, targetY, duration, null));
        }

        public void StartResetBatch(
            List<(Transform transform, float baselineY)> entries,
            float delayBeforeStart,
            float duration,
            int startsPerFrame,
            System.Action<Transform> onFinished)
        {
            StartCoroutine(CoResetBatch(entries, delayBeforeStart, duration, startsPerFrame, onFinished));
        }

        IEnumerator CoResetBatch(
            List<(Transform transform, float baselineY)> entries,
            float delayBeforeStart,
            float duration,
            int startsPerFrame,
            System.Action<Transform> onFinished)
        {
            if (delayBeforeStart > 0f)
                yield return new WaitForSeconds(delayBeforeStart);

            var cursor = 0;
            while (cursor < entries.Count)
            {
                var batch = Mathf.Min(startsPerFrame, entries.Count - cursor);
                for (var i = 0; i < batch; i++)
                {
                    var entry = entries[cursor + i];
                    if (entry.transform == null)
                        continue;

                    AnimateMoveY(entry.transform, entry.baselineY, duration);
                }

                cursor += batch;
                yield return null;
            }

            yield return new WaitForSeconds(duration);

            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].transform != null)
                    onFinished?.Invoke(entries[i].transform);
            }
        }

        IEnumerator CoMoveY(Transform target, float fromY, float toY, float duration, System.Action onComplete)
        {
            if (target == null)
                yield break;

            duration = Mathf.Max(0.05f, duration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null)
                    yield break;

                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                var pos = target.position;
                pos.y = Mathf.Lerp(fromY, toY, t);
                target.position = pos;
                yield return null;
            }

            if (target != null)
            {
                var pos = target.position;
                pos.y = toY;
                target.position = pos;
            }

            _active.Remove(target);
            onComplete?.Invoke();
        }
    }
}
