#if UNITY_EDITOR
using System;
using System.Text;

namespace Game.EditorTools
{
    public enum BatchRenameSequenceKind
    {
        None = 0,
        Number1 = 1,
        LetterLowerA = 2,
        LetterUpperA = 3,
    }

    /// <summary>批量命名序号与名称拼接规则。</summary>
    public static class BatchRenameSequenceUtility
    {
        public static int CompareNatural(string left, string right, bool ignoreCase = true)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            var li = 0;
            var ri = 0;
            while (li < left.Length && ri < right.Length)
            {
                var lc = left[li];
                var rc = right[ri];
                var ld = char.IsDigit(lc);
                var rd = char.IsDigit(rc);

                if (ld && rd)
                {
                    var lNum = ParseNumber(left, ref li);
                    var rNum = ParseNumber(right, ref ri);
                    if (lNum != rNum)
                        return lNum < rNum ? -1 : 1;
                    continue;
                }

                if (ld != rd)
                    return ld ? -1 : 1;

                if (ignoreCase)
                {
                    lc = char.ToUpperInvariant(lc);
                    rc = char.ToUpperInvariant(rc);
                }

                if (lc != rc)
                    return lc < rc ? -1 : 1;

                li++;
                ri++;
            }

            return left.Length.CompareTo(right.Length);
        }

        static long ParseNumber(string text, ref int index)
        {
            long value = 0;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                value = value * 10 + (text[index] - '0');
                index++;
            }

            return value;
        }

        public static int ResolveNumberPaddingWidth(int totalCount)
        {
            if (totalCount <= 0)
                return 1;

            var width = 1;
            var threshold = 10;
            while (totalCount >= threshold && width < 6)
            {
                width++;
                threshold *= 10;
            }

            return width;
        }

        public static string FormatSequence(
            BatchRenameSequenceKind kind,
            int index,
            int totalCount,
            int numberStart = 1,
            char letterStart = '\0')
        {
            if (kind == BatchRenameSequenceKind.None || index < 0 || totalCount <= 0)
                return string.Empty;

            return kind switch
            {
                BatchRenameSequenceKind.Number1 => FormatNumber(index, totalCount, numberStart),
                BatchRenameSequenceKind.LetterLowerA => FormatLetters(index, 'a', ResolveLetterStart('a', letterStart)),
                BatchRenameSequenceKind.LetterUpperA => FormatLetters(index, 'A', ResolveLetterStart('A', letterStart)),
                _ => string.Empty,
            };
        }

        public static string BuildName(
            string prefix,
            string suffix,
            string connector,
            BatchRenameSequenceKind frontKind,
            BatchRenameSequenceKind backKind,
            int index,
            int totalCount,
            int frontNumberStart = 1,
            int backNumberStart = 1,
            char frontLetterStart = '\0',
            char backLetterStart = '\0')
        {
            var front = FormatSequence(frontKind, index, totalCount, frontNumberStart, frontLetterStart);
            var back = FormatSequence(backKind, index, totalCount, backNumberStart, backLetterStart);
            var pieces = new[] { prefix ?? string.Empty, front, back, suffix ?? string.Empty };
            var result = new StringBuilder();
            var started = false;

            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (string.IsNullOrEmpty(piece))
                    continue;

                if (started)
                    result.Append(connector ?? string.Empty);

                result.Append(piece);
                started = true;
            }

            return result.Length > 0 ? result.ToString() : "unnamed";
        }

        static string FormatNumber(int index, int totalCount, int startNumber = 1)
        {
            var value = startNumber + index;
            var maxValue = startNumber + Math.Max(0, totalCount - 1);
            var width = Math.Max(ResolveNumberPaddingWidth(totalCount), maxValue.ToString().Length);
            return width <= 1 && maxValue < 10 ? value.ToString() : value.ToString().PadLeft(width, '0');
        }

        static char ResolveLetterStart(char defaultChar, char letterStart)
        {
            if (letterStart == '\0')
                return defaultChar;

            if (char.IsLetter(letterStart))
                return char.IsUpper(defaultChar) ? char.ToUpperInvariant(letterStart) : char.ToLowerInvariant(letterStart);

            return defaultChar;
        }

        static string FormatLetters(int index, char baseChar, char startLetter)
        {
            var normalizedBase = char.ToLowerInvariant(baseChar);
            var normalizedStart = char.ToLowerInvariant(startLetter);
            if (!char.IsLetter(normalizedStart))
                normalizedStart = normalizedBase;

            var startOffset = normalizedStart - normalizedBase;
            if (startOffset < 0)
                startOffset = 0;

            return IndexToLetters(index + startOffset, baseChar);
        }

        static string IndexToLetters(int index, char baseChar)
        {
            var value = index + 1;
            if (value <= 0)
                return string.Empty;

            var builder = new StringBuilder();
            while (value > 0)
            {
                value--;
                builder.Insert(0, (char)(baseChar + (value % 26)));
                value /= 26;
            }

            return builder.ToString();
        }
    }
}
#endif
