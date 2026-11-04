using Game.Core;

namespace Game.Maps
{
    /// <summary>
    /// 静态槽：下一跳要加载的场景名（由 <see cref="GameSceneLoader"/> 写入，过场 / 协程宿主读取）。
    /// </summary>
    public static class SceneTransitionRequest
    {
        static int s_navigationId;
        static int s_consumedNavigationId = -1;

        public static string PendingGameplayScene { get; set; }

        public static void PrepareNavigation(string targetScene)
        {
            s_navigationId++;
            s_consumedNavigationId = -1;
            PendingGameplayScene = targetScene;
        }

        /// <summary>防止 SceneTransition 被重复加载时二次触发 CoLoadGameplay。</summary>
        public static bool TryBeginGameplayLoad()
        {
            if (s_consumedNavigationId == s_navigationId)
                return false;

            s_consumedNavigationId = s_navigationId;
            return !string.IsNullOrWhiteSpace(PendingGameplayScene);
        }

        public static void ClearPending()
        {
            PendingGameplayScene = null;
        }
    }
}
