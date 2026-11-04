using System.Globalization;
using System.Text;

namespace Game.Economy
{
    /// <summary>
    /// HUD 金币/宝石等大额数字展示：&lt;1000 原样；≥1000 千分位逗号；≥100000 以千为单位取整后加 k，并对 k 前数字加逗号。
    /// </summary>
    public static class EconomyDisplayFormat
    {
        public static string Format(long value)
        {
            if (value < 0) value = 0;
            if (value < 1000) return value.ToString(CultureInfo.InvariantCulture);
            if (value < 100_000) return InsertCommas(value);
            var k = value / 1000;
            return InsertCommas(k) + "k";
        }

        private static string InsertCommas(long n)
        {
            var s = n.ToString(CultureInfo.InvariantCulture);
            if (s.Length <= 3) return s;

            var first = s.Length % 3;
            if (first == 0) first = 3;

            var sb = new StringBuilder();
            sb.Append(s.Substring(0, first));
            for (var i = first; i < s.Length; i += 3)
            {
                sb.Append(',');
                sb.Append(s.Substring(i, 3));
            }

            return sb.ToString();
        }
    }
}
