using FairyGUI;
using UnityEngine;

namespace Game.UI.FairyGUI
{
    /// <summary>Unity <see cref="Sprite"/> → FairyGUI <see cref="NTexture"/>（用于 Loader / icon 等）。</summary>
    public static class FguiNTextureUtil
    {
        public static NTexture FromSprite(Sprite sprite)
        {
            if (sprite == null) return null;
            return new NTexture(sprite);
        }

        /// <summary>
        /// <see cref="GButton.icon"/> 为包内 URL 字符串；运行时贴 Unity Sprite 需写到子节点 <c>icon</c> 的 <see cref="GLoader.texture"/>。
        /// </summary>
        public static void ApplySpriteToButtonIcon(GButton button, Sprite sprite)
        {
            if (button == null) return;
            var loader = button.GetChild("icon")?.asLoader;
            if (loader == null) return;
            loader.texture = FromSprite(sprite);
        }
    }
}
