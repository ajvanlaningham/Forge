using Forge.Services;

namespace Forge.Views.SubPages;

/// <summary>
/// Optional: place test results against published norms (Decision 23). Asks only what the norms
/// need — which set of norms and an age — and never an identity. Scores never depend on it.
/// </summary>
public sealed class ComparisonsPage : ContentPage
{
    private static readonly (string Id, string Label)[] Groups =
        { ("none", "Don't compare"), ("women", "Women's norms"), ("men", "Men's norms") };

    public ComparisonsPage()
    {
        Title = "Comparisons";
        var res = Application.Current!.Resources;
        var muted = (Color)res["Muted"];
        var onBg = (Color)res["OnBg"];

        var picker = new Picker
        {
            Title = "Compare my results with", ItemsSource = Groups.Select(g => g.Label).ToList(), FontSize = 18, TextColor = onBg,
            SelectedIndex = Math.Max(0, Array.FindIndex(Groups, g => g.Id == UserSettings.NormsGroup))
        };
        picker.SelectedIndexChanged += (_, _) => UserSettings.NormsGroup = Groups[Math.Max(0, picker.SelectedIndex)].Id;

        var year = new Entry
        {
            Keyboard = Keyboard.Numeric, Placeholder = "e.g. 1988", Text = UserSettings.BirthYear?.ToString() ?? "",
            FontFamily = "GymNum", FontSize = 24, TextColor = onBg, WidthRequest = 140
        };
        year.TextChanged += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.NewTextValue)) UserSettings.BirthYear = null;
            else if (int.TryParse(e.NewTextValue, out var y) && y > 1900 && y <= DateTime.Today.Year) UserSettings.BirthYear = y;
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 14),
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Fitness norms were collected separately for women and men, by age. If you'd like a test result placed against them, pick the set that fits your body best, and add your birth year. Or skip it: your scores only ever compare you with yourself.", FontSize = 15, TextColor = muted },
                    new Label { Text = "Norms", FontFamily = "Display", FontSize = 22, TextColor = onBg },
                    picker,
                    new Label { Text = "Birth year", FontFamily = "Display", FontSize = 22, TextColor = onBg },
                    year,
                    new Label { Text = "Used only to pick the right row of a norms table. It stays on this phone.", FontSize = 15, TextColor = muted }
                }
            }
        };
    }
}
