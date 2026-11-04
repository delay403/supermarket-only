using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Interaction
{
    /// <summary>
    /// 标记该 UI 区域不阻挡 <see cref="WorldRaycastInteractor"/>（仅装饰/摇杆底图等）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldRaycastUiPassthrough : MonoBehaviour
    {
    }

    /// <summary>
    /// 标记该 UI 区域必须阻挡世界射线（透明按钮等）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldRaycastUiBlocker : MonoBehaviour
    {
    }

    static class WorldRaycastUiFilter
    {
        const float DecorativeAlphaThreshold = 0.1f;

        public static bool IsConsumptiveUiHit(GameObject go)
        {
            if (go == null)
                return false;

            if (go.GetComponentInParent<WorldRaycastUiPassthrough>() != null)
                return false;

            if (go.GetComponentInParent<WorldRaycastUiBlocker>() != null)
                return true;

            var selectable = go.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
                return true;

            if (go.GetComponentInParent<ScrollRect>() != null)
                return true;

            if (go.GetComponentInParent<InputField>() != null)
                return true;

            var graphic = go.GetComponent<Graphic>();
            if (graphic == null)
                return false;

            if (!graphic.raycastTarget)
                return false;

            var group = go.GetComponentInParent<CanvasGroup>();
            if (group != null && !group.blocksRaycasts)
                return false;

            // 摇杆/全屏透明底图：仅用于接收拖拽，不阻挡世界点击
            if (graphic.color.a < DecorativeAlphaThreshold)
                return false;

            return false;
        }

        public static bool TryGetConsumptiveBlocker(Vector2 screenPos, out string blockerName) =>
            TryGetConsumptiveBlocker(screenPos, out blockerName, out _);

        public static bool TryGetConsumptiveBlocker(
            Vector2 screenPos,
            out string blockerName,
            out bool isWorldSpaceCanvas)
        {
            blockerName = null;
            isWorldSpaceCanvas = false;
            if (EventSystem.current == null)
                return false;

            var eventData = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            for (var i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (!IsConsumptiveUiHit(go))
                    continue;

                blockerName = go.name;
                var canvas = go.GetComponentInParent<Canvas>();
                isWorldSpaceCanvas = canvas != null && canvas.renderMode == RenderMode.WorldSpace;
                return true;
            }

            return false;
        }

        /// <summary>将指针点击转发给世界空间 UGUI（Button 等），在 EventSystem 未自动触发时兜底。</summary>
        public static bool TryForwardWorldSpacePointerClick(Vector2 screenPos)
        {
            if (EventSystem.current == null)
                return false;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPos,
                button = PointerEventData.InputButton.Left,
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            for (var i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null)
                    continue;

                var canvas = go.GetComponentInParent<Canvas>();
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                    continue;

                var button = go.GetComponentInParent<Button>();
                if (button != null && button.isActiveAndEnabled && button.interactable)
                {
                    ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
                    return true;
                }
            }

            return false;
        }
    }
}
