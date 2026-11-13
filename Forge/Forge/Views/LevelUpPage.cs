namespace Forge.Views;

/// <summary>
/// The level-up moment (PBI 4.2): a full-screen beat distinct from the routine XP dialog. Levels
/// measure consistency, so the copy celebrates showing up, never a number on a bar or the scale.
/// </summary>
public sealed class LevelUpPage : ContentPage
{
    public LevelUpPage(int level)
    {
        BackgroundColor = (Color)Application.Current!.Resources["Bg"];
        var accent = (Color)Application.Current.Resources["Accent"];
        var muted = (Color)Application.Current.Resources["Muted"];
        var onBg = (Color)Application.Current.Resources["OnBg"];

        var badge = new Border
        {
            WidthRequest = 170,
            HeightRequest = 170,
            StrokeThickness = 0,
            BackgroundColor = accent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 85 },
            HorizontalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = level.ToString(), FontFamily = "Display", FontSize = 56, TextColor = Colors.Black, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = "LVL", FontFamily = "Display", FontSize = 22, TextColor = Colors.Black, HorizontalTextAlignment = TextAlignment.Center }
                }
            }
        };

        var ok = new Button
        {
            Text = "Onward",
            FontFamily = "Display",
            FontSize = 24,
            HeightRequest = 60,
            BackgroundColor = accent,
            TextColor = Colors.Black,
            Margin = new Thickness(0, 24, 0, 0)
        };
        ok.Clicked += async (_, _) => await Navigation.PopModalAsync();

        var lines = new VerticalStackLayout
        {
            Spacing = 18,
            Padding = new Thickness(28, 0),
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "LEVEL UP", FontFamily = "Display", FontSize = 26, TextColor = accent, HorizontalTextAlignment = TextAlignment.Center },
                badge,
                new Label
                {
                    Text = "A week of showing up, forged into something that stays.",
                    FontFamily = "Display", FontSize = 24, TextColor = onBg, HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };

        lines.Children.Add(ok);
        Content = lines;

        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { /* no haptics */ }
    }
}
