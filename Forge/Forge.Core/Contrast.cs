namespace Forge.Constants
{
    /// <summary>
    /// WCAG 2.x contrast ratio between two sRGB colours. Text needs 4.5:1 (3:1 for large text, 24px
    /// or bold 19px and up). The theme's colours are checked against this by ContrastTests.
    /// </summary>
    public static class Contrast
    {
        public const double Text = 4.5;
        public const double LargeText = 3.0;

        public static double Ratio(string hexA, string hexB)
        {
            var a = Luminance(hexA);
            var b = Luminance(hexB);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }

        /// <summary>Relative luminance of "#RRGGBB" (or "#AARRGGBB", alpha ignored).</summary>
        public static double Luminance(string hex)
        {
            var h = hex.TrimStart('#');
            if (h.Length == 8) h = h[2..];
            if (h.Length != 6) throw new FormatException($"Not a #RRGGBB colour: {hex}");
            double Channel(int i)
            {
                var c = Convert.ToInt32(h.Substring(i, 2), 16) / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
        }
    }
}
