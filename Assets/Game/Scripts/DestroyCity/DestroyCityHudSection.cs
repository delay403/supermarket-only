using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.DestroyCity
{
    /// <summary>摧毁城市 HUD：导弹发射期间显示视角切换按钮。</summary>
    [DisallowMultipleComponent]
    public sealed class DestroyCityHudSection : MonoBehaviour
    {
        [SerializeField] private GameObject viewModeToggleRoot;
        [SerializeField] private Button viewModeToggleButton;
        [SerializeField] private NukeLaunchViewModeToggle viewModeToggle;

        void OnEnable()
        {
            ApplyProductModeVisibility();
            EnsureViewModeToggle();
            SetViewModeToggleVisible(false);
            NukeLaunchViewModeController.SessionActiveChanged += OnSessionActiveChanged;
        }

        void OnDisable()
        {
            NukeLaunchViewModeController.SessionActiveChanged -= OnSessionActiveChanged;
        }

        void OnSessionActiveChanged(bool _) => RefreshViewModeToggleVisibility();

        void ApplyProductModeVisibility() =>
            gameObject.SetActive(GameplayModuleContext.IsModuleActive(GameplayModule.DestroyCity));

        public void RefreshAll() => RefreshViewModeToggleVisibility();

        void EnsureViewModeToggle()
        {
            if (viewModeToggleRoot == null && viewModeToggleButton != null)
                viewModeToggleRoot = viewModeToggleButton.gameObject;

            if (viewModeToggle == null && viewModeToggleButton != null)
                viewModeToggle = viewModeToggleButton.GetComponent<NukeLaunchViewModeToggle>();
            if (viewModeToggle == null)
                viewModeToggle = GetComponentInChildren<NukeLaunchViewModeToggle>(true);

            if (viewModeToggleRoot == null && viewModeToggle != null)
                viewModeToggleRoot = viewModeToggle.gameObject;
        }

        void RefreshViewModeToggleVisibility()
        {
            var active = NukeLaunchViewModeController.Instance != null
                         && NukeLaunchViewModeController.Instance.IsSessionActive;
            SetViewModeToggleVisible(active);
        }

        void SetViewModeToggleVisible(bool visible)
        {
            var root = viewModeToggleRoot;
            if (root == null)
                return;

            if (root.activeSelf != visible)
                root.SetActive(visible);

            if (visible)
                viewModeToggle?.RefreshLabel();
        }
    }
}
