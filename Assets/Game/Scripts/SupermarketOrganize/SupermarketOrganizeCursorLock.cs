using Game.Player;
using Game.UI;
using Game.UI.Framework;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>局内将光标锁定并隐藏在屏幕中心；打开设置或回到局外时解开。</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class SupermarketOrganizeCursorLock : MonoBehaviour
    {
        LegacyPlayerInput _input;

        void Update()
        {
            HandleSettingsToggle();
            Apply();
        }

        void OnDisable() => UnlockCursor();

        void OnDestroy() => UnlockCursor();

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                Apply();
        }

        void HandleSettingsToggle()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
                return;

            if (IsSettingsVisible())
            {
                UiRouter.CloseSettings();
                return;
            }

            if (!IsChallengeRunning())
                return;

            UiRouter.OpenSettings();
        }

        void Apply()
        {
            var shouldLock = IsChallengeRunning() && !IsSettingsVisible();
            if (shouldLock)
                LockCursor();
            else
                UnlockCursor();

            var input = ResolveInput();
            if (input != null)
                input.SetLocked(!shouldLock);
        }

        static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        LegacyPlayerInput ResolveInput()
        {
            if (_input != null)
                return _input;

#if UNITY_2023_1_OR_NEWER
            _input = FindFirstObjectByType<LegacyPlayerInput>();
#else
            _input = FindObjectOfType<LegacyPlayerInput>();
#endif
            return _input;
        }

        public static bool IsChallengeRunning()
        {
            var director = SupermarketOrganizeDirector.Instance;
            return director != null
                   && director.Service != null
                   && director.Service.IsGameRunning;
        }

        public static bool IsSettingsVisible()
        {
#if UNITY_2023_1_OR_NEWER
            var panel = Object.FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
#else
            var panel = Object.FindObjectOfType<SettingsPanel>(true);
#endif
            if (panel == null)
                return false;

            if (panel.root != null)
                return panel.root.activeInHierarchy;

            return panel.isActiveAndEnabled;
        }
    }
}