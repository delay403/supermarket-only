using System;
using System.Collections.Generic;
using Game.Audio;
using Game.SupermarketOrganize.Config;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.SupermarketOrganize
{
    /// <summary>屏幕中心准星射线：瞄准高亮、拾取/放置、货架匹配提示。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketCrosshairInteractor : MonoBehaviour
    {
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private SupermarketOrganizeService service;
        [SerializeField] private SupermarketItemHighlightController highlights;
        [SerializeField] private SupermarketShelfFeedbackUi shelfFeedback;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private LayerMask itemMask = ~0;
        [SerializeField] private LayerMask shelfMask = ~0;
        [SerializeField] private float tapMoveThresholdPixels = 22f;
        [SerializeField] private bool requireReleaseTap = true;

        SupermarketOrganizeConfig runtimeConfig;
        SupermarketContainerZone[] _registeredZones = System.Array.Empty<SupermarketContainerZone>();
        int _cachedRegisteredZoneCount = -1;
        SupermarketOrganizeSkillController _skillController;

        static readonly List<RaycastResult> UiRaycastBuffer = new List<RaycastResult>();

        SupermarketItem _aimItem;
        SupermarketContainerZone _aimZone;
        Collider _wasteBinTrigger;
        bool _aimWasteBin;
        SupermarketAimState _aimState;
        SupermarketItem _lastSelectSfxItem;
        Vector2 _pointerStart;
        bool _pointerValid;
        int _pointerId = -1;

        public event Action<SupermarketAimState> AimStateChanged;
        public SupermarketAimState CurrentAimState => _aimState;
        public SupermarketItem AimItem => _aimItem;
        public SupermarketContainerZone AimZone => _aimZone;

        public void Configure(
            Camera cam,
            SupermarketOrganizeService organizeService,
            SupermarketItemHighlightController highlightController,
            SupermarketShelfFeedbackUi feedbackUi,
            SupermarketOrganizeConfig config)
        {
            raycastCamera = cam;
            service = organizeService;
            highlights = highlightController;
            shelfFeedback = feedbackUi;

            if (config == null)
                return;

            maxDistance = config.raycastMaxDistance;
            itemMask = config.itemRaycastMask;
            shelfMask = config.shelfRaycastMask;
            tapMoveThresholdPixels = config.tapMoveThresholdPixels;
            runtimeConfig = config;
        }

        public void SetSkillController(SupermarketOrganizeSkillController skillController) =>
            _skillController = skillController;

        public void SetWasteBinTrigger(Collider trigger) => _wasteBinTrigger = trigger;

        public void ApplyPickupRange(float distance) =>
            maxDistance = Mathf.Max(0.1f, distance);

        public void ForceRefreshShelfPresentation() =>
            RefreshShelfPresentation(BuildAimState());

        public void InvalidateZoneCache()
        {
            _cachedRegisteredZoneCount = -1;
            _registeredZones = System.Array.Empty<SupermarketContainerZone>();
        }

        public bool TryPickUp()
        {
            if (service == null || !service.IsGameRunning || !_aimState.canPickUp || _aimItem == null)
                return false;

            return service.TryPickUpFromGround(_aimItem);
        }

        public bool TryPlace()
        {
            if (service == null || !service.IsGameRunning || !_aimState.canPlace)
                return false;

            if (_aimWasteBin)
            {
                if (_wasteBinTrigger != null)
                    service.TryDiscardCurrentItem(_wasteBinTrigger.bounds.center);
                return true;
            }

            if (_aimZone == null)
                return false;

            if (service.CurrentItem != null && service.CurrentItem.IsDefective)
            {
                service.TryPlaceOnShelf(_aimZone);
                return true;
            }

            if (!service.EvaluateShelf(_aimZone, out var isCorrect, out _) || !isCorrect)
            {
                SupermarketOrganizePresentationFx.PlayPlaceWrong(_aimZone.transform.position);
                return true;
            }

            return service.TryPlaceOnShelf(_aimZone);
        }

        void Update()
        {
            if (raycastCamera == null)
                raycastCamera = Camera.main;
            if (raycastCamera == null || service == null)
                return;

            UpdateAim();
            service?.TickTimedEffects();
            PollPointerInput();
        }

        void UpdateAim()
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var ray = raycastCamera.ScreenPointToRay(center);

            SupermarketItem hitItem = null;
            SupermarketContainerZone hitZone = null;

            if (Physics.Raycast(ray, out var hit, maxDistance, itemMask, QueryTriggerInteraction.Collide))
            {
                var candidate = hit.collider.GetComponentInParent<SupermarketItem>();
                if (candidate != null && candidate.State == SupermarketItemState.OnGround)
                    hitItem = candidate;
            }

            _aimWasteBin = CanAimAtWasteBin(ray);
            if (!_aimWasteBin && Physics.Raycast(ray, out hit, maxDistance, shelfMask, QueryTriggerInteraction.Collide))
            {
                var candidate = hit.collider.GetComponentInParent<SupermarketContainerZone>();
                if (IsZoneInActiveGameplay(candidate))
                    hitZone = candidate;
            }

            _aimItem = hitItem;
            _aimZone = hitZone;

            if (_aimItem != _lastSelectSfxItem)
            {
                if (_aimItem != null)
                    SupermarketOrganizePresentationFx.PlaySelect(_aimItem.transform.position);
                _lastSelectSfxItem = _aimItem;
            }

            highlights?.SetAimTarget(_aimItem);

            var nextState = BuildAimState();
            if (!AimStateEquals(nextState, _aimState))
            {
                _aimState = nextState;
                AimStateChanged?.Invoke(_aimState);
            }

            RefreshShelfPresentation(nextState);

            if (shelfFeedback != null && !nextState.showShelfFeedback)
                shelfFeedback.Hide();
            else if (shelfFeedback != null && nextState.showShelfFeedback)
                shelfFeedback.ShowShelfMatch(nextState.shelfIsCorrect, nextState.showShelfExtraTip);
        }

        bool CanAimAtWasteBin(Ray ray)
        {
            if (_wasteBinTrigger == null || !_wasteBinTrigger.enabled ||
                !_wasteBinTrigger.gameObject.activeInHierarchy ||
                !_wasteBinTrigger.Raycast(ray, out var binHit, maxDistance))
                return false;

            return !Physics.Raycast(ray, out var obstruction, binHit.distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                obstruction.collider.gameObject == _wasteBinTrigger.gameObject;
        }

        void RefreshShelfPresentation(SupermarketAimState state)
        {
            var zones = ResolveContainerZones();
            var held = service != null ? service.CurrentItem : null;
            var xRayActive = _skillController != null && _skillController.IsXRayActive;

            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone == null)
                    continue;

                var isAimZone = zone == _aimZone;
                var idMatches = held != null && !held.IsDefective && zone.MatchesItem(held.ItemId);
                zone.SetTipVisible((isAimZone && idMatches) || (xRayActive && idMatches));

                if (isAimZone && state.canPlace && state.shelfIsCorrect)
                    zone.ShowPlacePreview(held, runtimeConfig);
                else
                    zone.HidePlacePreview();
            }
        }

        SupermarketContainerZone[] ResolveContainerZones()
        {
            if (service != null)
            {
                var registered = service.RegisteredZones;
                if (registered.Count > 0)
                {
                    if (_cachedRegisteredZoneCount != registered.Count || _registeredZones.Length == 0)
                    {
                        _registeredZones = CopyZones(registered);
                        _cachedRegisteredZoneCount = registered.Count;
                    }

                    return _registeredZones;
                }
            }

            var manager = SupermarketOrganizeDifficultyManager.ResolveRuntime();
            var activeShelves = manager?.GetActiveShelves();
            if (activeShelves != null && activeShelves.Count > 0)
            {
                if (_cachedRegisteredZoneCount != activeShelves.Count || _registeredZones.Length == 0)
                {
                    _registeredZones = CopyZones(activeShelves);
                    _cachedRegisteredZoneCount = activeShelves.Count;
                }

                return _registeredZones;
            }

            _cachedRegisteredZoneCount = -1;
            _registeredZones = System.Array.Empty<SupermarketContainerZone>();
            return _registeredZones;
        }

        static SupermarketContainerZone[] CopyZones(IReadOnlyList<SupermarketContainerZone> zones)
        {
            if (zones == null || zones.Count == 0)
                return System.Array.Empty<SupermarketContainerZone>();

            var result = new SupermarketContainerZone[zones.Count];
            var count = 0;
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null || !zone.gameObject.activeInHierarchy)
                    continue;

                result[count++] = zone;
            }

            if (count == result.Length)
                return result;

            var trimmed = new SupermarketContainerZone[count];
            for (var i = 0; i < count; i++)
                trimmed[i] = result[i];

            return trimmed;
        }

        bool IsZoneInActiveGameplay(SupermarketContainerZone zone)
        {
            if (zone == null || !zone.gameObject.activeInHierarchy)
                return false;

            if (service != null && service.ContainsZone(zone))
                return true;

            var manager = SupermarketOrganizeDifficultyManager.ResolveRuntime();
            var activeShelves = manager?.GetActiveShelves();
            if (activeShelves == null)
                return false;

            for (var i = 0; i < activeShelves.Count; i++)
            {
                if (activeShelves[i] == zone)
                    return true;
            }

            return false;
        }

        SupermarketAimState BuildAimState()
        {
            var state = new SupermarketAimState();
            if (service == null || !service.IsGameRunning)
            {
                state.hintText = string.Empty;
                return state;
            }

            if (_aimItem != null && CanPickUpItem(_aimItem))
            {
                var name = SupermarketItemDisplayNames.Resolve(_aimItem);
                state.hasAimTarget = true;
                state.canPickUp = true;
                state.hintText = $"瞄准：{name}（可拾取）";
                state.pickUpLabel = "拾取";
            }
            else if (_aimItem != null && _aimItem.State == SupermarketItemState.OnGround &&
                     !IsItemPhysicsSettled(_aimItem))
            {
                state.hasAimTarget = true;
                state.hintText = "物品尚未落地，请稍候…";
            }

            var current = service.CurrentItem;
            var holdingItem = current != null && current.State == SupermarketItemState.InHandle;
            if (_aimWasteBin && holdingItem)
            {
                state.hasAimTarget = true;
                state.canPlace = true;
                state.placeLabel = "投入";
                state.hintText = "瞄准：垃圾桶";
            }
            else if (_aimZone != null && holdingItem &&
                service.EvaluateShelf(_aimZone, out var isCorrect, out var showTip))
            {
                state.hasAimTarget = true;
                state.showShelfFeedback = true;
                state.shelfIsCorrect = isCorrect;
                state.showShelfExtraTip = showTip;
                state.canPlace = true;

                var currentName = SupermarketItemDisplayNames.Resolve(service.CurrentItem);
                state.placeLabel = "放置";

                if (isCorrect)
                {
                    var hint = FormatCorrectShelfHint(currentName);
                    if (string.IsNullOrWhiteSpace(state.hintText))
                        state.hintText = hint;
                    else
                        state.hintText += $"\n{hint}";

                    state.useHintTextColor = true;
                    state.hintTextColor = ResolveShelfHintColor(true);
                }
                else if (string.IsNullOrWhiteSpace(state.hintText))
                {
                    state.hintText = ResolveWrongShelfHintText();
                    state.useHintTextColor = true;
                    state.hintTextColor = ResolveShelfHintColor(false);
                }
                else
                {
                    state.hintText += $"\n{ResolveWrongShelfHintText()}";
                }
            }

            if (holdingItem)
            {
                if (current.IsDefective && _aimZone != null)
                    state.hintText = "商品不合格，请投入垃圾桶";
                else if (string.IsNullOrEmpty(state.hintText) || _aimWasteBin || current.IsDefective)
                    state.hintText = SupermarketOrganizeService.GetItemQualityHint(current);
            }

            if (!string.IsNullOrEmpty(service.QualityFeedback))
            {
                state.hintText = service.QualityFeedback;
                state.useHintTextColor = false;
            }

            return state;
        }

        string FormatCorrectShelfHint(string itemDisplayName)
        {
            runtimeConfig ??= SupermarketOrganizeConfig.ResolveRuntime();
            var format = runtimeConfig != null && !string.IsNullOrWhiteSpace(runtimeConfig.correctShelfHintFormat)
                ? runtimeConfig.correctShelfHintFormat
                : "正确货架 · 可放置 {0}";

            try
            {
                return string.Format(format, itemDisplayName);
            }
            catch (System.FormatException)
            {
                return $"正确货架 · 可放置 {itemDisplayName}";
            }
        }

        string ResolveWrongShelfHintText()
        {
            runtimeConfig ??= SupermarketOrganizeConfig.ResolveRuntime();
            return runtimeConfig != null && !string.IsNullOrWhiteSpace(runtimeConfig.wrongShelfHintText)
                ? runtimeConfig.wrongShelfHintText
                : "错误的货架";
        }

        Color ResolveShelfHintColor(bool isCorrect)
        {
            runtimeConfig ??= SupermarketOrganizeConfig.ResolveRuntime();
            if (runtimeConfig == null)
                return Color.white;

            return isCorrect ? runtimeConfig.correctShelfTextColor : runtimeConfig.wrongShelfTextColor;
        }

        static bool CanPickUpItem(SupermarketItem item) =>
            item != null
            && item.State == SupermarketItemState.OnGround
            && IsItemPhysicsSettled(item);

        static bool IsItemPhysicsSettled(SupermarketItem item)
        {
            if (item == null)
                return false;

            var settle = item.GetComponent<SupermarketItemPhysicsSettle>();
            return settle == null || settle.IsSettled;
        }

        static bool AimStateEquals(SupermarketAimState a, SupermarketAimState b) =>
            a.hasAimTarget == b.hasAimTarget
            && a.canPickUp == b.canPickUp
            && a.canPlace == b.canPlace
            && a.showShelfFeedback == b.showShelfFeedback
            && a.shelfIsCorrect == b.shelfIsCorrect
            && a.showShelfExtraTip == b.showShelfExtraTip
            && a.useHintTextColor == b.useHintTextColor
            && a.hintTextColor == b.hintTextColor
            && a.hintText == b.hintText
            && a.pickUpLabel == b.pickUpLabel
            && a.placeLabel == b.placeLabel;

        void PollPointerInput()
        {
            if (Input.touchCount > 0)
            {
                for (var i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    switch (touch.phase)
                    {
                        case TouchPhase.Began:
                            if (IsBlockedByUi(touch.position))
                                continue;

                            _pointerId = touch.fingerId;
                            _pointerStart = touch.position;
                            _pointerValid = true;
                            if (!requireReleaseTap)
                                TryInteractFromPointer();
                            break;
                        case TouchPhase.Ended:
                        case TouchPhase.Canceled:
                            if (!_pointerValid || touch.fingerId != _pointerId)
                                continue;

                            if ((touch.position - _pointerStart).sqrMagnitude <= tapMoveThresholdPixels * tapMoveThresholdPixels)
                                TryInteractFromPointer();

                            _pointerValid = false;
                            break;
                    }
                }

                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (IsBlockedByUi(Input.mousePosition))
                    return;

                _pointerStart = Input.mousePosition;
                _pointerValid = true;
                if (!requireReleaseTap)
                    TryInteractFromPointer();
            }
            else if (Input.GetMouseButtonUp(0) && _pointerValid)
            {
                if ((Input.mousePosition - (Vector3)_pointerStart).sqrMagnitude <= tapMoveThresholdPixels * tapMoveThresholdPixels)
                    TryInteractFromPointer();

                _pointerValid = false;
            }

#if UNITY_EDITOR
            PollEditorDebugInput();
#else
            if (Input.GetKeyDown(KeyCode.E))
                TryInteractFromPointer();
#endif
        }

