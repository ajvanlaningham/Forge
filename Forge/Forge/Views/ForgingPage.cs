using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Views;

/// <summary>
/// The end of a quest (Decisions 18, 25): the one big celebration. The hammer strikes; a passed
/// quest earns a forging and the player picks which of the five slots climbs one metal. A missed
/// one says kindly that the metal cooled (the training still counted). Shown once per judged
/// block; a forging not spent here waits on the Hero tab.
/// </summary>
public sealed class ForgingPage : ContentPage
{
    private static readonly (string Frame, int Ms)[] Strike = { ("forge_0.png", 380), ("forge_1.png", 80), ("forge_2.png", 280) };

    private readonly ForgeResultRow _result;
    private readonly IProgramService _programs;
    private readonly IForgeService _forge;
    private readonly Image _anvil = new() { WidthRequest = 180, HeightRequest = 180, Source = "forge_0.png" };
    private readonly VerticalStackLayout _reveal = new() { Spacing = 16, Opacity = 0 };
    private readonly Color _accent, _muted, _onBg, _bg, _surface;
    private bool _played;

    public ForgingPage(ForgeResultRow result, IProgramService programs, IForgeService forge)
    {
        _result = result;
        _programs = programs;
        _forge = forge;
        var res = Application.Current!.Resources;
        _accent = (Color)res["Accent"];
        _muted = (Color)res["Muted"];
        _onBg = (Color)res["OnBg"];
        _bg = (Color)res["Bg"];
        _surface = (Color)res["Surface"];
        BackgroundColor = _bg;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 32),
                Spacing = 18,
                Children = { _anvil, _reveal }
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
        _anvil.Source = "forge_0.png";
        if (_result.Forged) await ShowChoiceAsync();
        else ShowCooled();
        await _reveal.FadeToAsync(1, 400);
    }

    private Label Title(string text, Color color) => new()
    {
        Text = text, FontFamily = "Display", FontSize = 34, TextColor = color, HorizontalTextAlignment = TextAlignment.Center
    };

    private Label Line(string text) => new()
    {
        Text = text, FontFamily = "Body", FontSize = 18, TextColor = _muted, HorizontalTextAlignment = TextAlignment.Center
    };

    private Button Primary(string text) => new()
    {
        Text = text, HeightRequest = 64, CornerRadius = 14, FontFamily = "Display", FontSize = 28,
        BackgroundColor = _accent, TextColor = _bg
    };

    private void ShowCooled()
    {
        _reveal.Children.Add(Title("THE METAL COOLED", _muted));
        _reveal.Children.Add(Line($"{_result.DaysDone} of {_result.DaysTotal} training days; {_result.DaysNeeded} earn a forging. Every session still counted toward your level and your lifts."));
        var again = Primary("TRY AGAIN");
        again.Clicked += async (_, _) =>
        {
            if (await _programs.GetActiveAsync() is { } active)
                await _programs.StartProgramAsync(active.Definition.Id, DateOnly.FromDateTime(DateTime.Today));
            await CloseAsync("//today");
        };
        var other = new Button { Text = "Choose another quest", HeightRequest = 56, FontFamily = "Display", FontSize = 22 };
        other.Clicked += async (_, _) => { await Navigation.PopModalAsync(); await Shell.Current.GoToAsync("//today"); await Shell.Current.GoToAsync(nameof(QuestPage)); };
        _reveal.Children.Add(again);
        _reveal.Children.Add(other);
    }

    /// <summary>Five big tiles: each slot's current metal and what this forging would make it.</summary>
    private async Task ShowChoiceAsync()
    {
        _reveal.Children.Add(Title("A FORGING EARNED", _accent));
        _reveal.Children.Add(Line($"{_result.DaysDone} of {_result.DaysTotal} training days. Choose a piece to raise one metal."));

        var forged = await _forge.GetForgedAsync();
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 12, RowSpacing = 12 };
        for (var i = 0; i < Armoury.Slots.Length; i++)
        {
            var slot = Armoury.Slots[i];
            var now = Armoury.MetalOf(forged, slot);
            var next = Armoury.NextMetal(forged, slot);
            var tile = Tile(slot, now, next);
            if (i / 2 >= grid.RowDefinitions.Count) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Add(tile, i % 2, i / 2);
        }
        _reveal.Children.Add(grid);

        var later = new Button { Text = "Decide later", HeightRequest = 56, FontFamily = "Display", FontSize = 22 };
        later.Clicked += async (_, _) => await CloseAsync("//hero");
        _reveal.Children.Add(later);
    }

    private View Tile(GearSlot slot, string? now, string? next)
    {
        var sprite = HeroQuest.Sprite(slot, next ?? now ?? "Copper");
        var stack = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        if (sprite is not null) stack.Children.Add(new Image { Source = sprite, HeightRequest = 64, WidthRequest = 64 });
        stack.Children.Add(new Label { Text = Armoury.Label(slot), FontFamily = "Display", FontSize = 26, TextColor = _onBg, HorizontalTextAlignment = TextAlignment.Center });
        stack.Children.Add(new Label
        {
            Text = next is null ? $"{now}: maxed" : now is null ? $"bare → {next}" : $"{now} → {next}",
            FontFamily = "Body", FontSize = 16, TextColor = next is null ? _muted : _accent, HorizontalTextAlignment = TextAlignment.Center
        });
        var border = new Border
        {
            Content = stack, Padding = new Thickness(8, 14), BackgroundColor = _surface,
            Stroke = next is null ? _muted : _accent, StrokeThickness = 2,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Opacity = next is null ? 0.5 : 1
        };
        if (next is not null)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await SpendAsync(slot);
            border.GestureRecognizers.Add(tap);
        }
        return border;
    }

    private async Task SpendAsync(GearSlot slot)
    {
        var metal = await _forge.SpendAsync(_result.BlockId, slot);
        if (metal is null) return;
        Buzz(300);
        await _reveal.FadeToAsync(0, 200);
        _reveal.Children.Clear();
        var sprite = HeroQuest.Sprite(slot, metal);
        if (sprite is not null)
        {
            _anvil.Source = sprite;
            _anvil.Scale = 0.6;
            _ = _anvil.ScaleToAsync(1, 400, Easing.CubicOut);
        }
        _reveal.Children.Add(Title("FORGED", _accent));
        _reveal.Children.Add(new Label
        {
            Text = $"{metal} {HeroQuest.ItemName(slot)}", FontFamily = "Display", FontSize = 30, TextColor = _onBg,
            HorizontalTextAlignment = TextAlignment.Center
        });
        var done = Primary("DONE");
        done.Clicked += async (_, _) => await CloseAsync("//hero");
        _reveal.Children.Add(done);
        await _reveal.FadeToAsync(1, 300);
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
