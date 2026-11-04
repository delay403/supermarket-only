using System.Collections;
using Game.Audio;
using Game.Core;
using Game.Fishing.Config;
using Game.Player;
using Game.Save;
using Game.VFX;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 钓鱼水花、水面涟漪、钓获脚下庆祝粒子与音效。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingPresentationFx : MonoBehaviour
    {
        const float WaterSplashLifetimeSeconds = 0.5f;

        [SerializeField] private PlayerFishingController fishing;
        [SerializeField] private PlayerFishingLineDriver lineDriver;
        [SerializeField] private PlayerRig rig;
        [SerializeField] private Transform footVfxAnchor;

        [Header("可选：未在 GameplayVfxManager 绑定时使用")]
        [SerializeField] private GameObject waterSplashPrefabOverride;
        [SerializeField] private GameObject waterRipplePrefabOverride;
        [SerializeField] private GameObject footCelebratePrefabOverride;
        [SerializeField] private FishingCatchCelebrateLibrary celebrateLibrary;

        [Header("涟漪")]
        [SerializeField] private float waterRippleLifetimeSeconds = 2.5f;
        [SerializeField] private float ambientRippleIntervalMin = 0.5f;
        [SerializeField] private float ambientRippleIntervalMax = 2f;

        FishingLineVisual _lineVisual;
        bool _searchedLineVisual;
        GameObject _footCelebrateInstance;
        GameObject _footCelebrateOverrideInstance;
        Coroutine _celebrateReleaseRoutine;
        Coroutine _ambientRippleRoutine;

        void Awake()
        {
            if (fishing == null) fishing = GetComponent<PlayerFishingController>();
            if (lineDriver == null) lineDriver = GetComponent<PlayerFishingLineDriver>();
            if (rig == null) rig = GetComponent<PlayerRig>();
            if (celebrateLibrary == null)
                celebrateLibrary = Resources.Load<FishingCatchCelebrateLibrary>("FishingCatchCelebrateLibrary");
            ResolveFootAnchor();
        }

        void OnEnable()
        {
            if (fishing != null)
            {
                fishing.SessionStarted += OnSessionStarted;
                fishing.GameplayStarted += OnGameplayStarted;
                fishing.SessionCancelled += OnFishingSessionEnded;
                fishing.CatchCompleted += OnCatchCompleted;
                fishing.ReelApplied += OnReelApplied;
            }

            if (lineDriver != null)
                lineDriver.PostCatchPresentationEnded += OnPostCatchPresentationEnded;

            BindLineVisual();
        }

        void OnDisable()
        {
            if (fishing != null)
            {
                fishing.SessionStarted -= OnSessionStarted;
                fishing.GameplayStarted -= OnGameplayStarted;
                fishing.SessionCancelled -= OnFishingSessionEnded;
                fishing.CatchCompleted -= OnCatchCompleted;
                fishing.ReelApplied -= OnReelApplied;
            }

            if (lineDriver != null)
                lineDriver.PostCatchPresentationEnded -= OnPostCatchPresentationEnded;

            UnbindLineVisual();
            StopFootCelebrate();
            StopAmbientRipples();
        }

        void OnSessionStarted(FishingSessionState _)
        {
            StopFootCelebrate();
            StopAmbientRipples();
            ResolveFootAnchor();
            RefreshLineReference();
        }

        void OnGameplayStarted() => StartAmbientRipples();

        void OnFishingSessionEnded() => StopAmbientRipples();

        void OnCatchCompleted(FishingService.CatchResult _) => StopAmbientRipples();

        void OnReelApplied(FishingService.ReelResult reel)
        {
            if (reel.completed)
                return;

            PlayReelCelebrateFx();
        }

        void OnPostCatchPresentationEnded(bool naturalComplete)
        {
            StopFootCelebrate();
        }

        void BindLineVisual()
        {
            CacheLineVisual();
            if (_lineVisual != null)
                _lineVisual.LineHitWater += OnLineHitWater;
        }

        void UnbindLineVisual()
        {
            if (_lineVisual != null)
                _lineVisual.LineHitWater -= OnLineHitWater;
        }

        public void RefreshLineReference()
        {
            UnbindLineVisual();
            _searchedLineVisual = false;
            _lineVisual = null;
            BindLineVisual();
        }

        void CacheLineVisual()
        {
            if (_searchedLineVisual && _lineVisual != null)
                return;
            _searchedLineVisual = true;
            _lineVisual = GetComponentInChildren<FishingLineVisual>(true);
        }

        void OnLineHitWater(Vector3 worldPosition)
        {
            PlayWaterSplash(worldPosition, "CastLineHitWater");
        }

        public void PlayWaterSplash(Vector3 worldPosition, string source = "Unknown")
        {
            var spawned = false;

            var mgr = GameplayVfxManager.Instance;
            if (mgr != null && mgr.HasKind(VfxKind.FishingWaterSplash))
            {
                var pooled = mgr.TryBorrowWorld(
                    VfxKind.FishingWaterSplash,
                    worldPosition,
                    Quaternion.identity,
                    Vector3.one,
                    WaterSplashLifetimeSeconds);
                if (pooled != null)
                    spawned = true;
            }

            if (!spawned && waterSplashPrefabOverride != null)
            {
                var instance = SpawnOneShotOverride(waterSplashPrefabOverride, worldPosition, null);
                if (instance != null)
                {
                    Destroy(instance, WaterSplashLifetimeSeconds);
                    spawned = true;
                }
            }

            if (spawned)
                AudioGameplayFeedback.PlayFishingWaterSplash(worldPosition);

            PlayWaterRipple(worldPosition, source);
        }

        public void PlayWaterRipple(Vector3 worldPosition, string source = "Unknown")
        {
            var lifetime = ResolveRippleLifetime();
            var rotation = Quaternion.identity;
            var spawned = false;

            var mgr = GameplayVfxManager.Instance;
            if (mgr != null && mgr.HasKind(VfxKind.FishingWaterRipple))
            {
                var pooled = mgr.TryBorrowWorld(
                    VfxKind.FishingWaterRipple,
                    worldPosition,
                    rotation,
                    Vector3.one,
                    lifetime);
                if (pooled != null)
                    spawned = true;
            }

            if (!spawned && waterRipplePrefabOverride != null)
            {
                var instance = SpawnOneShotOverride(waterRipplePrefabOverride, worldPosition, null, rotation);
                if (instance != null)
                {
                    Destroy(instance, lifetime);
                    spawned = true;
                }
            }

            if (!spawned)
                CreateRuntimeRipple(worldPosition, rotation, lifetime);
        }

        float ResolveRippleLifetime()
        {
            if (waterRipplePrefabOverride != null)
            {
                var fx = waterRipplePrefabOverride.GetComponent<FishingWaterRippleFx>();
                if (fx != null)
                    return fx.LifetimeSeconds;
            }

            return Mathf.Max(0.5f, waterRippleLifetimeSeconds);
        }

        GameObject CreateRuntimeRipple(Vector3 worldPosition, Quaternion rotation, float lifetime)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "FishingWaterRipple_Runtime";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(worldPosition, rotation);

            var mat = Resources.Load<Material>("Materials/FishingWaterRipple");
            if (mat != null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = mat;
            }

            var rippleFx = go.AddComponent<FishingWaterRippleFx>();
            Destroy(go, lifetime);
            return go;
        }

        void StartAmbientRipples()
        {
            StopAmbientRipples();
            _ambientRippleRoutine = StartCoroutine(CoAmbientRipplesDuringFishing());
        }

        void StopAmbientRipples()
        {
            if (_ambientRippleRoutine == null)
                return;

            StopCoroutine(_ambientRippleRoutine);
            _ambientRippleRoutine = null;
        }

        IEnumerator CoAmbientRipplesDuringFishing()
        {
            var minInterval = Mathf.Max(0.05f, ambientRippleIntervalMin);
            var maxInterval = Mathf.Max(minInterval, ambientRippleIntervalMax);

            while (fishing != null && fishing.IsGameplayActive)
            {
                yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

                if (fishing == null || !fishing.IsGameplayActive)
                    yield break;

                if (TryResolveWaterSurfacePosition(out var pos))
                    PlayWaterSplash(pos, "AmbientDuringFishing");
            }
        }

        public bool TryResolveWaterSurfacePosition(out Vector3 splashPos)
        {
            splashPos = default;
            CacheLineVisual();
            if (_lineVisual == null)
                return false;

            if (_lineVisual.TryGetLineWaterContact(out splashPos))
                return true;

            if (fishing?.ActiveZone != null)
            {
                var castTarget = fishing.ActiveZone.GetCastTargetWorldPosition();
                splashPos = new Vector3(castTarget.x, _lineVisual.WaterSurfaceY, castTarget.z);
                return true;
            }

            return false;
        }

        void StartFootCelebrate()
        {
            StopFootCelebrate();
            ResolveFootAnchor();

            var anchor = footVfxAnchor != null ? footVfxAnchor : transform;
            var mgr = GameplayVfxManager.Instance;
            if (mgr != null)
            {
                _footCelebrateInstance = mgr.BorrowFollow(
                    VfxKind.FishingCatchCelebrate,
                    anchor,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one);
            }

            if (_footCelebrateInstance == null)
            {
                _footCelebrateOverrideInstance = SpawnOneShotOverride(
                    footCelebratePrefabOverride,
                    anchor.position,
                    anchor);
            }
        }

        /// <summary>按当前装备角色在脚下播放庆祝特效（抽杆时调用）。</summary>
        public void PlayReelCelebrateFx() => PlayCharacterCelebrateFx();

        /// <summary>保留旧 API；行为与 <see cref="PlayReelCelebrateFx"/> 相同。</summary>
        public void PlayCatchCelebrateSyncedWithToast() => PlayCharacterCelebrateFx();

        void PlayCharacterCelebrateFx()
        {
            StopFootCelebrate();
            ResolveFootAnchor();

            var characterId = SaveService.Data?.equippedCharacterId;
            var entry = celebrateLibrary != null
                ? celebrateLibrary.Resolve(characterId)
                : null;

            var duration = entry != null && entry.playDurationSeconds > 0f
                ? entry.playDurationSeconds
                : 2f;

            var prefab = entry != null ? entry.celebratePrefab : null;
            if (prefab == null)
                prefab = footCelebratePrefabOverride;

            PlayFootCelebrateInternal(prefab, duration);
        }

        /// <summary>AI 钓获展示：在 FootRoot 播放庆祝特效；prefab 为空则走 VFX 池。</summary>
        public void PlayFootCelebrateForAi(GameObject prefabOverride, float durationSeconds)
        {
            StopFootCelebrate();
            ResolveFootAnchor();
            PlayFootCelebrateInternal(prefabOverride, durationSeconds);
        }

        void PlayFootCelebrateInternal(GameObject prefabOverride, float durationSeconds)
        {
            var anchor = footVfxAnchor != null ? footVfxAnchor : transform;
            if (prefabOverride != null)
            {
                _footCelebrateOverrideInstance = SpawnOneShotOverride(prefabOverride, anchor.position, anchor);
            }
            else
            {
                StartFootCelebrate();
            }

            if (_footCelebrateInstance != null || _footCelebrateOverrideInstance != null)
                _celebrateReleaseRoutine = StartCoroutine(CoReleaseCelebrateAfter(durationSeconds));
        }

        public Vector3 FootWorldPosition => GetFootWorldPosition();

        public SfxId ResolveCelebrateSfx()
        {
            var characterId = SaveService.Data?.equippedCharacterId;
            var entry = celebrateLibrary != null
                ? celebrateLibrary.Resolve(characterId)
                : null;
            var sfx = entry != null ? entry.celebrateSfx : SfxId.FishingCatchCelebrate1;
            return sfx != SfxId.None ? sfx : SfxId.FishingCatchCelebrate1;
        }

        IEnumerator CoReleaseCelebrateAfter(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));
            _celebrateReleaseRoutine = null;
            StopFootCelebrate();
        }

        public void StopFootCelebrate()
        {
            if (_celebrateReleaseRoutine != null)
            {
                StopCoroutine(_celebrateReleaseRoutine);
                _celebrateReleaseRoutine = null;
            }

            if (_footCelebrateInstance != null)
            {
                var mgr = GameplayVfxManager.Instance;
                if (mgr != null)
                    mgr.Release(_footCelebrateInstance);
                _footCelebrateInstance = null;
            }

            if (_footCelebrateOverrideInstance != null)
            {
                Destroy(_footCelebrateOverrideInstance);
                _footCelebrateOverrideInstance = null;
            }
        }

        void ResolveFootAnchor()
        {
            if (footVfxAnchor != null && footVfxAnchor.name == CharacterHierarchySockets.FootRootTransformName)
                return;

            rig?.EnsureSceneSocketsResolved();
            if (rig?.footSocket != null)
            {
                footVfxAnchor = rig.footSocket;
                return;
            }

            var characterRoot = rig != null ? rig.characterSocket : null;
            if (characterRoot == null)
                characterRoot = transform;

            var found = CharacterHierarchySockets.FindDeepChildByName(
                characterRoot,
                CharacterHierarchySockets.FootRootTransformName);
            if (found != null)
                footVfxAnchor = found;
        }

        Vector3 GetFootWorldPosition() =>
            footVfxAnchor != null ? footVfxAnchor.position : transform.position;

        GameObject SpawnOneShotOverride(
            GameObject prefab,
            Vector3 worldPosition,
            Transform parent,
            Quaternion? worldRotation = null)
        {
            if (prefab == null)
                return null;

            var rotation = worldRotation ?? Quaternion.identity;
            var instance = parent != null
                ? Instantiate(prefab, parent)
                : Instantiate(prefab, worldPosition, rotation);

            if (parent != null)
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }

            return instance;
        }
    }
}