#if UNITY_EDITOR
        void PollEditorDebugInput()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                TryDebugPlaceAllItems();
                return;
            }

            if (!Input.GetKeyDown(KeyCode.E))
                return;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                TryDebugFillAimedShelf();
            else
                TryInteractFromPointer();
        }

        void TryDebugPlaceAllItems()
        {
            if (service == null || !service.IsGameRunning)
                return;

            service.DebugPlaceAllItemsOnShelves();
            ForceRefreshShelfPresentation();
        }

        void TryDebugFillAimedShelf()
        {
            if (service == null)
                return;

            if (_aimZone == null)
            {
                Debug.Log("[SupermarketDebug] Shift+E：请先对准一个货架。");
                return;
            }

            var placed = service.DebugFillShelf(_aimZone);
            Debug.Log(
                $"[SupermarketDebug] Shift+E：已向货架「{_aimZone.TargetItemId}」填充 {placed} 个物品" +
                $"（{ _aimZone.PlacedCount }/{ _aimZone.Capacity }）。");
            ForceRefreshShelfPresentation();
        }
#endif

        void TryInteractFromPointer()
        {
            if (TryPlace())
                return;

            TryPickUp();
        }

        static bool IsBlockedByUi(Vector2 screenPos)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
                return false;

            if (EventSystem.current == null)
                return false;

            var ped = new PointerEventData(EventSystem.current) { position = screenPos };
            UiRaycastBuffer.Clear();
            EventSystem.current.RaycastAll(ped, UiRaycastBuffer);
            for (var i = 0; i < UiRaycastBuffer.Count; i++)
            {
                var result = UiRaycastBuffer[i];
                if (IsTouchJoystickUi(result.gameObject))
                    continue;

                if (result.gameObject.GetComponentInParent<Selectable>() != null)
                    return true;
            }

            return false;
        }

        static bool IsTouchJoystickUi(GameObject hit)
        {
            var current = hit != null ? hit.transform : null;
            while (current != null)
            {
                var n = current.name;
                if (n == "InputPanel" || n == "JoystickR" || n == "JoystickL")
                    return true;

                current = current.parent;
            }

            return false;
        }
    }
}
