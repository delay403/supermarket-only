using System;
using System.Collections.Generic;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>超市收纳核心玩法服务：拾取、放置、胜利判定。</summary>
    public sealed class SupermarketOrganizeService
    {
        readonly SupermarketOrganizeConfig _config;
        readonly SupermarketHandleZone _handleZone;
        readonly SupermarketItemHighlightController _highlights;
        readonly List<SupermarketContainerZone> _zones = new List<SupermarketContainerZone>();
        readonly List<SupermarketItem> _groundItems = new List<SupermarketItem>();
        readonly Dictionary<string, List<SupermarketItem>> _itemsById = new Dictionary<string, List<SupermarketItem>>();

        int _remainingGroundCount;
        bool _gameRunning;
        bool _introFinished = true;
        float _scannerHighlightUntil;

        public event Action GameStarted;
        public event Action GameWon;
        public event Action<SupermarketItem> CurrentItemChanged;
        public event Action<int, int> HandleCountChanged;
        public event Action ProgressChanged;

        public bool IsGameRunning => _gameRunning && _introFinished;
        public int RemainingGroundCount => _remainingGroundCount;
        public SupermarketHandleZone HandleZone => _handleZone;
        public SupermarketItem CurrentItem => _handleZone != null ? _handleZone.CurrentItem : null;
        public int TotalShelfCount => _zones.Count;
        public IReadOnlyList<SupermarketContainerZone> RegisteredZones => _zones;

        public bool ContainsZone(SupermarketContainerZone zone) =>
            zone != null && _zones.Contains(zone);

        public void NotifyProgressChanged() => ProgressChanged?.Invoke();

        public int CompletedShelfCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _zones.Count; i++)
                {
                    if (_zones[i] != null && _zones[i].IsFull)
                        count++;
                }

                return count;
            }
        }

        public int TotalItemSlotCount
        {
            get
            {
                var total = 0;
                for (var i = 0; i < _zones.Count; i++)
                {
                    if (_zones[i] != null)
                        total += _zones[i].Capacity;
                }

                return total;
            }
        }

        public int PlacedItemCount
        {
            get
            {
                var total = 0;
                for (var i = 0; i < _zones.Count; i++)
                {
                    if (_zones[i] != null)
                        total += _zones[i].PlacedCount;
                }

                return total;
            }
        }

        public IReadOnlyList<SupermarketItem> GroundItems => _groundItems;

        public bool TryCycleCurrentHeldItem()
        {
            if (_handleZone == null || _handleZone.Count <= 1)
                return false;

            return _handleZone.TryCycleCurrentItem();
        }

        public SupermarketOrganizeService(
            SupermarketOrganizeConfig config,
            SupermarketHandleZone handleZone,
            SupermarketItemHighlightController highlights)
        {
            _config = config;
            _handleZone = handleZone;
            _highlights = highlights;

            if (_handleZone != null)
                _handleZone.CurrentItemChanged += OnHandleCurrentItemChanged;
        }

        public void RegisterZone(SupermarketContainerZone zone)
        {
            if (zone == null || _zones.Contains(zone))
                return;

            _zones.Add(zone);
        }

        public void RegisterItem(SupermarketItem item)
        {
            if (item == null)
                return;

            if (!_itemsById.TryGetValue(item.ItemId, out var list))
            {
                list = new List<SupermarketItem>();
                _itemsById[item.ItemId] = list;
            }

            if (!list.Contains(item))
                list.Add(item);

            if (item.State == SupermarketItemState.OnGround && !_groundItems.Contains(item))
            {
                _groundItems.Add(item);
                _remainingGroundCount++;
            }
        }

        public void BeginGameAfterIntro()
        {
            _introFinished = true;
            _gameRunning = true;
            ApplyProgressionAttributes();
            RefreshSameIdHighlights();
            NotifyProgressChanged();
            GameStarted?.Invoke();
        }

        public void SetIntroPending()
        {
            _introFinished = false;
            _gameRunning = false;
        }

        public void ResetForLobby()
        {
            _gameRunning = false;
            _introFinished = true;
            _remainingGroundCount = 0;
            _scannerHighlightUntil = 0f;
            ClearRegisteredWorldContent();
            _handleZone?.ClearAll();
            _highlights?.SetSameIdHighlights(null);
            _highlights?.SetHeldItem(null);
        }

        public void ClearRegisteredWorldContent()
        {
            _zones.Clear();
            _groundItems.Clear();
            _itemsById.Clear();
            _remainingGroundCount = 0;
        }

        public bool TryPickUpFromGround(SupermarketItem item, bool playPickUpFx = true)
        {
            if (!IsGameRunning || item == null || item.State != SupermarketItemState.OnGround)
                return false;

            var settle = item.GetComponent<SupermarketItemPhysicsSettle>();
            if (settle != null && !settle.IsSettled)
                return false;

            if (_handleZone.IsFull)
                return false;

            if (!_handleZone.TryAdd(item, out var slotIndex))
                return false;

            _groundItems.Remove(item);
            _remainingGroundCount = Mathf.Max(0, _remainingGroundCount - 1);

            item.PrepareForHandle();
            item.SetState(SupermarketItemState.Moving);

            if (_handleZone.Count >= 2)
                _handleZone.RelayoutHeldItems();

            var handleTransform = _handleZone.transform;
            item.transform.SetParent(handleTransform, true);
            item.transform.localRotation = _handleZone.GetSlotLocalRotation(slotIndex);

            var targetLocal = _handleZone.GetSlotLocalPosition(slotIndex);
            BeginArcMoveLocal(item, targetLocal, () => CompletePickUp(item, slotIndex));

            if (playPickUpFx)
                SupermarketOrganizePresentationFx.PlayPickUp(item.transform.position);
            HandleCountChanged?.Invoke(_handleZone.Count, _handleZone.MaxCapacity);
            RefreshHeldItemHighlight();
            return true;
        }

        public int TryShuffleUnplacedGroundItems(SupermarketItemSpawner spawner)
        {
            if (!IsGameRunning || spawner == null)
                return 0;

            var candidates = new List<SupermarketItem>();
            for (var i = 0; i < _groundItems.Count; i++)
            {
                var item = _groundItems[i];
                if (item == null || item.State != SupermarketItemState.OnGround)
                    continue;

                var settle = item.GetComponent<SupermarketItemPhysicsSettle>();
                if (settle != null && !settle.IsSettled)
                    continue;

                candidates.Add(item);
            }

            if (candidates.Count == 0)
                return 0;

            return spawner.RelocateGroundItems(candidates);
        }

        public void ApplyProgressionAttributes()
        {
            if (_handleZone == null)
                return;

            _handleZone.SetMaxCapacity(SupermarketOrganizeProgressionService.GetHandleCapacity());
            HandleCountChanged?.Invoke(_handleZone.Count, _handleZone.MaxCapacity);
        }

        public void ActivateScannerHighlight(float durationSeconds)
        {
            _scannerHighlightUntil = Time.time + Mathf.Max(0f, durationSeconds);
            RefreshSameIdHighlights();
        }

        public bool IsScannerHighlightActive => Time.time < _scannerHighlightUntil;

        public void TickTimedEffects()
        {
            if (_scannerHighlightUntil > 0f && Time.time >= _scannerHighlightUntil)
            {
                _scannerHighlightUntil = 0f;
                RefreshSameIdHighlights();
            }
        }

        public int TryMagnetPull(Vector3 playerPosition, float radius)
        {
            if (!IsGameRunning || _handleZone == null || _handleZone.IsFull || radius <= 0f)
                return 0;

            var freeSlots = _handleZone.MaxCapacity - _handleZone.Count;
            if (freeSlots <= 0)
                return 0;

            var candidates = new List<(SupermarketItem item, float distance)>();
            for (var i = 0; i < _groundItems.Count; i++)
            {
                var item = _groundItems[i];
                if (item == null || item.State != SupermarketItemState.OnGround)
                    continue;

                var settle = item.GetComponent<SupermarketItemPhysicsSettle>();
                if (settle != null && !settle.IsSettled)
                    continue;

                var distance = Vector3.Distance(playerPosition, item.transform.position);
                if (distance > radius)
                    continue;

                candidates.Add((item, distance));
            }

            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            var pulled = 0;
            Vector3? firstPickPos = null;
            for (var i = 0; i < candidates.Count && pulled < freeSlots; i++)
            {
                var pickPos = candidates[i].item.transform.position;
                if (!TryPickUpFromGround(candidates[i].item, playPickUpFx: false))
                    continue;

                firstPickPos ??= pickPos;
                pulled++;
            }

            if (pulled > 0 && firstPickPos.HasValue)
                SupermarketOrganizePresentationFx.PlayPickUp(firstPickPos.Value);

            return pulled;
        }

        public int TryAutoStockAllHeldItems()
        {
            if (!IsGameRunning || _handleZone == null || _handleZone.Count == 0)
                return 0;

            var placed = 0;
            while (_handleZone.Count > 0)
            {
                SupermarketItem candidate = null;
                var items = _handleZone.Items;
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null || item.State != SupermarketItemState.InHandle)
                        continue;

                    if (FindZoneForItemId(item.ItemId) != null)
                    {
                        candidate = item;
                        break;
                    }
                }

                if (candidate == null || !TryPlaceHeldItemOnMatchingShelf(candidate))
                    break;

                placed++;
            }

            return placed;
        }

        public bool TryAutoPlaceOneGroundItemToShelf(Vector3 referencePosition)
        {
            if (!IsGameRunning)
                return false;

            SupermarketItem bestItem = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < _groundItems.Count; i++)
            {
                var item = _groundItems[i];
                if (item == null || item.State != SupermarketItemState.OnGround)
                    continue;

                var settle = item.GetComponent<SupermarketItemPhysicsSettle>();
                if (settle != null && !settle.IsSettled)
                    continue;

                if (FindZoneForItemId(item.ItemId) == null)
                    continue;

                var distance = Vector3.SqrMagnitude(item.transform.position - referencePosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestItem = item;
                }
            }

            return bestItem != null && TryAutoPlaceGroundItemToShelf(bestItem);
        }

        public bool TryAutoPlaceGroundItemToShelf(SupermarketItem item)
        {
            if (!IsGameRunning || item == null || item.State != SupermarketItemState.OnGround)
                return false;

            var zone = FindZoneForItemId(item.ItemId);
            if (zone == null || !zone.TryAcceptItem(item, out var slotIndex))
                return false;

            _groundItems.Remove(item);
            _remainingGroundCount = Mathf.Max(0, _remainingGroundCount - 1);

            item.PrepareForHandle();
            item.SetState(SupermarketItemState.Moving);
            item.transform.SetParent(null, true);

            var targetWorld = zone.GetSlotWorldPosition(slotIndex);
            BeginArcMoveWorld(item, targetWorld, () => CompletePlace(item, zone, slotIndex));
            CheckVictory();
            return true;
        }

        bool TryPlaceHeldItemOnMatchingShelf(SupermarketItem item)
        {
            if (!IsGameRunning || item == null || item.State != SupermarketItemState.InHandle)
                return false;

            var zone = FindZoneForItemId(item.ItemId);
            if (zone == null || !zone.TryAcceptItem(item, out var slotIndex))
                return false;

            _handleZone.TryRemove(item);
            item.PrepareForHandle();
            item.SetState(SupermarketItemState.Moving);
            item.gameObject.SetActive(true);
            item.transform.SetParent(null, true);

            var targetWorld = zone.GetSlotWorldPosition(slotIndex);
            BeginArcMoveWorld(item, targetWorld, () => CompletePlace(item, zone, slotIndex));

            HandleCountChanged?.Invoke(_handleZone.Count, _handleZone.MaxCapacity);
            CheckVictory();
            return true;
        }

        public bool TryFindRobotTask(Vector3 robotPosition, out SupermarketItem item, out SupermarketContainerZone zone)
        {
            item = null;
            zone = null;
            if (!IsGameRunning)
                return false;

            SupermarketItem bestItem = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < _groundItems.Count; i++)
            {
                var candidate = _groundItems[i];
                if (candidate == null || candidate.State != SupermarketItemState.OnGround)
                    continue;

                var settle = candidate.GetComponent<SupermarketItemPhysicsSettle>();
                if (settle != null && !settle.IsSettled)
                    continue;

                var targetZone = FindBestPlaceZoneForItem(candidate.ItemId, robotPosition);
                if (targetZone == null)
                    continue;

                var distance = Vector3.SqrMagnitude(candidate.transform.position - robotPosition);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestItem = candidate;
                zone = targetZone;
            }

            item = bestItem;
            return item != null && zone != null;
        }

        public bool TryRobotPickUp(SupermarketItem item, Transform robotHandle, Action onComplete)
        {
            if (!IsGameRunning || item == null || robotHandle == null
                || item.State != SupermarketItemState.OnGround)
                return false;

            var settle = item.GetComponent<SupermarketItemPhysicsSettle>();
            if (settle != null && !settle.IsSettled)
                return false;

            _groundItems.Remove(item);
            _remainingGroundCount = Mathf.Max(0, _remainingGroundCount - 1);

            item.PrepareForHandle();
            item.SetState(SupermarketItemState.Moving);
            item.transform.SetParent(robotHandle, true);
            item.transform.localRotation = Quaternion.identity;

            var targetLocal = Vector3.zero;
            BeginArcMoveLocal(item, targetLocal, () =>
            {
                if (item == null)
                    return;

                item.SetState(SupermarketItemState.InRobotHandle);
                item.ReleaseTransformControl();
                onComplete?.Invoke();
            });

            SupermarketOrganizePresentationFx.PlayPickUp(item.transform.position);
            return true;
        }

        public bool TryRobotPlace(SupermarketItem item, SupermarketContainerZone zone, Action onComplete)
        {
            if (!IsGameRunning || item == null || zone == null
                || item.State != SupermarketItemState.InRobotHandle)
                return false;

            if (!zone.TryAcceptItem(item, out var slotIndex))
                return false;

            item.PrepareForHandle();
            item.SetState(SupermarketItemState.Moving);
            item.transform.SetParent(null, true);

            var targetWorld = zone.GetSlotWorldPosition(slotIndex);
            BeginArcMoveWorld(item, targetWorld, () =>
            {
                CompletePlace(item, zone, slotIndex);
                onComplete?.Invoke();
            });

            return true;
        }

        public bool TryGetRobotHeldItem(Transform robotHandle, out SupermarketItem item)
        {
            item = null;
            if (robotHandle == null)
                return false;

            for (var i = 0; i < robotHandle.childCount; i++)
            {
                var candidate = robotHandle.GetChild(i).GetComponent<SupermarketItem>();
                if (candidate == null || candidate.State != SupermarketItemState.InRobotHandle)
                    continue;

                item = candidate;
                return true;
            }

            return false;
        }

        public bool TryFindRobotPlaceTask(
            Transform robotHandle,
            Vector3 robotPosition,
            out SupermarketItem item,
            out SupermarketContainerZone zone)
        {
            item = null;
            zone = null;
            if (!IsGameRunning || !TryGetRobotHeldItem(robotHandle, out item) || item == null)
                return false;

            zone = FindBestPlaceZoneForItem(item.ItemId, robotPosition);
            return zone != null;
        }

        public SupermarketContainerZone FindBestPlaceZoneForItem(
            string itemId,
            Vector3 referencePosition,
            SupermarketContainerZone exclude = null)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            SupermarketContainerZone bestZone = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < _zones.Count; i++)
            {
                var zone = _zones[i];
                if (zone == null || zone == exclude || zone.IsFull || !zone.MatchesItem(itemId))
                    continue;

                var distance = referencePosition == Vector3.zero
                    ? 0f
                    : Vector3.SqrMagnitude(zone.transform.position - referencePosition);
                if (bestZone != null && distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestZone = zone;
            }

            return bestZone;
        }

        SupermarketContainerZone FindZoneForItemId(string itemId) =>
            FindBestPlaceZoneForItem(itemId, Vector3.zero);

        public void RecordVictory()
        {
            var save = Game.Save.SaveService.Data?.supermarketOrganize;
            if (save == null)
                return;

            save.totalRuns++;
            save.totalVictories++;
            Game.Save.SaveService.Save();
        }

        public bool TryPlaceOnShelf(SupermarketContainerZone zone)
        {
            if (!IsGameRunning || zone == null)
                return false;

            var item = _handleZone.CurrentItem;
            if (item == null || item.State != SupermarketItemState.InHandle)
                return false;

            if (!zone.TryAcceptItem(item, out var slotIndex))
                return false;

            _handleZone.TryRemove(item);
            item.PrepareForHandle();
            item.SetState(SupermarketItemState.Moving);
            item.gameObject.SetActive(true);
            item.transform.SetParent(null, true);

            var targetWorld = zone.GetSlotWorldPosition(slotIndex);
            BeginArcMoveWorld(item, targetWorld, () => CompletePlace(item, zone, slotIndex));

            HandleCountChanged?.Invoke(_handleZone.Count, _handleZone.MaxCapacity);
            CheckVictory();
            return true;
        }

        public bool EvaluateShelf(SupermarketContainerZone zone, out bool isCorrect, out bool showExtraTip)
        {
            isCorrect = false;
            showExtraTip = false;
            if (zone == null)
                return false;

            var current = CurrentItem;
            if (current == null)
                return true;

            isCorrect = zone.MatchesItem(current.ItemId);
            showExtraTip = zone.HasPlacedSameIdBefore;
            return true;
        }

        public IEnumerable<SupermarketItem> GetGroundItemsById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !_itemsById.TryGetValue(itemId, out var list))
                yield break;

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item != null && item.State == SupermarketItemState.OnGround)
                    yield return item;
            }
        }

        void CompletePickUp(SupermarketItem item, int slotIndex)
        {
            if (item == null || _handleZone == null)
                return;

            item.SetState(SupermarketItemState.InHandle);
            _handleZone.RelayoutHeldItems();
            item.ReleaseTransformControl();
            RefreshHeldItemHighlight();
            RefreshSameIdHighlights();
            CurrentItemChanged?.Invoke(_handleZone.CurrentItem);
        }

        void CompletePlace(SupermarketItem item, SupermarketContainerZone zone, int slotIndex)
        {
            if (item == null || zone == null)
                return;

            item.transform.SetParent(zone.transform, false);
            zone.ApplyPlacedItemTransform(item.transform, slotIndex);
            item.SetPlaced(zone, slotIndex);
            item.gameObject.SetActive(true);
            _handleZone.RelayoutHeldItems();
            RefreshSameIdHighlights();

            SupermarketOrganizePresentationFx.PlayPlace(zone.GetShelfCompleteFxWorldPosition());
            if (zone.IsFull)
            {
                SupermarketOrganizePresentationFx.PlayShelfComplete(zone.GetShelfCompleteFxWorldPosition());
                GrantPlacementRewards(zone, shelfCompleted: true);
            }
            else
            {
                GrantPlacementRewards(zone, shelfCompleted: false);
            }

            ProgressChanged?.Invoke();
            CheckVictory();
        }

        void OnHandleCurrentItemChanged()
        {
            RefreshSameIdHighlights();
            RefreshHeldItemHighlight();
            CurrentItemChanged?.Invoke(_handleZone.CurrentItem);
        }

        void RefreshHeldItemHighlight()
        {
            if (_highlights == null || _handleZone == null || _handleZone.Count <= 0)
            {
                _highlights?.SetHeldItem(null);
                return;
            }

            _highlights.ClearAllHeldVisuals(_handleZone.Items);
            _highlights.SetHeldItem(_handleZone.CurrentItem);
        }

        void GrantPlacementRewards(SupermarketContainerZone zone, bool shelfCompleted)
        {
            if (_config == null)
                return;

            var perItem = Mathf.Max(0, _config.coinsPerPlacedItem);
            if (perItem > 0)
                SupermarketOrganizeEconomyRewards.GrantCoins(perItem);

            if (shelfCompleted)
            {
                var shelfBonus = Mathf.Max(0, _config.coinsPerCompletedShelf);
                if (shelfBonus > 0)
                    SupermarketOrganizeEconomyRewards.GrantCoins(shelfBonus);
            }
        }

        void RefreshSameIdHighlights()
        {
            if (_highlights == null)
                return;

            if (!IsScannerHighlightActive)
            {
                _highlights.SetSameIdHighlights(null);
                return;
            }

            var current = CurrentItem;
            if (current == null)
            {
                _highlights.SetSameIdHighlights(null);
                return;
            }

            _highlights.SetSameIdHighlights(GetGroundItemsById(current.ItemId));
        }

        void CheckVictory()
        {
            if (_remainingGroundCount > 0)
                return;

            if (_handleZone.Count > 0)
                return;

            if (TotalShelfCount > 0 && CompletedShelfCount < TotalShelfCount)
                return;

            if (TotalItemSlotCount > 0 && PlacedItemCount < TotalItemSlotCount)
                return;

            _gameRunning = false;
            SupermarketOrganizePresentationFx.PlayVictory();
            RecordVictory();
            GameWon?.Invoke();
        }

        static SupermarketItemMotion EnsureMotion(SupermarketItem item)
        {
            var motion = item.GetComponent<SupermarketItemMotion>();
            if (motion == null)
                motion = item.gameObject.AddComponent<SupermarketItemMotion>();
            return motion;
        }

        float GetMoveMaxSeconds() =>
            _config != null ? _config.pickUpMoveMaxSeconds : 0.5f;

        float GetMoveArcHeight() =>
            _config != null ? _config.pickUpArcHeight : 0.25f;

        float ResolveMoveSpeed(float distance)
        {
            var maxSeconds = GetMoveMaxSeconds();
            return Mathf.Max(
                _config != null ? _config.itemMoveSpeed : 6f,
                distance / Mathf.Max(0.01f, maxSeconds));
        }

        void BeginArcMoveLocal(SupermarketItem item, Vector3 targetLocal, Action onComplete)
        {
            var maxSeconds = GetMoveMaxSeconds();
            var arcHeight = GetMoveArcHeight();
            var distance = Vector3.Distance(item.transform.localPosition, targetLocal);
            var speed = ResolveMoveSpeed(distance);
            var motion = EnsureMotion(item);
            motion.BeginMoveLocal(
                () => targetLocal,
                speed,
                onComplete,
                maxSeconds,
                arcHeight);
        }

        void BeginArcMoveWorld(SupermarketItem item, Vector3 targetWorld, Action onComplete)
        {
            var maxSeconds = GetMoveMaxSeconds();
            var arcHeight = GetMoveArcHeight();
            var distance = Vector3.Distance(item.transform.position, targetWorld);
            var speed = ResolveMoveSpeed(distance);
            var motion = EnsureMotion(item);
            motion.BeginMoveWorld(
                () => targetWorld,
                speed,
                onComplete,
                maxSeconds,
                arcHeight);
        }

