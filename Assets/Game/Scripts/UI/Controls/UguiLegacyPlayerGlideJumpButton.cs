using Game.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.Controls
{
    /// <summary>
    /// 与 <see cref="Game.Hud.GameHudFguiView"/> 中跳跃键一致：<see cref="LegacyPlayerInput.QueueGlidePressFromUi"/>。
    /// 需挂载在带 <see cref="UnityEngine.UI.Graphic"/> 且开启 Raycast Target 的对象上。
    /// 使用 <see cref="IPointerDownHandler"/> 以降低点击延迟。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UguiLegacyPlayerGlideJumpButton : MonoBehaviour,
        IPointerDownHandler,
        IPointerClickHandler
    {
        [SerializeField] private LegacyPlayerInput target;

        [Tooltip("若勾选，也会在 Click 时再排队一次（部分平台 pointer down 不可靠时可开）")]
        [SerializeField] private bool alsoQueueOnClick;

        private void Awake()
        {
            if (target == null)
                target = FindObjectOfType<LegacyPlayerInput>();
        }

        public void Bind(LegacyPlayerInput input) => target = input;

        public void OnPointerDown(PointerEventData eventData) => QueueIfPossible();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (alsoQueueOnClick)
                QueueIfPossible();
        }

        private void QueueIfPossible()
        {
            if (target == null) return;
            target.QueueGlidePressFromUi();
        }
    }
}
