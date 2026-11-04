using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Maps
{
    /// <summary>
    /// 放在「过场中转场景」任意物体上：启动后异步加载真正的 gameplay 场景。
    /// 需在场景中配置美术/UI（Logo、Tips）；可选绑定 <see cref="SceneNavigationSettings"/>。
    /// </summary>
    public sealed class TransitionSceneController : MonoBehaviour
    {
        [SerializeField] private SceneNavigationSettings settingsOverride;

        void Start()
        {
            // 加法加载时仅提供过场 UI，由 SceneTransitionCoroutineHost 驱动完整流程。
            if (SceneManager.sceneCount > 1)
                return;

            if (!SceneTransitionRequest.TryBeginGameplayLoad())
            {
                Debug.LogWarning(
                    $"[{nameof(TransitionSceneController)}] 忽略重复的过场加载请求（SceneTransition 可能被加载了两次）。");
                return;
            }

            SceneTransitionCoroutineHost.StartLoadPendingGameplay(settingsOverride, assumeAlreadyFullScreenCovered: false);
        }
    }
}
