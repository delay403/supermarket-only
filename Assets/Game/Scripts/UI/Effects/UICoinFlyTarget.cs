using UnityEngine;

namespace Game.UI.Effects
{
    /// <summary>
    /// 标记 HUD 上金币飞入的目标位置；场景中优先级最高者作为默认目标。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UICoinFlyTarget : MonoBehaviour
    {
        [SerializeField] private int priority;
        [SerializeField] private RectTransform targetRect;

        public static UICoinFlyTarget Primary { get; private set; }

        public RectTransform TargetRect
        {
            get
            {
                if (targetRect != null)
                    return targetRect;
                return transform as RectTransform;
            }
        }

        void Awake()
        {
            if (targetRect == null)
                targetRect = transform as RectTransform;
        }

        void OnEnable() => RegisterIfHigherPriority();

        void OnDisable()
        {
            if (Primary == this)
                Primary = null;
        }

        void RegisterIfHigherPriority()
        {
            if (Primary == null || priority >= Primary.priority)
                Primary = this;
        }

        public static void Register(RectTransform rect, int registerPriority = 0)
        {
            if (rect == null)
                return;

            var marker = rect.GetComponent<UICoinFlyTarget>();
            if (marker == null)
                marker = rect.gameObject.AddComponent<UICoinFlyTarget>();

            marker.priority = registerPriority;
            marker.targetRect = rect;
            marker.RegisterIfHigherPriority();
        }
    }
}
