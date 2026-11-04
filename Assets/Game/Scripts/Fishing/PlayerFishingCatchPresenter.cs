using Game.Fishing.Config;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 钓获结算：在鱼线终点生成鱼模型，收线期间跟随线端。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFishingCatchPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerFishingController fishing;
        [SerializeField] private PlayerFishingLineDriver lineDriver;
        [SerializeField] private FishingPresentationFx presentationFx;
        [SerializeField] private float catchModelLifetimeSeconds = 8f;
        [SerializeField] private float catchModelScale = 1.5f;

        FishingZone _sessionZone;
        FishingLineVisual _lineVisual;
        bool _searchedLineVisual;

        void Awake()
        {
            if (fishing == null)
                fishing = GetComponent<PlayerFishingController>();
            if (lineDriver == null)
                lineDriver = GetComponent<PlayerFishingLineDriver>();
            if (presentationFx == null)
                presentationFx = GetComponent<FishingPresentationFx>();
        }

        void OnEnable()
        {
            if (fishing == null) return;
            fishing.SessionStarted += OnSessionStarted;
            fishing.CatchCompleted += OnCatchCompleted;
        }

        void OnDisable()
        {
            if (fishing == null) return;
            fishing.SessionStarted -= OnSessionStarted;
            fishing.CatchCompleted -= OnCatchCompleted;
        }

        void OnSessionStarted(FishingSessionState _)
        {
            _sessionZone = fishing != null ? fishing.ActiveZone : null;
            CacheLineVisual();
        }

        void OnCatchCompleted(FishingService.CatchResult result)
        {
            if (!result.success || result.fish == null)
            {
                Debug.LogWarning("[钓鱼] 收杆完成但未抽中鱼（请检查 FishingZoneDef.fishPool 是否已配置 FishDef）。");
                return;
            }

            var fish = result.fish;
            var name = string.IsNullOrEmpty(fish.displayName) ? fish.fishId : fish.displayName;
            Debug.Log(
                $"[钓鱼] 钓获成功：{name} (id={fish.fishId})，获得金币 {result.coinsAwarded}（品质 {fish.quality}）");

            CacheLineVisual();
            var spawnPos = ResolveSpawnPosition();
            SpawnCatchModel(fish, spawnPos);
        }

        void CacheLineVisual()
        {
            if (_searchedLineVisual && _lineVisual != null)
                return;
            _searchedLineVisual = true;
            _lineVisual = GetComponentInChildren<FishingLineVisual>(true);
        }

        Vector3 ResolveSpawnPosition()
        {
            if (_lineVisual != null)
                return _lineVisual.CurrentLineEndWorld;

            if (_sessionZone != null)
                return _sessionZone.GetCastTargetWorldPosition();

            if (fishing?.ActiveZone != null)
                return fishing.ActiveZone.GetCastTargetWorldPosition();

            return transform.position + transform.forward * 2f;
        }

        void SpawnCatchModel(FishDef fish, Vector3 worldPosition)
        {
            var prefab = fish.worldCatchPrefab;
            if (prefab == null)
            {
                Debug.Log($"[钓鱼] FishDef '{fish.fishId}' 未配置 worldCatchPrefab，跳过场景模型生成。");
                return;
            }

            var instance = Instantiate(prefab, worldPosition, Quaternion.Euler(0f, 180f, 0f));
            instance.transform.localScale = prefab.transform.localScale * catchModelScale;
            if (_lineVisual != null && _lineVisual.EnableLineEffects)
            {
                if (presentationFx == null)
                    presentationFx = GetComponent<FishingPresentationFx>();

                var follower = instance.GetComponent<FishingCatchLineFollower>();
                if (follower == null)
                    follower = instance.AddComponent<FishingCatchLineFollower>();
                follower.Bind(_lineVisual, presentationFx, default);
                lineDriver?.RegisterCatchFollower(follower);
            }
            else if (catchModelLifetimeSeconds > 0f)
            {
                Destroy(instance, catchModelLifetimeSeconds);
            }
        }
    }
}
