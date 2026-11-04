using System;
using System.Collections;
using System.Collections.Generic;
using Game.Audio;
using Game.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Effects
{
    /// <summary>
    /// 可复用金币飞入特效：支持世界坐标 / 屏幕坐标起点，随机曲线飞向目标 UI。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UICoinFlyEffect : MonoBehaviour
    {
        public static UICoinFlyEffect Instance { get; private set; }

        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private RectTransform flyRoot;
        [SerializeField] private UICoinFlyParticle coinPrefab;
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private RectTransform defaultTarget;
        [SerializeField] private UICoinFlySettings defaults = new UICoinFlySettings();
        [SerializeField] private int prewarmPoolSize = 12;

        readonly Queue<UICoinFlyParticle> _pool = new Queue<UICoinFlyParticle>();
        RectTransform _rootRect;
        RectTransform _defaultTargetRegistered;

        sealed class CoinGrantTracker
        {
            public long Total;
            public long Granted;
            public bool Enabled;
            public Action<long> OnCoinArrived;

            public void Grant(long chunk)
            {
                if (!Enabled || chunk <= 0)
                    return;

                EconomyCoinFlyGrant.GrantChunk(chunk);
                Granted += chunk;
                OnCoinArrived?.Invoke(chunk);
            }

            public void GrantUnspawnedChunks(long[] chunks, int spawnedCount)
            {
                if (!Enabled || chunks == null)
                {
                    if (Enabled && Granted == 0 && Total > 0)
                        Grant(Total);
                    return;
                }

                for (var i = spawnedCount; i < chunks.Length; i++)
                    Grant(chunks[i]);
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            EnsureHierarchy();
            PrewarmPool();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static UICoinFlyEffect EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var existing = FindObjectOfType<UICoinFlyEffect>(true);
            if (existing != null)
                return existing;

            var canvas = FindObjectOfType<Canvas>();
            var go = new GameObject(nameof(UICoinFlyEffect));
            if (canvas != null)
                go.transform.SetParent(canvas.transform, false);

            return go.AddComponent<UICoinFlyEffect>();
        }

        public void RegisterDefaultTarget(RectTransform target, int priority = 0)
        {
            defaultTarget = target;
            _defaultTargetRegistered = target;
            if (target != null)
                UICoinFlyTarget.Register(target, priority);
        }

        /// <summary>从世界坐标播放金币飞入。</summary>
        public void PlayFromWorld(Vector3 worldPosition, Camera worldCamera, long coinAmount) =>
            Play(new UICoinFlyPlayParams
            {
                WorldPosition = worldPosition,
                WorldCamera = worldCamera,
                CoinAmount = coinAmount,
            });

        /// <summary>从屏幕坐标播放金币飞入。</summary>
        public void PlayFromScreen(Vector2 screenPosition, long coinAmount) =>
            Play(new UICoinFlyPlayParams
            {
                ScreenPosition = screenPosition,
                CoinAmount = coinAmount,
            });

        /// <summary>完整参数播放。</summary>
        public void Play(UICoinFlyPlayParams parameters)
        {
            if (parameters.CoinAmount <= 0)
                return;

            EnsureHierarchy();
            StartCoroutine(CoPlay(parameters));
        }

        IEnumerator CoPlay(UICoinFlyPlayParams parameters)
        {
            var settings = defaults ?? new UICoinFlySettings();
            var grantTracker = new CoinGrantTracker
            {
                Total = parameters.CoinAmount,
                Enabled = parameters.GrantCoinsOnArrival ?? settings.grantCoinsOnArrival,
                OnCoinArrived = parameters.OnCoinArrived,
            };

            long[] coinChunks = null;
            var spawnedCount = 0;

            try
            {
                var delay = parameters.StartDelay ?? settings.startDelay;
                if (delay > 0f)
                {
                    var elapsed = 0f;
                    while (elapsed < delay)
                    {
                        if (!isActiveAndEnabled)
                            yield break;

                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (!TryResolveScreenOrigin(parameters, out var screenOrigin))
                    yield break;

                var target = ResolveTarget(parameters.Target);
                if (target == null)
                {
                    Debug.LogWarning("[UICoinFlyEffect] 未找到飞行目标 RectTransform。");
                    yield break;
                }

                if (!TryScreenToFlyLocal(screenOrigin, out var startLocal)
                    || !TryGetTargetLocalPosition(target, out var endLocal))
                {
                    yield break;
                }

                var count = ResolveVisualCoinCount(parameters, settings);
                if (count <= 0)
                    yield break;

                coinChunks = grantTracker.Enabled
                    ? EconomyCoinFlyGrant.SplitAmount(parameters.CoinAmount, count)
                    : null;

                var offsetRadius = parameters.SpawnOffsetRadius ?? settings.spawnOffsetRadius;
                var durationBase = parameters.MoveDuration ?? settings.moveDuration;
                var durationJitter = parameters.MoveDurationJitter ?? settings.moveDurationJitter;
                var arcStrengthBase = parameters.ArcStrength ?? settings.arcStrength;
                var moveCurve = parameters.MoveCurve ?? settings.moveCurve;
                var arcCurve = parameters.ArcCurve ?? settings.arcCurve;
                var sprite = ResolveCoinSprite(target);
                var spawnInterval = parameters.CoinSpawnInterval ?? settings.coinSpawnInterval;
                var playCollectSfx = parameters.PlayCollectSfx ?? settings.playCollectSfx;
                var playCollectSfxOnce = parameters.PlayCollectSfxOnce ?? false;
                var collectSfxVolume = parameters.CollectSfxVolume ?? settings.collectSfxVolume;
                var collectSfxMoveProgress = parameters.CollectSfxMoveProgress ?? 1f;
                var collectSfxPlayedForBatch = false;

                for (var i = 0; i < count; i++)
                {
                    if (i > 0 && spawnInterval > 0f)
                    {
                        var intervalElapsed = 0f;
                        while (intervalElapsed < spawnInterval)
                        {
                            if (!isActiveAndEnabled)
                                yield break;

                            intervalElapsed += Time.unscaledDeltaTime;
                            yield return null;
                        }
                    }

                    var offset = new Vector2(
                        UnityEngine.Random.Range(-offsetRadius.x, offsetRadius.x),
                        UnityEngine.Random.Range(-offsetRadius.y, offsetRadius.y));
                    var start = startLocal + offset;
                    var arcSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                    var arcStrength = arcStrengthBase * UnityEngine.Random.Range(
                        settings.arcStrengthRandom.x,
                        settings.arcStrengthRandom.y);
                    var duration = durationBase + UnityEngine.Random.Range(0f, durationJitter);
                    var grantChunk = coinChunks != null && i < coinChunks.Length ? coinChunks[i] : 0L;

                    Action onCollectSfx = null;
                    float? sfxProgress = null;
                    if (playCollectSfx)
                    {
                        sfxProgress = collectSfxMoveProgress;
                        var volume = collectSfxVolume;
                        onCollectSfx = () =>
                        {
                            if (playCollectSfxOnce && collectSfxPlayedForBatch)
                                return;

                            if (playCollectSfxOnce)
                                collectSfxPlayedForBatch = true;

                            AudioGameplayFeedback.PlayResourceCollect(volume);
                        };
                    }

                    var particle = Rent();
                    particle.EnsureImage(sprite);
                    particle.Play(
                        _rootRect,
                        start,
                        endLocal,
                        arcSign,
                        arcStrength,
                        duration,
                        settings.coinSize,
                        settings.scaleRange,
                        moveCurve,
                        arcCurve,
                        sfxProgress,
                        onCollectSfx,
                        p => OnParticleFinished(p, grantTracker, grantChunk));
                    spawnedCount++;
                }
            }
            finally
            {
                grantTracker.GrantUnspawnedChunks(coinChunks, spawnedCount);
            }
        }

        void OnParticleFinished(
            UICoinFlyParticle particle,
            CoinGrantTracker grantTracker,
            long grantChunk)
        {
            grantTracker?.Grant(grantChunk);
            ReturnToPool(particle);
        }

        void EnsureHierarchy()
        {
            if (targetCanvas == null)
                targetCanvas = GetComponentInParent<Canvas>();

            if (targetCanvas == null)
            {
                var canvasGo = new GameObject("UICoinFlyCanvas");
                canvasGo.transform.SetParent(transform, false);
                targetCanvas = canvasGo.AddComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.sortingOrder = 5000;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            if (flyRoot == null)
            {
                var rootGo = new GameObject("FlyRoot", typeof(RectTransform));
                rootGo.transform.SetParent(targetCanvas.transform, false);
                flyRoot = rootGo.GetComponent<RectTransform>();
                flyRoot.anchorMin = Vector2.zero;
                flyRoot.anchorMax = Vector2.one;
                flyRoot.offsetMin = Vector2.zero;
                flyRoot.offsetMax = Vector2.zero;
            }

            _rootRect = flyRoot;
        }

        void PrewarmPool()
        {
            var count = Mathf.Max(0, prewarmPoolSize);
            for (var i = _pool.Count; i < count; i++)
                CreatePooledParticle();
        }

        UICoinFlyParticle Rent()
        {
            if (_pool.Count == 0)
                CreatePooledParticle();

            var particle = _pool.Dequeue();
            particle.gameObject.SetActive(false);
            return particle;
        }

        void ReturnToPool(UICoinFlyParticle particle)
        {
            if (particle == null)
                return;

            particle.gameObject.SetActive(false);
            _pool.Enqueue(particle);
        }

        void CreatePooledParticle()
        {
            UICoinFlyParticle particle;
            if (coinPrefab != null)
            {
                particle = Instantiate(coinPrefab, _rootRect);
            }
            else
            {
                var go = new GameObject("CoinFlyParticle", typeof(RectTransform));
                go.transform.SetParent(_rootRect, false);
                particle = go.AddComponent<UICoinFlyParticle>();
            }

            particle.EnsureImage(coinSprite);
            particle.gameObject.SetActive(false);
            _pool.Enqueue(particle);
        }

        static bool TryResolveScreenOrigin(UICoinFlyPlayParams parameters, out Vector2 screenOrigin)
        {
            if (parameters.ScreenPosition.HasValue)
            {
                screenOrigin = parameters.ScreenPosition.Value;
                return true;
            }

            if (parameters.WorldPosition.HasValue)
            {
                var cam = parameters.WorldCamera != null ? parameters.WorldCamera : Camera.main;
                if (cam == null)
                {
                    screenOrigin = default;
                    return false;
                }

                var world = parameters.WorldPosition.Value;
                var screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                {
                    screenOrigin = default;
                    return false;
                }

                screenOrigin = new Vector2(screen.x, screen.y);
                return true;
            }

            screenOrigin = default;
            return false;
        }

        bool TryScreenToFlyLocal(Vector2 screenPoint, out Vector2 localPoint)
        {
            var eventCam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootRect,
                screenPoint,
                eventCam,
                out localPoint);
        }

        bool TryGetTargetLocalPosition(RectTransform target, out Vector2 localPoint)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var center = (corners[0] + corners[2]) * 0.5f;
            var screen = RectTransformUtility.WorldToScreenPoint(
                targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera,
                center);

            return TryScreenToFlyLocal(screen, out localPoint);
        }

        RectTransform ResolveTarget(RectTransform overrideTarget)
        {
            if (overrideTarget != null)
                return overrideTarget;
            if (defaultTarget != null)
                return defaultTarget;
            if (_defaultTargetRegistered != null)
                return _defaultTargetRegistered;
            return UICoinFlyTarget.Primary != null ? UICoinFlyTarget.Primary.TargetRect : null;
        }

        Sprite ResolveCoinSprite(RectTransform target)
        {
            if (coinSprite != null)
                return coinSprite;

            if (target != null && target.TryGetComponent<Image>(out var image) && image.sprite != null)
                return image.sprite;

            return null;
        }

        static int ResolveVisualCoinCount(UICoinFlyPlayParams parameters, UICoinFlySettings settings)
        {
            if (parameters.VisualCoinCount.HasValue)
                return Mathf.Clamp(parameters.VisualCoinCount.Value, 1, settings.maxVisualCoins);

            if (parameters.CoinAmount <= 0)
                return 0;

            var log = Mathf.Log10(Mathf.Max(1f, parameters.CoinAmount));
            var scaled = settings.minVisualCoins + Mathf.RoundToInt(log * 3f);
            return Mathf.Clamp(scaled, settings.minVisualCoins, settings.maxVisualCoins);
        }
    }
}
