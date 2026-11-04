using System;
using System.Collections;
using UnityEngine;

namespace Game.Maps
{
    /// <summary>
    /// 过场遮罩 API；实际 UI 由场景中的 <see cref="TransitionOverlayBinding"/> 提供。
    /// </summary>
    public static class SceneTransitionOverlay
    {
        public static bool IsScreenCoveredForActivation => TransitionOverlayBinding.IsScreenCoveredForActivation;

        /// <summary>离开场景：Mask scale min → max。</summary>
        public static IEnumerator CoverScreenRoutine(SceneNavigationSettings cfg, Action<float> onProgress = null)
        {
            if (!TransitionOverlayBinding.HasInstance)
            {
                Debug.LogError(
                    $"{nameof(SceneTransitionOverlay)}: 未找到 {nameof(TransitionOverlayBinding)}。请在过场场景挂好绑定，或先通过导航流程加法加载过场场景。");
                yield break;
            }

            yield return TransitionOverlayBinding.Instance.CoCover(cfg, onProgress);
        }

        /// <summary>进入场景：Mask scale max → min，并隐藏 CanvasGroup。</summary>
        public static IEnumerator RevealScreenRoutine(SceneNavigationSettings cfg)
        {
            if (!TransitionOverlayBinding.HasInstance)
            {
                Debug.LogError($"{nameof(SceneTransitionOverlay)}: 未找到 {nameof(TransitionOverlayBinding)}。");
                yield break;
            }

            yield return TransitionOverlayBinding.Instance.CoReveal(cfg);
        }
    }
}
