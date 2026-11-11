using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Views;

/// <summary>
/// The end of a quest (Decision 18): the hammer strikes, then the piece is revealed, forged in
/// its metal, or the metal has cooled (said kindly: the training still counted). Shown once per
/// judged block, from Today.
/// </summary>
public sealed class ForgingPage : ContentPage
{
    private static readonly (string Frame, int Ms)[] Strike = { ("forge_0.png", 380), ("forge_1.png", 80), ("forge_2.png", 280) };

    private readonly ForgeResultRow _result;
    private readonly IProgramService _programs;
    private readonly Image _anvil = new() { WidthRequest = 200, HeightRequest = 200, Source = "forge_0.png" };
    private readonly Image _item = new() { WidthRequest = 180, HeightRequest = 180, Opacity = 0, Scale = 0.6 };
    private readonly VerticalStackLayout _reveal = new() { Spacing = 12, Opacity = 0 };
    private bool _played;

    public ForgingPage(ForgeResultRow result, IProgramService programs)
    {
        _result = result;
        _programs = programs;
        var res = Application.Current!.Resources;
        var accent = (Color)res["Accent"];
        var muted = (Color)res["Muted"];
        var onBg = (Color)res["OnBg"];
        BackgroundColor = (Color)res["Bg"];

        var slot = (GearSlot)result.Slot;
        var name = $"{result.Metal} {HeroQuest.ItemName(slot)}";
        _item.Source = HeroQuest.Sprite(slot, result.Metal);

        var title = new Label
        {
            Text = result.Forged ? "FORGED" : "THE METAL COOLED",
            FontFamily = "PixelH", FontSize = 22, TextColor = result.Forged ? accent : muted,
            HorizontalTextAlignment = TextAlignment.Center
        };
        var item = new Label
        {
            Text = name, FontFamily = "PixelUI", FontSize = 34, TextColor = onBg, HorizontalTextAlignment = TextAlignment.Center
        };
        var body = new Label
        {
            Text = result.Forged
                ? $"{result.DaysDone} of {result.DaysTotal} training days. It's yours, and it's on the Armoury rack."
                : $"{result.DaysDone} of {result.DaysTotal} training days; {result.DaysNeeded} forge it. Every session still counted toward your level and your lifts. The forge is ready when you are.",
            FontFamily = "PixelUI", FontSize = 21, TextColor = muted, HorizontalTextAlignment = TextAlignment.Center
        };
        _reveal.Children.Add(title);
        _reveal.Children.Add(item);
        _reveal.Children.Add(body);
        if (!result.Forged) _item.Opacity = 0;

        var primary = new Button
        {
            Text = result.Forged ? "To the Armoury" : "Try this quest again",
            HeightRequest = 60, FontFamily = "PixelUI", FontSize = 24, BackgroundColor = accent, TextColor = (Color)res["Bg"]
        };
        primary.Clicked += async (_, _) =>
        {
            if (!result.Forged && await _programs.GetActiveAsync() is { } active)
                await _programs.StartProgramAsync(active.Definition.Id, DateOnly.FromDateTime(DateTime.Today));
            await CloseAsync(result.Forged ? "//hero" : "//today");
        };
        var secondary = new Button { Text = "Choose the next quest", HeightRequest = 52, FontFamily = "PixelUI", FontSize = 20 };
        secondary.Clicked += async (_, _) => await CloseAsync("//quest");
        _reveal.Children.Add(primary);
        _reveal.Children.Add(secondary);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 40),
                Spacing = 18,
                VerticalOptions = LayoutOptions.Center,
                Children = { new Grid { Children = { _anvil, _item } }, _reveal }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_played) return;
        _played = true;
        for (var n = 0; n < 3; n++)
            foreach (var (frame, ms) in Strike)
            {
                _anvil.Source = frame;
                if (frame == "forge_2.png") Buzz(n == 2 ? 300 : 60);
                await Task.Delay(ms);
            }
        if (_result.Forged)
        {
            await _anvil.FadeTo(0, 250);
            await Task.WhenAll(_item.FadeTo(1, 400), _item.ScaleTo(1, 400, Easing.CubicOut));
        }
        else
        {
            _anvil.Source = "forge_0.png";
        }
        await _reveal.FadeTo(1, 400);
    }

    private async Task CloseAsync(string route)
    {
        await Navigation.PopModalAsync();
        await Shell.Current.GoToAsync(route);
    }

    private static void Buzz(int ms)
    {
        try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(ms)); } catch { /* no vibrator */ }
    }
}
