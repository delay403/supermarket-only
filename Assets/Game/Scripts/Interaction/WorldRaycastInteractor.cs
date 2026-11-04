using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Interaction
{
    /// <summary>
    /// 世界射线交互器：支持 WebGL 鼠标与移动端触摸；自动跳过 UI 上的点击。
    /// 挂在玩家或相机上，DestroyCity / 其它 3D 玩法通用。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class WorldRaycastInteractor : MonoBehaviour
    {
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private Transform interactorRoot;
        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private float maxDistance = 120f;
        [Tooltip("触摸/点击移动超过该像素则视为拖拽视角，不触发交互。")]
        [SerializeField] private float tapMoveThresholdPixels = 22f;
        [Tooltip("为 true 时仅在 TouchPhase.Ended / MouseUp 且位移小于阈值时触发（更不易误触）。")]
        [SerializeField] private bool requireReleaseTap = false;
        [Tooltip("仅当指针下存在可点击 UI（Button 等）时才阻挡世界射线；透明摇杆底图不阻挡。")]
        [SerializeField] private bool blockOnlyConsumptiveUi = true;
        [Header("Debug")]
        [SerializeField] private bool debugLog;
        [Tooltip("仅输出未命中可交互物时的日志，减少刷屏。")]
        [SerializeField] private bool debugLogOnlyMisses;

        static readonly List<RaycastResult> UiRaycastBuffer = new List<RaycastResult>();

        struct PendingPointer
        {
            public int id;
            public Vector2 startPos;
            public bool valid;
        }

        struct PendingWorldUiClick
        {
            public int id;
            public Vector2 startPos;
            public bool valid;
        }

        PendingPointer _pending;
        bool _hasPendingMouse;
        PendingWorldUiClick _pendingWorldUiClick;
        bool _pendingWorldUiClickMouse;

        void Awake()
        {
            if (raycastCamera == null)
                raycastCamera = Camera.main;
        }

        public void Configure(Camera cam, Transform root, LayerMask mask)
        {
            if (cam != null)
                raycastCamera = cam;
            if (root != null)
                interactorRoot = root;
            interactableMask = mask;
        }

        void Update()
        {
            if (!enabled)
                return;

            if (raycastCamera == null)
                raycastCamera = Camera.main;
            if (raycastCamera == null)
            {
                LogDebug("无可用 Camera，射线已跳过。");
                return;
            }

            PollTouch();
            PollMouse();
        }

        void PollTouch()
        {
            if (Input.touchCount <= 0)
                return;

            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        if (IsBlockedByUi(
                                touch.position,
                                touch.fingerId,
                                out var uiBlockTouch,
                                blockOnlyConsumptiveUi,
                                out var worldUiTouch))
                        {
                            LogDebug($"触摸被 UI 阻挡：{uiBlockTouch}");
                            if (worldUiTouch)
                            {
                                _pendingWorldUiClick = new PendingWorldUiClick
                                {
                                    id = touch.fingerId,
                                    startPos = touch.position,
                                    valid = true,
                                };
                            }

                            continue;
                        }

                        _pending = new PendingPointer
                        {
                            id = touch.fingerId,
                            startPos = touch.position,
                            valid = true,
                        };
                        if (!requireReleaseTap)
                            TryInteractAt(touch.position, touch.fingerId, WorldInteractionPointerKind.Touch);
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (_pendingWorldUiClick.valid && _pendingWorldUiClick.id == touch.fingerId)
                        {
                            if ((touch.position - _pendingWorldUiClick.startPos).sqrMagnitude <=
                                tapMoveThresholdPixels * tapMoveThresholdPixels)
                            {
                                WorldRaycastUiFilter.TryForwardWorldSpacePointerClick(touch.position);
                            }

                            _pendingWorldUiClick.valid = false;
                        }

                        if (!_pending.valid || _pending.id != touch.fingerId)
                            continue;
                        if (requireReleaseTap &&
                            (touch.position - _pending.startPos).sqrMagnitude <=
                            tapMoveThresholdPixels * tapMoveThresholdPixels)
                        {
                            TryInteractAt(touch.position, touch.fingerId, WorldInteractionPointerKind.Touch);
                        }

                        _pending.valid = false;
                        break;
                }
            }
        }

        void PollMouse()
        {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            if (Input.touchCount > 0)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                if (IsBlockedByUi(
                        Input.mousePosition,
                        -1,
                        out var uiBlockMouse,
                        blockOnlyConsumptiveUi,
                        out var worldUiMouse))
                {
                    LogDebug($"鼠标被 UI 阻挡：{uiBlockMouse}");
                    if (worldUiMouse)
                    {
                        _pendingWorldUiClickMouse = true;
                        _pendingWorldUiClick = new PendingWorldUiClick
                        {
                            id = -1,
                            startPos = Input.mousePosition,
                            valid = true,
                        };
                    }

                    return;
                }

                _hasPendingMouse = true;
                _pending = new PendingPointer
                {
                    id = -1,
                    startPos = Input.mousePosition,
                    valid = true,
                };
                if (!requireReleaseTap)
                    TryInteractAt(Input.mousePosition, -1, WorldInteractionPointerKind.Mouse);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (_pendingWorldUiClickMouse && _pendingWorldUiClick.valid)
                {
                    if (((Vector2)Input.mousePosition - _pendingWorldUiClick.startPos).sqrMagnitude <=
                        tapMoveThresholdPixels * tapMoveThresholdPixels)
                    {
                        WorldRaycastUiFilter.TryForwardWorldSpacePointerClick(Input.mousePosition);
                    }

                    _pendingWorldUiClickMouse = false;
                    _pendingWorldUiClick.valid = false;
                }

                if (_hasPendingMouse && _pending.valid)
                {
                    if (requireReleaseTap &&
                        ((Vector2)Input.mousePosition - _pending.startPos).sqrMagnitude <=
                        tapMoveThresholdPixels * tapMoveThresholdPixels)
                    {
                        TryInteractAt(Input.mousePosition, -1, WorldInteractionPointerKind.Mouse);
                    }

                    _hasPendingMouse = false;
                    _pending.valid = false;
                }
            }
