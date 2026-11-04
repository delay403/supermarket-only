using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 将 0～9 精灵数组绑定到一排 <see cref="Image"/>（左高位），与滑翔 HUD 位图字逻辑一致。
    /// </summary>
    public static class BitmapDigitStripBinder
    {
        public static void Bind(Image[] slots, Sprite[] digitSprites0To9, int value)
        {
            Bind(slots, digitSprites0To9, Mathf.Max(0, value).ToString());
        }

        public static void Bind(Image[] slots, Sprite[] digitSprites0To9, long value)
        {
            var v = value < 0 ? 0 : value;
            Bind(slots, digitSprites0To9, v.ToString());
        }

        public static void Bind(Image[] slots, Sprite[] digitSprites0To9, string numericString)
        {
            if (slots == null || slots.Length == 0 || digitSprites0To9 == null || digitSprites0To9.Length < 10)
                return;

            var s = string.IsNullOrEmpty(numericString) ? "0" : numericString;
            if (s.Length > slots.Length && slots.Length > 0)
                s = s.Substring(s.Length - slots.Length);

            var len = s.Length;
            for (var i = 0; i < slots.Length; i++)
            {
                var img = slots[i];
                if (img == null)
                    continue;

                if (i >= len)
                {
                    img.gameObject.SetActive(false);
                    continue;
                }

                var c = s[i] - '0';
                if (c < 0 || c > 9 || digitSprites0To9[c] == null)
                {
                    img.gameObject.SetActive(false);
                    continue;
                }

                img.sprite = digitSprites0To9[c];
                img.gameObject.SetActive(true);
            }
        }

        public static Image[] CacheDigitImages(RectTransform digitStrip)
        {
            if (digitStrip == null)
                return System.Array.Empty<Image>();

            var n = digitStrip.childCount;
            var arr = new Image[n];
            for (var i = 0; i < n; i++)
                arr[i] = digitStrip.GetChild(i).GetComponent<Image>();

            return arr;
        }
    }
}
