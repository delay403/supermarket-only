using System.Collections.Generic;
using Game.Core;
using Game.Economy;
using Game.UI.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 管理 HUD 上多个限时优惠入口按钮；挂于 GameHudUGUI 的 container 上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DiscountOfferHudController : MonoBehaviour
    {
        [SerializeField] private RectTransform entryContainer;
        [SerializeField] private DiscountOfferHudEntryView entryPrefab;

        readonly Dictionary<int, DiscountOfferHudEntryView> _entries = new Dictionary<int, DiscountOfferHudEntryView>();

        void OnEnable()
        {
            DiscountOfferService.Changed += RefreshAll;
            GameplayModuleContext.ModuleChanged += OnModuleChanged;
            RefreshAll();
        }

        void OnDisable()
        {
            DiscountOfferService.Changed -= RefreshAll;
            GameplayModuleContext.ModuleChanged -= OnModuleChanged;
        }

        void OnModuleChanged(GameplayModule previous, GameplayModule current)
        {
            RefreshAll();
        }

        public void RefreshAll()
        {
            if (!DiscountOfferSaveBridge.IsActive)
            {
                ClearAllEntries();
                return;
            }
            DiscountOfferService.TickEntryExpiry();
            var db = DiscountOfferService.Database;
            if (db?.offers == null || entryContainer == null || entryPrefab == null)
                return;

            var activeIds = new HashSet<int>();
            for (var i = 0; i < db.offers.Length; i++)
            {
                var offer = db.offers[i];
                var id = offer.offerId;
                if (id <= 0 || !DiscountOfferSaveBridge.MatchesScope(offer))
                    continue;
                if (!DiscountOfferService.IsHudEntryActive(id))
                    continue;

                activeIds.Add(id);
                if (!_entries.TryGetValue(id, out var view) || view == null)
                {
                    view = Instantiate(entryPrefab, entryContainer);
                    view.OpenRequested += OnEntryOpenRequested;
                    _entries[id] = view;
                }

                view.gameObject.SetActive(true);
                view.Bind(id, offer);
            }

            var toRemove = new List<int>();
            foreach (var pair in _entries)
            {
                if (activeIds.Contains(pair.Key))
                    continue;

                if (pair.Value != null)
                {
                    pair.Value.OpenRequested -= OnEntryOpenRequested;
                    Destroy(pair.Value.gameObject);
                }

                toRemove.Add(pair.Key);
            }

            for (var i = 0; i < toRemove.Count; i++)
                _entries.Remove(toRemove[i]);
        }

        public void TickTimers()
        {
            foreach (var pair in _entries)
                pair.Value?.RefreshTimer();
        }

        void OnEntryOpenRequested(int offerId)
        {
            UiRouter.OpenDiscountOffer(offerId);
        }

        void ClearAllEntries()
        {
            var toRemove = new List<int>(_entries.Keys);
            for (var i = 0; i < toRemove.Count; i++)
            {
                if (!_entries.TryGetValue(toRemove[i], out var view) || view == null)
                    continue;

                view.OpenRequested -= OnEntryOpenRequested;
                Destroy(view.gameObject);
                _entries.Remove(toRemove[i]);
            }
        }
    }
}
