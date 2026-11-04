using FairyGUI;

namespace Game.UI.Framework
{
    /// <summary>
    /// UGUI 与 FairyGUI 并行时，FGUI 对所有输入独立做命中：<c>UIPanel</c> 的根 <see cref="Stage"/> 子节点
    /// 与 <see cref="GRoot"/> 是兄弟关系，仅关 <c>GRoot.touchable</c> 挡不住挂在 Stage 下的 HUD（如 GameWnd）。
    /// 挡住整屏时须关闭 Stage 对自己的子节点的命中：<see cref="Container.touchChildren"/> =
    /// false（仅设 <see cref="DisplayObject.touchable"/> 无效，因 <see cref="Stage"/> 的 HitTest 会绕过自身 direct 的 touchable 仍遍历子节点）。
    /// </summary>
    public static class FairyGuiTouchGuard
    {
        static int s_depth;
        static bool s_savedStageTouchable = true;
        static bool s_savedStageTouchChildren = true;

        /// <summary>当前是否有 UGUI 层正在要求屏蔽 FGUI 触摸。</summary>
        public static bool IsBlocking => s_depth > 0;

        public static void PushBlock()
        {
            if (s_depth++ == 0)
            {
                var stage = Stage.inst;
                s_savedStageTouchable = stage.touchable;
                s_savedStageTouchChildren = stage.touchChildren;
                stage.touchable = false;
                stage.touchChildren = false;
            }
        }

        public static void PopBlock()
        {
            if (s_depth <= 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning("[FairyGuiTouchGuard] PopBlock called while depth is already 0.");
#endif
                return;
            }

            if (--s_depth == 0)
            {
                if (Stage.inst != null)
                {
                    Stage.inst.touchable = s_savedStageTouchable;
                    Stage.inst.touchChildren = s_savedStageTouchChildren;
                }
            }
        }

        /// <summary>强制恢复到「未屏蔽」状态（例如切场景、异常恢复）。慎用。</summary>
        public static void ResetDepth()
        {
            s_depth = 0;
            if (Stage.inst != null)
            {
                Stage.inst.touchable = true;
                Stage.inst.touchChildren = true;
            }
        }
    }
}
