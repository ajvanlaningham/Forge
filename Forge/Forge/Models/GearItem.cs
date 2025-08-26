using System.Text;

using Forge.ViewModels;

namespace Forge.Models
{
    public sealed class GearItem : BaseViewModel
    {
        private bool _owned;

        public required Equipment Equipment { get; init; }
        public required string Name { get; init; }

        public bool Owned
        {
            get => _owned;
            set => SetProperty(ref _owned, value);
        }
        /// <summary>
        /// Sprite file name compiled by MAUI (MauiImage).
        /// Rule: kebab-cased enum name → "{kebab}.png" (e.g., "PullUpBar" → "pull-up-bar.png").
        /// If an explicit override exists, use that instead.
        /// </summary>
        public string SpriteSource => GetSpriteFileName(Equipment);

        // ---- Mapping helpers ----

        private static readonly IReadOnlyDictionary<Equipment, string> Overrides =
            new Dictionary<Equipment, string>
            {
                // Put exact filenames here when they don't follow the rule
                // { Equipment.Barbell, "barbell.png" },     // (rule already produces this)
                // { Equipment.JumpRope, "jump-rope.png" },  // (rule already produces this)
            };

        private static string GetSpriteFileName(Equipment eq)
        {
            if (Overrides.TryGetValue(eq, out var file)) return file;

            // Default: convert enum name to kebab-case, then ".png"
            var kebab = ToKebab(eq.ToString().ToLower());
            return $"{kebab}.png";
        }

        private static string ToKebab(string s)
        {
            // "PullUpBar" -> "pull-up-bar", "BoxStep" -> "box-step"
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]))))
                        sb.Append('-');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                // ignore spaces/underscores; hyphens are inserted by case rule
            }
            return sb.ToString().ToLower();
        }
    }
}