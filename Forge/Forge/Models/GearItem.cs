using System.Text;

using Forge.ViewModels;

namespace Forge.Models
{
    public sealed class GearItem : BaseViewModel
    {
        private bool _owned;

        public required Equipment Equipment { get; init; }
        public required string Name { get; init; }

        /// <summary>"PullUpBar" → "Pull up bar"; "BoxStep" → "Box step".</summary>
        public string DisplayName => string.Concat(Equipment.ToString().Select((c, i) =>
            i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));

        /// <summary>Dumbbells, kettlebells and the barbell open a weights editor instead of toggling.</summary>
        public bool IsWeighted => Forge.Training.GearLoads.IsWeighted(Equipment);

        private string? _summary;
        /// <summary>What's owned, e.g. "15 · 25 · 35 lb"; for weighted gear only.</summary>
        public string? Summary { get => _summary; set { if (SetProperty(ref _summary, value)) OnPropertyChanged(nameof(HasSummary)); } }
        public bool HasSummary => Summary is not null;

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

            // Default: snake_case of the enum name ("PullUpBar" -> "pull_up_bar.png"). Android
            // resource names allow only lowercase letters, digits and underscores — no hyphens.
            return string.Concat(eq.ToString().Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString())) + ".png";
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