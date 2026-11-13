using System;
using System.Globalization;
using System.Text;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Format
{
    /// <summary>
    /// Money display with explicit per-language rules (no CultureInfo, so Web/iOS/Android/server agree).
    /// en: "$1,234.56", "$1.2M"; ru: "1 234,56 $", "1,2 млн $" (U+00A0 spaces). Negative sign is U+2212.
    /// </summary>
    public static class MoneyFormat
    {
        const char Minus = '−';
        const char Nbsp = ' ';
        const long CompactThresholdCents = 1_000_000; // $10,000

        static readonly string[] SuffixEn = { "K", "M", "B", "T" };
        static readonly string[] SuffixRu = { "тыс.", "млн", "млрд", "трлн" };

        public static string Full(Money m, string lang)
        {
            bool ru = IsRu(lang);
            bool neg = m.Cents < 0;
            long a = Math.Abs(m.Cents);
            string num = Group(a / 100, ru) + (ru ? "," : ".") + (a % 100).ToString("00", CultureInfo.InvariantCulture);
            return Wrap(num, null, neg, ru);
        }

        public static string Compact(Money m, string lang)
        {
            bool ru = IsRu(lang);
            bool neg = m.Cents < 0;
            long a = Math.Abs(m.Cents);
            if (a < CompactThresholdCents)
                return Wrap(Group((a + 50) / 100, ru), null, neg, ru);

            double scaled = a / 100.0 / 1000.0;
            int unit = 0;
            while (unit < SuffixEn.Length - 1 && RoundOne(scaled) >= 1000.0)
            {
                scaled /= 1000.0;
                unit++;
            }
            double v = RoundOne(scaled);
            string num = Math.Abs(v - Math.Round(v)) < 1e-9
                ? ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture)
                : v.ToString("0.0", CultureInfo.InvariantCulture);
            if (ru) num = num.Replace('.', ',');
            return Wrap(num, ru ? SuffixRu[unit] : SuffixEn[unit], neg, ru);
        }

        static double RoundOne(double x) => Math.Round(x, 1, MidpointRounding.AwayFromZero);

        static bool IsRu(string lang) => lang != null && lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        static string Wrap(string num, string suffix, bool neg, bool ru)
        {
            var sb = new StringBuilder();
            if (neg) sb.Append(Minus);
            if (ru)
            {
                sb.Append(num);
                if (suffix != null) sb.Append(Nbsp).Append(suffix);
                sb.Append(Nbsp).Append('$');
            }
            else
            {
                sb.Append('$').Append(num);
                if (suffix != null) sb.Append(suffix);
            }
            return sb.ToString();
        }

        static string Group(long n, bool ru)
        {
            string digits = n.ToString(CultureInfo.InvariantCulture);
            char sep = ru ? Nbsp : ',';
            var sb = new StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(sep);
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }
    }
}
