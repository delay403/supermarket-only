using System;

namespace Game.Maps
{
    /// <summary>地图、奖牌等与 HUD / UI 同步的全局通知（解锁、切换、Teleport）。</summary>
    public static class MapRuntime
    {
        public static event Action Changed;

        public static void RaiseChanged() => Changed?.Invoke();
    }
}
