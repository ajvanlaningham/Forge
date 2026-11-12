using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Views;

/// <summary>
/// The first run (Decision 20's journey): start fresh or restore a backup, then units, a hero, the
/// gear at home, and a first quest in the version that fits. A few taps, nothing typed; everything
/// can be changed later. Installs that already have data skip it (<see cref="NeededAsync"/>).
/// </summary>
public sealed class OnboardingPage : ContentPage
{
    private readonly Action _done;
    private readonly bool _preview;
    private readonly IServiceProvider _services;

    private readonly Color _accent = (Color)Application.Current!.Resources["Accent"];
    private readonly Color _surface = (Color)Application.Current.Resources["Surface"];
    private readonly Color _muted = (Color)Application.Current.Resources["Muted"];
    private readonly Color _onBg = (Color)Application.Current.Resources["OnBg"];
    private readonly Color _bg = (Color)Application.Current.Resources["Bg"];

    // Choices, applied together at the end.
    private WeightUnit _unit = WeightUnit.Pounds;
    private string _race = "construct";
    private string _class = "wizard";
    private Equipment _gear = Equipment.Wall;
    private string _discipline = "Hearth";
    private int _days = 4;
    private int _step;

    /// <summary>Home gear offered at onboarding: what people have at home (gym machines are left out).</summary>
    private static readonly Equipment[] HomeGear =
    {
        Equipment.Dumbbell, Equipment.Kettlebell, Equipment.Barbell, Equipment.Bench, Equipment.PullUpBar,
        Equipment.Bands, Equipment.BoxStep, Equipment.Mat, Equipment.Bike, Equipment.JumpRope,
        Equipment.PunchingBag, Equipment.MedicineBall, Equipment.WeightVest, Equipment.SuspensionTrainer
    };

    /// <param name="preview">A walk-through from Settings: the steps run, but nothing is applied.</param>
    public OnboardingPage(Action done, bool preview = false)
    {
        _done = done;
        _preview = preview;
        _services = IPlatformApplication.Current!.Services;
        BackgroundColor = _bg;
        Shell.SetNavBarIsVisible(this, false);
        Show(0);
    }

    /// <summary>
    /// True on a fresh install. An install that already has training, a block or a weigh-in (an
    /// existing user updating the app) is marked done instead, so it never sees the first run.
    /// </summary>
    public static async Task<bool> NeededAsync()
    {
        if (UserSettings.OnboardingDone) return false;
        var services = IPlatformApplication.Current?.Services;
        if (services is null) return false;
        var hasData =
            await services.GetRequiredService<IRepository<ProgramBlockRow>>().FirstOrDefaultAsync(b => b.Id > 0) is not null
            || await services.GetRequiredService<IRepository<TrainingSessionRow>>().FirstOrDefaultAsync(s => s.Id > 0) is not null
            || await services.GetRequiredService<IWeightService>().GetLatestAsync() is not null;
        if (hasData) UserSettings.OnboardingDone = true;
        return !hasData;
    }

    // ------------------------------------------------------------------ steps

    private void Show(int step)
    {
        _step = step;
        View body = step switch
        {
            0 => Welcome(),
            1 => Units(),
            2 => Hero(),
            3 => Gear(),
            _ => Quest()
        };
        var stack = new VerticalStackLayout { Padding = new Thickness(20, 28, 20, 28), Spacing = 16 };
        if (step > 0)
        {
            var nav = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
            var back = new Button { Text = "‹ Back", FontFamily = "PixelUI", FontSize = 20, BackgroundColor = Colors.Transparent, TextColor = _muted, BorderWidth = 0, Padding = 0 };
            back.Clicked += (_, _) => Show(_step - 1);
            nav.Add(back);
            nav.Add(new Label { Text = $"{step} of 4", FontFamily = "PixelUI", FontSize = 20, TextColor = _muted, HorizontalTextAlignment = TextAlignment.End, VerticalOptions = LayoutOptions.Center }, 1);
            stack.Add(nav);
        }
        stack.Add(body);
        Content = new ScrollView { Content = stack };
    }