#if UNITY_EDITOR
        /// <summary>编辑器调试：将所有地面/手持物品瞬间放到匹配货架。</summary>
        public int DebugPlaceAllItemsOnShelves()
        {
            if (!Application.isEditor)
                return 0;

            SyncGroundItemsForDebug();
            var placed = 0;
            var pass = 0;
            const int maxPasses = 8;

            while (pass < maxPasses)
            {
                pass++;
                var passPlaced = 0;

                var groundSnapshot = new List<SupermarketItem>(_groundItems);
                for (var i = 0; i < groundSnapshot.Count; i++)
                {
                    var item = groundSnapshot[i];
                    if (item == null || !IsDebugPlaceableItem(item))
                        continue;

                    var zone = FindBestPlaceZoneForItem(item.ItemId, item.transform.position);
                    if (zone != null && TryInstantPlaceOnZone(item, zone))
                    {
                        passPlaced++;
                        placed++;
                    }
                }

                var heldSnapshot = _handleZone != null
                    ? new List<SupermarketItem>(_handleZone.Items)
                    : null;
                if (heldSnapshot != null)
                {
                    for (var i = 0; i < heldSnapshot.Count; i++)
                    {
                        var item = heldSnapshot[i];
                        if (item == null || item.State != SupermarketItemState.InHandle)
                            continue;

                        var zone = FindBestPlaceZoneForItem(item.ItemId, Vector3.zero);
                        if (zone != null && TryInstantPlaceOnZone(item, zone))
                        {
                            passPlaced++;
                            placed++;
                        }
                    }
                }

                if (passPlaced == 0)
                    break;
            }

            LogDebugPlacementSummary(placed);
            return placed;
        }

        static bool IsDebugPlaceableItem(SupermarketItem item) =>
            item != null
            && (item.State == SupermarketItemState.OnGround || item.State == SupermarketItemState.Moving);

        void SyncGroundItemsForDebug()
        {
#if UNITY_2023_1_OR_NEWER
            var allItems = UnityEngine.Object.FindObjectsByType<SupermarketItem>(FindObjectsSortMode.None);
#else
            var allItems = UnityEngine.Object.FindObjectsOfType<SupermarketItem>();
#endif
            for (var i = 0; i < allItems.Length; i++)
            {
                var item = allItems[i];
                if (item == null || item.gameObject == null)
                    continue;

                switch (item.State)
                {
                    case SupermarketItemState.OnShelf:
                    case SupermarketItemState.InHandle:
                    case SupermarketItemState.InRobotHandle:
                        continue;
                    case SupermarketItemState.Moving:
                        item.GetComponent<SupermarketItemMotion>()?.Cancel();
                        item.SetState(SupermarketItemState.OnGround);
                        break;
                }

                RegisterItem(item);
            }
        }

        void LogDebugPlacementSummary(int placed)
        {
            var remaining = 0;
            var unmatched = new List<string>();
            for (var i = 0; i < _groundItems.Count; i++)
            {
                var item = _groundItems[i];
                if (item == null || !IsDebugPlaceableItem(item))
                    continue;

                remaining++;
                if (FindBestPlaceZoneForItem(item.ItemId, item.transform.position) == null)
                {
                    var label = string.IsNullOrWhiteSpace(item.ItemId) ? item.name : item.ItemId;
                    if (!unmatched.Contains(label))
                        unmatched.Add(label);
                }
            }

            if (remaining == 0)
            {
                Debug.Log($"[SupermarketDebug] Q：已放置 {placed} 个物品，地面已清空。");
                return;
            }

            var unmatchedPreview = unmatched.Count <= 10
                ? string.Join(", ", unmatched)
                : string.Join(", ", unmatched.GetRange(0, 10)) + ", ...";
            var unmatchedText = unmatched.Count > 0
                ? $"\n未匹配货架的 ItemId（前 10 个）：{unmatchedPreview}"
                : "\n仍有物品未放入货架，可能是目标货架已满。";
            Debug.LogWarning(
                $"[SupermarketDebug] Q：已放置 {placed} 个物品，仍有 {remaining} 个未收集。{unmatchedText}");
        }

        /// <summary>编辑器调试：将匹配当前货架的物品全部收集到该货架。</summary>
        public int DebugFillShelf(SupermarketContainerZone zone)
        {
            if (!Application.isEditor || zone == null)
                return 0;

            var placed = 0;
            var groundSnapshot = new List<SupermarketItem>(_groundItems);
            for (var i = 0; i < groundSnapshot.Count && !zone.IsFull; i++)
            {
                var item = groundSnapshot[i];
                if (item == null || item.State != SupermarketItemState.OnGround)
                    continue;

                if (!zone.MatchesItem(item.ItemId))
                    continue;

                if (TryInstantPlaceOnZone(item, zone))
                    placed++;
            }

            if (_handleZone != null && !zone.IsFull)
            {
                var heldSnapshot = new List<SupermarketItem>(_handleZone.Items);
                for (var i = 0; i < heldSnapshot.Count && !zone.IsFull; i++)
                {
                    var item = heldSnapshot[i];
                    if (item == null || item.State != SupermarketItemState.InHandle)
                        continue;

                    if (!zone.MatchesItem(item.ItemId))
                        continue;

                    if (TryInstantPlaceOnZone(item, zone))
                        placed++;
                }
            }

            return placed;
        }

        bool TryInstantPlaceOnZone(SupermarketItem item, SupermarketContainerZone zone)
        {
            if (item == null || zone == null || zone.IsFull || !zone.MatchesItem(item.ItemId))
                return false;

            if (!zone.TryAcceptItem(item, out var slotIndex))
                return false;

            DetachItemFromSource(item);
            item.GetComponent<SupermarketItemMotion>()?.Cancel();
            item.PrepareForHandle();
            item.transform.SetParent(null, true);
            CompletePlace(item, zone, slotIndex);
            return true;
        }

        void DetachItemFromSource(SupermarketItem item)
        {
            if (item == null)
                return;

            if (item.State == SupermarketItemState.OnGround)
            {
                _groundItems.Remove(item);
                _remainingGroundCount = Mathf.Max(0, _remainingGroundCount - 1);
                return;
            }

            if (item.State == SupermarketItemState.InHandle && _handleZone != null)
            {
                _handleZone.TryRemove(item);
                HandleCountChanged?.Invoke(_handleZone.Count, _handleZone.MaxCapacity);
            }
        }
#endif
    }
}
