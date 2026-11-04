using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Util
{
    /// <summary>
    /// 将 <see cref="Sprite"/> 正确显示到 <see cref="RawImage"/>（含 Sprite Atlas 的 UV 裁剪）。
    /// 切勿对图集 Sprite 使用 <c>rawImage.texture = sprite.texture</c>，那会显示整张图集。
    /// </summary>
    public static class UiSpriteRawImageUtil
    {
        public static void Apply(RawImage rawImage, Sprite sprite)
        {
            if (rawImage == null)
                return;

            if (sprite == null)
            {
                rawImage.texture = null;
                rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            var tex = sprite.texture;
            rawImage.texture = tex;
            if (tex == null)
            {
                rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            var rect = sprite.textureRect;
            rawImage.uvRect = new Rect(
                rect.x / tex.width,
                rect.y / tex.height,
                rect.width / tex.width,
                rect.height / tex.height);
        }
    }
}