    private View Welcome()
    {
        var fresh = Primary("Start fresh");
        fresh.Clicked += (_, _) => Show(1);
        var restore = new Button { Text = "Restore a backup", HeightRequest = 56, FontFamily = "PixelUI", FontSize = 22 };
        restore.Clicked += async (_, _) =>
        {
            if (_preview) { await DisplayAlertAsync("Preview", "In a real first run this opens the backup picker.", "OK"); return; }
            await RestoreFlow.RunAsync(this, _services.GetRequiredService<IBackupService>());
        };
        return new VerticalStackLayout
        {
            Spacing = 18,
            Children =
            {
                new Image { Source = "forge_2.png", WidthRequest = 220, HeightRequest = 220, HorizontalOptions = LayoutOptions.Center },
                new Label { Text = "FITNESS FORGE", FontFamily = "PixelH", FontSize = 24, TextColor = _accent, HorizontalTextAlignment = TextAlignment.Center },
                Body("Train at home, a quest at a time. Four weeks at the forge, and the piece you forge is yours. Every session moves you on."),
                fresh,
                restore,
                Note("Restoring brings back everything from a Forge backup (Download/Forge on this phone, or a copy you saved).")
            }
        };
    }

    private View Units()
    {
        var lb = Choice("Pounds (lb)", _unit == WeightUnit.Pounds);
        var kg = Choice("Kilograms (kg)", _unit == WeightUnit.Kilograms);
        lb.Clicked += (_, _) => { _unit = WeightUnit.Pounds; Show(1); };
        kg.Clicked += (_, _) => { _unit = WeightUnit.Kilograms; Show(1); };
        var next = Primary("Next");
        next.Clicked += (_, _) => Show(2);
        return Stack(Title("Which units do you lift in?"), lb, kg, Note("Change it any time in Settings."), next);
    }

    private View Hero()
    {
        var preview = new Image { Source = UserSettings.HeroSprite(_race, _class), HeightRequest = 200, HorizontalOptions = LayoutOptions.Center };
        var name = new Label
        {
            Text = $"{Label(UserSettings.Races, _race)} {Label(UserSettings.Classes, _class)}",
            FontFamily = "PixelUI", FontSize = 28, TextColor = _accent, HorizontalTextAlignment = TextAlignment.Center
        };
        var races = Chips(UserSettings.Races, () => _race, id => { _race = id; Show(2); });
        var classes = Chips(UserSettings.Classes, () => _class, id => { _class = id; Show(2); });
        var next = Primary("Next");
        next.Clicked += (_, _) => Show(3);
        return Stack(Title("Choose your hero"), preview, name, Small("Race"), races, Small("Class"), classes,
            Note("Change it any time on the Hero tab. The gear you forge is made for your hero."), next);
    }

