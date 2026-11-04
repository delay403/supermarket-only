using System.Collections.Generic;
using FairyGUI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.Framework
{
    /// <summary>
    /// FairyGUI <see cref="Stage"/> 为 DontDestroyOnLoad；在打包后异步切场景时，旧场景 <see cref="UIPanel"/>
    /// 的容器有时会在新场景 UIPanel 已 <see cref="UIPanel.OnEnable"/> 之后才销毁，导致 Stage 上残留整页 HUD（重复控件与叠字）。
    /// 在每次场景加载完成后，仅保留仍挂在场景物体上的 <see cref="UIPanel.container"/>。
    /// </summary>
    public static class FairyGuiSceneLifecycle
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
                return;

            PruneStaleStagePanels();
            CleanupGRootOverlays();
            ResetFairyGuiTouchGuard();
        }

        static void OnSceneUnloaded(Scene scene)
        {
            if (!Application.isPlaying)
                return;

            // 旧场景 OnDestroy 可能在 sceneLoaded 之后才执行；卸载时再清一次 Stage 残留。
            PruneStaleStagePanels();
            CleanupGRootOverlays();
            ResetFairyGuiTouchGuard();
        }

        /// <summary>供过场协程在揭开遮罩前再清一次（与 sceneLoaded 双保险）。</summary>
        public static void PruneAfterGameplaySceneActivated()
        {
            if (!Application.isPlaying)
                return;

            PruneStaleStagePanels();
            CleanupGRootOverlays();
            ResetFairyGuiTouchGuard();
        }

        static void ResetFairyGuiTouchGuard()
        {
            if (FairyGuiTouchGuard.IsBlocking)
                FairyGuiTouchGuard.ResetDepth();
        }

        static void PruneStaleStagePanels()
        {
            if (Stage.inst == null)
                return;

            var allowed = new HashSet<Container>();
            CollectLivePanelContainers(allowed);

            var stage = Stage.inst;
            for (var i = stage.numChildren - 1; i >= 0; i--)
            {
                var child = stage.GetChildAt(i);
                if (child == null || child.isDisposed)
                    continue;

                if (child == GRoot.inst.displayObject)
                    continue;

                if (child is Container c && !allowed.Contains(c))
                    c.Dispose();
            }
        }

        static void CollectLivePanelContainers(HashSet<Container> allowed)
        {
#if UNITY_2023_1_OR_NEWER
            var panels = Object.FindObjectsByType<UIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var panels = Object.FindObjectsOfType<UIPanel>(true);
#endif
            for (var i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];
                if (panel == null || panel.container == null)
                    continue;

                allowed.Add(panel.container);
            }
        }

        static void CleanupGRootOverlays()
        {
            if (GRoot.inst == null)
                return;

            GRoot.inst.HidePopup();
            GRoot.inst.HideTooltips();

            var root = GRoot.inst;
            for (var i = root.numChildren - 1; i >= 0; i--)
            {
                var child = root.GetChildAt(i);
                if (child != null && !child.isDisposed)
                    child.Dispose();
            }
        }
    }
}
