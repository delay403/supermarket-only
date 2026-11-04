using System.Collections.Generic;
using Game.Collectibles;
using Game.Core.Config;
using Game.Fishing.Config;
using Game.Player;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Fishing
{
    /// <summary>
    /// 钓鱼区世界空间概率公示：根据 <see cref="FishingZoneDef.fishPool"/> 填充 Grid 内 FishItem。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingZoneProbabilityBoard : MonoBehaviour
    {
        [SerializeField] private FishingZone fishingZone;
        [SerializeField] private FishingZoneDef zoneDefOverride;
        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private FishingZoneProbabilityItemView itemPrefab;
        [SerializeField] private Text titleText;
        [SerializeField] private string titleLabel = "概率公示";
        [SerializeField] private bool useSpecialCatchTable;
        [SerializeField] private int maxVisibleItems = 9;
        [SerializeField] private QualityVisualLibrary qualityVisualLibrary;

        readonly List<FishingZoneProbabilityItemView> _spawnedItems = new List<FishingZoneProbabilityItemView>(9);
        PlayerFishingController _fishingController;

        void Awake()
        {
            ResolveGridRoot();
            Refresh();
        }

        void OnEnable()
        {
            BindFishingController();
            if (_fishingController != null)
                _fishingController.CatchCompleted += OnCatchCompleted;
        }

        void OnDisable()
        {
            if (_fishingController != null)
                _fishingController.CatchCompleted -= OnCatchCompleted;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (gridRoot == null)
                ResolveGridRoot();
        }
#endif

        public void Refresh(FishingZone zone)
        {
            if (zone != null)
                fishingZone = zone;
            Refresh();
        }

        public void Refresh()
        {
            ResolveGridRoot();

            var zoneDef = ResolveZoneDef();
            if (zoneDef == null)
            {
                HideAllItems();
                return;
            }

            if (titleText != null && !string.IsNullOrEmpty(titleLabel))
                titleText.text = titleLabel;

            var lib = qualityVisualLibrary != null ? qualityVisualLibrary : ConfigRegistry.QualityVisuals;
            var rows = FishingCatchProbability.BuildDisclosureRows(zoneDef, useSpecialCatchTable);
            var limit = Mathf.Min(maxVisibleItems, rows.Count);
            EnsureItemViews(limit);

            for (var i = 0; i < _spawnedItems.Count; i++)
            {
                var view = _spawnedItems[i];
                if (view == null)
                    continue;

                if (i < limit)
                {
                    var row = rows[i];
                    var fishId = row.Fish != null ? row.Fish.fishId : string.Empty;
                    var discovered = !string.IsNullOrEmpty(fishId) && FishCodexService.IsDiscovered(fishId);
                    view.Apply(row.Fish, row.Probability01, discovered, i == limit - 1, lib);
                    view.SetVisible(true);
                }
                else
                {
                    view.SetVisible(false);
                }
            }
        }

        void OnCatchCompleted(FishingService.CatchResult _) => Refresh();

        void BindFishingController()
        {
            if (_fishingController != null)
                return;

            var motor = FindObjectOfType<PlayerMotor>();
            if (motor != null)
                _fishingController = motor.GetComponent<PlayerFishingController>();
        }

        FishingZoneDef ResolveZoneDef()
        {
            if (zoneDefOverride != null)
                return zoneDefOverride;

            if (fishingZone == null)
                fishingZone = GetComponentInParent<FishingZone>();

            return fishingZone != null ? fishingZone.ZoneDef : null;
        }

        void ResolveGridRoot()
        {
            if (gridRoot != null)
                return;

            var grid = transform.Find("Grid");
            if (grid != null)
                gridRoot = grid as RectTransform;

            if (titleText == null)
            {
                var title = transform.Find("Title");
                if (title != null)
                    titleText = title.GetComponent<Text>();
            }
        }

        void EnsureItemViews(int requiredCount)
        {
            _spawnedItems.Clear();
            if (gridRoot == null)
                return;

            for (var i = 0; i < gridRoot.childCount; i++)
            {
                var child = gridRoot.GetChild(i);
                var view = child.GetComponent<FishingZoneProbabilityItemView>();
                if (view == null)
                    view = child.gameObject.AddComponent<FishingZoneProbabilityItemView>();
                _spawnedItems.Add(view);
            }

            while (_spawnedItems.Count < requiredCount)
            {
                if (itemPrefab == null)
                    break;

                var instance = Instantiate(itemPrefab, gridRoot);
                _spawnedItems.Add(instance);
            }
        }

        void HideAllItems()
        {
            for (var i = 0; i < _spawnedItems.Count; i++)
            {
                if (_spawnedItems[i] != null)
                    _spawnedItems[i].SetVisible(false);
            }
        }
    }
}
