using System.Collections.Generic;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>管理各难度地图显隐，并为首页/导演提供货架统计与激活货架列表。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeDifficultyManager : MonoBehaviour
    {
        public static SupermarketOrganizeDifficultyManager Instance { get; private set; }

        [SerializeField] private SupermarketOrganizeDifficultySetup[] setups;

        SupermarketOrganizeDifficultySetup _activeSetup;

        public SupermarketOrganizeDifficultySetup ActiveSetup => _activeSetup;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SupermarketOrganizeDifficultyManager] Duplicate instance ignored.", this);
                return;
            }

            Instance = this;
            ResolveSetups();
            HideAll();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static SupermarketOrganizeDifficultyManager ResolveRuntime()
        {
            if (Instance != null)
                return Instance;

#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<SupermarketOrganizeDifficultyManager>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<SupermarketOrganizeDifficultyManager>(true);
#endif
        }

        public static bool TryGetStats(string difficultyId, out int shelfCount, out int itemCount)
        {
            shelfCount = 0;
            itemCount = 0;
            var manager = ResolveRuntime();
            if (manager == null || !manager.TryGetSetup(difficultyId, out var setup))
                return false;

            setup.ComputeCounts(out shelfCount, out itemCount);
            return true;
        }

        public void ResolveSetups()
        {
            if (setups == null || setups.Length == 0)
            {
#if UNITY_2023_1_OR_NEWER
                setups = FindObjectsByType<SupermarketOrganizeDifficultySetup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
                setups = FindObjectsOfType<SupermarketOrganizeDifficultySetup>(true);
#endif
            }

            if (setups == null)
                return;

            for (var i = 0; i < setups.Length; i++)
                setups[i]?.ResolveReferences();
        }

        public void HideAll()
        {
            ResolveSetups();
            if (setups == null)
                return;

            for (var i = 0; i < setups.Length; i++)
                setups[i]?.SetContentVisible(false);

            _activeSetup = null;
        }

        public bool ApplyDifficulty(string difficultyId)
        {
            ResolveSetups();
            HideAll();

            if (!TryGetSetup(difficultyId, out var setup))
            {
                Debug.LogWarning(
                    $"[SupermarketOrganizeDifficultyManager] 未找到难度场景配置：{difficultyId}",
                    this);
                return false;
            }

            setup.SetContentVisible(true);
            _activeSetup = setup;
            return true;
        }

        public IReadOnlyList<SupermarketContainerZone> GetActiveShelves()
        {
            return _activeSetup != null
                ? _activeSetup.GetShelves()
                : System.Array.Empty<SupermarketContainerZone>();
        }

        public bool TryGetSetup(string difficultyId, out SupermarketOrganizeDifficultySetup setup)
        {
            setup = null;
            if (string.IsNullOrWhiteSpace(difficultyId))
                return false;

            ResolveSetups();
            if (setups == null)
                return false;

            var id = difficultyId.Trim();
            for (var i = 0; i < setups.Length; i++)
            {
                var candidate = setups[i];
                if (candidate == null)
                    continue;

                if (candidate.DifficultyId == id)
                {
                    setup = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
