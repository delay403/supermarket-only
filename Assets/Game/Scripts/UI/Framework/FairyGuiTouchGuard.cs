using FairyGUI;

namespace Game.UI.Framework
{
    public static class FairyGuiTouchGuard
    {
        static int s_depth;
        static bool s_savedStageTouchable = true;
        static bool s_savedStageTouchChildren = true;

        public static bool IsBlocking => s_depth > 0;

        public static void PushBlock()
        {
            if (s_depth++ != 0)
                return;
            var stage = Stage.inst;
            s_savedStageTouchable = stage.touchable;
            s_savedStageTouchChildren = stage.touchChildren;
            stage.touchable = false;
            stage.touchChildren = false;
        }

        public static void PopBlock()
        {
            if (s_depth <= 0)
                return;
            if (--s_depth != 0 || Stage.inst == null)
                return;
            Stage.inst.touchable = s_savedStageTouchable;
            Stage.inst.touchChildren = s_savedStageTouchChildren;
        }

        public static void ResetDepth()
        {
            s_depth = 0;
            if (Stage.inst == null)
                return;
            Stage.inst.touchable = true;
            Stage.inst.touchChildren = true;
        }
    }
}
