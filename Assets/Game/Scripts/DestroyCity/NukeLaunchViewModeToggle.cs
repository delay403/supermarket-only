using UnityEngine;
using UnityEngine.UI;

namespace Game.DestroyCity
{
    /// <summary>HUD 按钮：发射期间点击切换玩家/导弹视角与对应输入。</summary>
    [DisallowMultipleComponent]
    public sealed class NukeLaunchViewModeToggle : MonoBehaviour
    {
        [SerializeField] private Button toggleButton;
        [SerializeField] private Text labelText;
        [SerializeField] private string playerModeLabel = "导弹视角";
        [SerializeField] private string missileModeLabel = "玩家视角";

        void Awake()
        {
            if (toggleButton == null)
                toggleButton = GetComponent<Button>();
        }

        void OnEnable()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveListener(OnTogglePressed);
                toggleButton.onClick.AddListener(OnTogglePressed);
                toggleButton.transition = Selectable.Transition.None;
                toggleButton.interactable = true;
            }

            RefreshLabel();
        }

        void OnDisable()
        {
            if (toggleButton != null)
                toggleButton.onClick.RemoveListener(OnTogglePressed);
        }

        void OnTogglePressed()
        {
            var controller = NukeLaunchViewModeController.Instance;
            if (controller == null || !controller.IsSessionActive)
                return;

            controller.ToggleMode();
            RefreshLabel();
        }

        public void RefreshLabel()
        {
            if (toggleButton != null && NukeLaunchViewModeController.Instance != null
                && NukeLaunchViewModeController.Instance.IsSessionActive)
                toggleButton.interactable = true;

            if (labelText == null)
                return;

            var controller = NukeLaunchViewModeController.Instance;
            if (controller == null || !controller.IsSessionActive)
            {
                labelText.text = playerModeLabel;
                return;
            }

            labelText.text = controller.Mode == NukeLaunchViewMode.Missile
                ? missileModeLabel
                : playerModeLabel;
        }
    }
}
