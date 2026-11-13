using Forge.Constants;
using Forge.Services.Interfaces;

namespace Forge.Views;

/// <summary>
/// Shown at launch while the library is seeded and the stats are opened: the hammer strikes the
/// anvil (art/gen_forge.py) under a rotating line of forge talk. Hands over to the tabs when ready,
/// after at least one full strike so it never flickers.
/// </summary>
public sealed class LoadingPage : ContentPage
{
    private static readonly string[] Lines =
    {
        "Stoking the forge…", "Heating the copper…", "Checking the quest board…", "Sharpening the tools…"
    };
    private static readonly (string Frame, int Ms)[] Strike = { ("forge_0.png", 420), ("forge_1.png", 90), ("forge_2.png", 300) };

    private readonly Image _anvil = new() { WidthRequest = 220, HeightRequest = 220, Aspect = Aspect.AspectFit, Source = "forge_0.png" };
    private readonly Label _line = new()
    {
        FontFamily = "Body", FontSize = 22, HorizontalTextAlignment = TextAlignment.Center,
        TextColor = (Color)Application.Current!.Resources["Muted"]
    };
    private readonly Action _ready;
    private bool _done;

    public LoadingPage(Action ready)
    {
        _ready = ready;
        BackgroundColor = (Color)Application.Current!.Resources["Bg"];
        Shell.SetNavBarIsVisible(this, false);
        Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Spacing = 14,
            Children =
            {
                _anvil,
                new Label
                {
                    Text = "FITNESS FORGE", FontFamily = "Display", FontSize = 24, HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = (Color)Application.Current.Resources["Accent"]
                },
                _line
            }
        };
        _line.Text = Lines[Random.Shared.Next(Lines.Length)];
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var animate = AnimateAsync();
        var work = PrepareAsync();
        await Task.WhenAll(work, Task.Delay(Strike.Sum(s => s.Ms) + 150));
        _done = true;
        await animate;
        _ready();
    }

    private async Task AnimateAsync()
    {
        var n = 0;
        while (!_done)
        {
            foreach (var (frame, ms) in Strike)
            {
                _anvil.Source = frame;
                await Task.Delay(ms);
            }
            if (++n % 2 == 0) _line.Text = Lines[(Array.IndexOf(Lines, _line.Text) + 1) % Lines.Length];
        }
    }

    /// <summary>The slow first-run work: seeding the exercise library and opening the stats.</summary>
    private static async Task PrepareAsync()
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services?.GetService<IExerciseLibraryImporter>() is { } importer)
                await importer.EnsureSeededAsync(GameConstants.Exercises.LibraryFiles, GameConstants.Exercises.LibraryVersion);
            if (services?.GetService<IStatsService>() is { } stats)
                await stats.InitAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Startup preparation failed (Today retries): {ex}");
        }
    }
}
