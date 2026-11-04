using UnityEngine;

namespace Game.Maps
{
    /// <summary>
    /// 主页选关进入玩法前可临时指定过场背景图；未设置时使用 <see cref="SceneNavigationSettings"/> 默认图。
    /// </summary>
    public static class SceneTransitionPresentation
    {
        public static Sprite PendingBackgroundSprite { get; private set; }

        public static void SetPendingBackground(Sprite sprite)
        {
            PendingBackgroundSprite = sprite;
        }

        public static Sprite ConsumePendingBackground()
        {
            var sprite = PendingBackgroundSprite;
            PendingBackgroundSprite = null;
            return sprite;
        }
    }
}
