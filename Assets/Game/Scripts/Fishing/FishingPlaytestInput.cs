using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 开发测试用：键盘触发钓鱼 / 收杆 / 取消（正式 UI 接入后可移除或关闭）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingPlaytestInput : MonoBehaviour
    {
        [SerializeField] private FishingHudInputBridge bridge;
        [SerializeField] private bool enableKeyboard = true;
        [SerializeField] private KeyCode fishOrReelKey = KeyCode.F;
        [SerializeField] private KeyCode cancelKey = KeyCode.X;
#if UNITY_EDITOR
        [SerializeField] private bool debugLogFailures = true;
#else
        [SerializeField] private bool debugLogFailures;
#endif

        [Tooltip("关闭 Editor 下 OnGUI 每帧探测，避免与大量场景对象叠加时拖慢 Play 模式。")]
        [SerializeField] private bool showEditorOverlay = false;

        void Awake() => EnsureBridge();

        void EnsureBridge()
        {
            if (bridge != null)
                return;

            bridge = GetComponent<FishingHudInputBridge>();
            if (bridge == null)
                bridge = GetComponentInParent<FishingHudInputBridge>();

            if (bridge != null)
                return;

            var motor = GetComponent<Game.Player.PlayerMotor>()
                        ?? GetComponentInParent<Game.Player.PlayerMotor>();
            if (motor == null)
                return;

            bridge = motor.GetComponent<FishingHudInputBridge>();
            if (bridge == null)
                bridge = motor.gameObject.AddComponent<FishingHudInputBridge>();
        }

        void Update()
        {
            if (!enableKeyboard)
                return;

            if (bridge == null)
                EnsureBridge();

            if (bridge == null)
                return;

            if (Input.GetKeyDown(fishOrReelKey))
            {
                var started = bridge.OnFishingButtonPressedWithResult();
                if (!started && debugLogFailures)
                    Debug.Log("[FishingPlaytestInput] 按 F 未触发钓鱼，见 PlayerFishingController 日志。");
            }

            if (Input.GetKeyDown(cancelKey))
                bridge.OnCancelFishingPressed();
        }

#if UNITY_EDITOR
        void OnGUI()
        {
            if (!showEditorOverlay || !enableKeyboard || bridge == null)
                return;

            const float w = 280f;
            const float h = 88f;
            var rect = new Rect(12f, Screen.height - h - 12f, w, h);
            GUI.Box(rect, "钓鱼测试 (FishingPlaytestInput)");
            GUI.Label(new Rect(rect.x + 8f, rect.y + 22f, w - 16f, 20f),
                bridge.CanStartFishing() ? "在钓鱼区内 — 按 F 抛线" :
                bridge.CanReelOrCancel() ? "钓鱼中 — F 收杆 / X 取消" :
                "请走进 FishingZone 触发器");
            GUI.Label(new Rect(rect.x + 8f, rect.y + 44f, w - 16f, 20f), "F = 抛线/收杆    X = 取消");
        }
#endif
    }
}