    private View Gear()
    {
        var flex = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start };
        foreach (var e in HomeGear)
        {
            var item = new GearItem { Equipment = e, Name = e.ToString() };
            var on = _gear.HasFlag(e);
            var tile = new Border
            {
                WidthRequest = 100, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(4, 8),
                BackgroundColor = _surface, Stroke = on ? _accent : _muted, StrokeThickness = on ? 3 : 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Image { Source = item.SpriteSource, HeightRequest = 36, Opacity = on ? 1 : 0.45 },
                        new Label { Text = item.DisplayName, FontSize = 13, HorizontalTextAlignment = TextAlignment.Center, TextColor = on ? _onBg : _muted }
                    }
                }
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => { _gear ^= e; Show(3); };
            tile.GestureRecognizers.Add(tap);
            flex.Add(tile);
        }
        var next = Primary("Next");
        next.Clicked += (_, _) => Show(4);
        return Stack(Title("What do you have at home?"), Body("Tap everything you have. No gear at all is fine: quests swap in bodyweight moves."),
            flex, Note("Which dumbbell or kettlebell weights you own can wait: set them on Hero → Edit gear when you're ready."), next);
    }

    private View Quest()
    {
        var options = new[]
        {
            ("Hearth", "Balanced", "Strength and muscle together, with movement every day."),
            ("Anvil", "Stronger", "Heavier lifts, fewer reps, full rest."),
            ("Bellows", "Fitter", "Breath and work capacity: intervals, circuits, a long ride."),
        };
        var cards = new VerticalStackLayout { Spacing = 8 };
        foreach (var (id, label, about) in options)
        {
            var on = id == _discipline;
            var card = new Border
            {
                BackgroundColor = _surface, Stroke = on ? _accent : _muted, StrokeThickness = on ? 3 : 1, Padding = 12,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label { Text = $"{label} · {id}", FontFamily = "PixelUI", FontSize = 24, TextColor = on ? _accent : _onBg },
                        new Label { Text = about, FontSize = 14, TextColor = _muted }
                    }
                }
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => { _discipline = id; Show(4); };
            card.GestureRecognizers.Add(tap);
            cards.Add(card);
        }

        var three = Choice("3 days a week", _days == 3);
        var four = Choice("4 days a week", _days == 4);
        three.Clicked += (_, _) => { _days = 3; Show(4); };
        four.Clicked += (_, _) => { _days = 4; Show(4); };
        var days = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 8 };
        days.Add(three);
        days.Add(four, 1);

        // The quest, as the board will show it, named for the hero.
        var slot = Armoury.SlotFor(_discipline);
        var item = HeroGear.Name(_race, _class, slot);
        var programId = FirstQuest.ProgramId(_discipline, _gear.HasFlag(Equipment.Dumbbell), _gear.HasFlag(Equipment.Kettlebell), _days);
        var implement = programId.Contains("-kb-") ? "Kettlebell" : "Dumbbell";
        var notice = new Border
        {
            BackgroundColor = Color.FromArgb("#D9C9A3"), Stroke = Color.FromArgb("#2B1D14"), StrokeThickness = 3, Padding = 14,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 2 },
            Content = new Grid
            {
                ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 12,
                Children =
                {
                    new Image { Source = HeroGear.Sprite(_race, _class, slot, "Copper"), WidthRequest = 56, HeightRequest = 56 },
                }
            }
        };
        var noticeText = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = $"Copper {item}", FontFamily = "FantasyH", FontSize = 24, TextColor = Color.FromArgb("#2B1D14") },
                new Label { Text = $"{_discipline} · {implement} · {_days}-day", FontFamily = "PixelUI", FontSize = 19, TextColor = Color.FromArgb("#8E2A2A") },
                new Label { Text = "Four weeks. Train 75% of the days and it's forged.", FontFamily = "PixelUI", FontSize = 17, TextColor = Color.FromArgb("#5A4330") }
            }
        };
        ((Grid)notice.Content).Add(noticeText, 1);

        var accept = Primary("Accept the quest");
        accept.Clicked += async (_, _) => await FinishAsync(programId);
        return Stack(Title("Your first quest"), cards, Small("How often"), days, notice, accept,
            Note("Sessions take 30–40 minutes. You can switch quests on the Quest tab any time."));
    }

    private async Task FinishAsync(string programId)
    {
        if (_preview)
        {
            await DisplayAlertAsync("Preview", $"In a real first run this would start {programId} and save your choices. Nothing was changed.", "OK");
            _done();
            return;
        }
        try
        {
            UserSettings.WeightUnit = _unit;
            UserSettings.HeroRace = _race;
            UserSettings.HeroClass = _class;
            var inventory = _services.GetRequiredService<IInventoryService>();
            await inventory.InitializeAsync();
            await inventory.SetFromFlagsAsync(_gear);
            await _services.GetRequiredService<IProgramService>().StartProgramAsync(programId, DateOnly.FromDateTime(DateTime.Today));
            UserSettings.OnboardingDone = true;
            _done();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't start", ex.Message, "OK");
        }
    }

    // ------------------------------------------------------------------ building blocks

    private static string Label((string Id, string Label)[] options, string id) => options.First(o => o.Id == id).Label;

    private View Stack(params View[] children)
    {
        var s = new VerticalStackLayout { Spacing = 14 };
        foreach (var c in children) s.Add(c);
        return s;
    }

    private View Title(string text) => new Label { Text = text, FontFamily = "PixelUI", FontSize = 32, TextColor = _accent };
    private View Body(string text) => new Label { Text = text, FontFamily = "PixelUI", FontSize = 21, TextColor = _onBg };
    private View Small(string text) => new Label { Text = text, FontFamily = "PixelUI", FontSize = 20, TextColor = _muted };
    private View Note(string text) => new Label { Text = text, FontSize = 13, TextColor = _muted };

    private Button Primary(string text) => new()
    {
        Text = text, HeightRequest = 64, FontFamily = "PixelUI", FontSize = 26, BackgroundColor = _accent, TextColor = _bg
    };

    private Button Choice(string text, bool on) => new()
    {
        Text = text, HeightRequest = 56, FontFamily = "PixelUI", FontSize = 22,
        BackgroundColor = on ? _accent : _surface, TextColor = on ? _bg : _onBg
    };

    private View Chips((string Id, string Label)[] options, Func<string> selected, Action<string> pick)
    {
        var flex = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        foreach (var (id, label) in options)
        {
            var on = id == selected();
            var b = new Button
            {
                Text = label, FontFamily = "PixelUI", FontSize = 19, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(12, 2), HeightRequest = 44,
                BackgroundColor = on ? _accent : _surface, TextColor = on ? _bg : _onBg
            };
            b.Clicked += (_, _) => pick(id);
            flex.Add(b);
        }
        return flex;
    }
}