#endif
        }

        void TryInteractAt(Vector2 screenPos, int pointerId, WorldInteractionPointerKind kind)
        {
            var ray = raycastCamera.ScreenPointToRay(screenPos);
            var hits = Physics.RaycastAll(ray, maxDistance, interactableMask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                LogMiss(
                    $"{kind} @ {screenPos} | cam={raycastCamera.name} mask={LayerMaskToString(interactableMask)} | 无 Physics 命中");
                return;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            IWorldInteractable best = null;
            RaycastHit bestHit = default;
            var bestScore = float.MinValue;
            var hitSummary = debugLog ? new System.Text.StringBuilder() : null;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                var interactable = WorldInteractableResolver.Resolve(hit.collider);
                if (hitSummary != null)
                {
                    hitSummary.Append('\n');
                    hitSummary.Append("  [").Append(i).Append("] ")
                        .Append(hit.collider.name)
                        .Append(" (").Append(LayerMask.LayerToName(hit.collider.gameObject.layer))
                        .Append(") d=").Append(hit.distance.ToString("F2"))
                        .Append(interactable != null ? " ✓IWorldInteractable" : " ✗无交互组件");
                }

                if (interactable == null)
                    continue;

                if (WorldInteractionPromptController.ShouldSuppressWorldInteractableRaycast
                    && interactable is WorldInteractableBase)
                    continue;

                var score = interactable.InteractionPriority * 1000f - hit.distance;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = interactable;
                bestHit = hit;
            }

            if (best == null)
            {
                LogMiss(
                    $"{kind} @ {screenPos} | {hits.Length} 次命中但无 IWorldInteractable：{hitSummary}");
                return;
            }

            var root = interactorRoot != null ? interactorRoot : transform;
            var ctx = new WorldInteractionContext(
                raycastCamera,
                screenPos,
                pointerId,
                kind,
                bestHit,
                root);

            var targetName = (best as Component)?.name ?? best.GetType().Name;
            if (!best.CanInteract(ctx))
            {
                LogMiss($"{kind} @ {screenPos} | 目标={targetName} CanInteract=false");
                return;
            }

            LogDebug($"{kind} @ {screenPos} | 交互 → {targetName} (prio={best.InteractionPriority}, hit={bestHit.collider.name})");
            best.Interact(ctx);
        }

        static bool IsBlockedByUi(
            Vector2 screenPos,
            int pointerId,
            out string blockerName,
            bool consumptiveOnly,
            out bool isWorldSpaceCanvas)
        {
            blockerName = null;
            isWorldSpaceCanvas = false;
            if (EventSystem.current == null)
                return false;

            if (consumptiveOnly)
                return WorldRaycastUiFilter.TryGetConsumptiveBlocker(
                    screenPos, out blockerName, out isWorldSpaceCanvas);

            var eventData = new PointerEventData(EventSystem.current) { position = screenPos };
            UiRaycastBuffer.Clear();
            EventSystem.current.RaycastAll(eventData, UiRaycastBuffer);

            for (var i = 0; i < UiRaycastBuffer.Count; i++)
            {
                var result = UiRaycastBuffer[i];
                if (result.gameObject == null)
                    continue;

                var graphic = result.gameObject.GetComponent<Graphic>();
                if (graphic != null && !graphic.raycastTarget)
                    continue;

                var group = result.gameObject.GetComponentInParent<CanvasGroup>();
                if (group != null && !group.blocksRaycasts)
                    continue;

                blockerName = result.gameObject.name;
                var canvas = result.gameObject.GetComponentInParent<Canvas>();
                isWorldSpaceCanvas = canvas != null && canvas.renderMode == RenderMode.WorldSpace;
                return true;
            }

            return false;
        }

        static bool IsBlockedByUi(Vector2 screenPos, int pointerId, out string blockerName, bool consumptiveOnly) =>
            IsBlockedByUi(screenPos, pointerId, out blockerName, consumptiveOnly, out _);

        void LogDebug(string message)
        {
            if (!debugLog || debugLogOnlyMisses)
                return;
            Debug.Log($"[WorldRaycast] {message}", this);
        }

        void LogMiss(string message)
        {
            if (!debugLog)
                return;
            Debug.LogWarning($"[WorldRaycast] {message}", this);
        }

        static string LayerMaskToString(LayerMask mask)
        {
            if (mask.value == ~0)
                return "Everything";

            var names = new List<string>();
            for (var i = 0; i < 32; i++)
            {
                if ((mask.value & (1 << i)) == 0)
                    continue;
                var name = LayerMask.LayerToName(i);
                names.Add(string.IsNullOrEmpty(name) ? $"Layer{i}" : name);
            }

            return names.Count > 0 ? string.Join(",", names) : mask.value.ToString();
        }

        public static WorldRaycastInteractor EnsureOnPlayer(Transform player, Camera cam = null)
        {
            if (player == null)
                return null;

            cam ??= Camera.main;
            var interactor = player.GetComponent<WorldRaycastInteractor>();
            if (interactor == null)
                interactor = player.gameObject.AddComponent<WorldRaycastInteractor>();

            return interactor;
        }

#if UNITY_EDITOR
        void Reset()
        {
            debugLog = true;
            debugLogOnlyMisses = true;
        }
#endif
    }
}
