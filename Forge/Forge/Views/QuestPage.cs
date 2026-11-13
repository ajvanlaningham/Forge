using Forge.ViewModels;
using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;
using Forge.Views.SubPages;
using Microsoft.Maui.Controls.Shapes;

namespace Forge.Views;

/// <summary>
/// The quest board (plan phase 4, 2026-11-13; pushed from the map, no longer a tab). Every quest is
/// a rolled scroll on the wooden board: tap to unroll it. Your quest hangs at the top under a wax
/// seal and opens to its days, the forge meter and "restart"; below a clear divider the other
/// quests open to their description, a version picker and one ACCEPT, confirmed inline.
/// </summary>
public sealed class QuestPage : ContentPage
{
    // The board's palette (ink on parchment passes AA: Ink 10:1, InkSoft 5.6:1, Wax 5.1:1).
    private static readonly Color Parchment = Color.FromArgb("#D9C9A3");
    private static readonly Color ParchmentShade = Color.FromArgb("#B8A37A");
    private static readonly Color Ink = Color.FromArgb("#2B1D14");
    private static readonly Color InkSoft = Color.FromArgb("#5A4330");
    private static readonly Color Wax = Color.FromArgb("#8E2A2A");
    private static readonly Color[] RodRows =
        { Color.FromArgb("#000000"), Color.FromArgb("#B07E52"), Color.FromArgb("#9A6B44"), Color.FromArgb("#80583A"), Color.FromArgb("#6A472E"), Color.FromArgb("#523622"), Color.FromArgb("#000000") };

    private readonly IProgramService _programs;
    private readonly ITrainingService _training;
    private Func<string, MoveTraits> _traits = _ => default;
    private readonly VerticalStackLayout _body = new() { Padding = new Thickness(14, 18, 14, 40), Spacing = 22 };
    private Equipment _homeGear;

    public QuestPage(IProgramService programs, ITrainingService training)
    {
        _programs = programs;
        _training = training;
        base.Title = "Quests";
        BackgroundColor = (Color)Application.Current!.Resources["Bg"];
        Content = new Grid
        {
            Children =
            {
                new Image { Source = "board_wood.png", Aspect = Aspect.AspectFill, Opacity = 0.9 },
                new ScrollView { Content = _body }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await LoadAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Quest board load failed: {ex}"); }
    }

    private async Task LoadAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var catalog = await _programs.GetProgramsAsync();
        _traits = await GuidedSupport.TraitsAsync(_training);
        var view = await _programs.GetViewAsync(today);
        var active = view?.Program.Definition;
        _homeGear = await _training.GetEquipmentAsync(TrainingLocation.Home);

        _body.Children.Clear();
        if (active is not null && view is not null)
        {
            _body.Children.Add(BoardLabel("YOUR QUEST"));
            _body.Children.Add(await ActiveScrollAsync(view, catalog.Where(p => p.Quest == active.Quest).ToList()));
        }

        _body.Children.Add(BoardLabel(active is null ? "CHOOSE A QUEST" : "OTHER QUESTS"));
        foreach (var quest in catalog.Where(p => p.Quest != active?.Quest).GroupBy(p => p.Quest))
            _body.Children.Add(OtherScroll(quest.ToList()));
    }

    // --- scrolls --------------------------------------------------------------------------------

    /// <summary>A scroll: rods top and bottom, parchment between. The rolled part shows the header; tap unrolls.</summary>
    private View Scroll(View header, Func<View> details, bool sealed_ = false, bool open = false)
    {
        var paper = new VerticalStackLayout { Spacing = 12 };
        paper.Children.Add(header);
        View? unrolled = null;
        var hint = Hint(open ? "Tap to roll up" : "Tap to unroll");
        paper.Children.Add(hint);
        void Toggle()
        {
            if (unrolled is null)
            {
                unrolled = details();
                paper.Children.Insert(paper.Children.Count - 1, unrolled);
                unrolled.Opacity = 0;
                _ = unrolled.FadeToAsync(1, 220);
                hint.Text = "Tap to roll up";
            }
            else
            {
                paper.Children.Remove(unrolled);
                unrolled = null;
                hint.Text = "Tap to unroll";
            }
        }
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => Toggle();
        header.GestureRecognizers.Add(tap);
        hint.GestureRecognizers.Add(tap);
        if (open) Toggle();

        var sheet = new Border
        {
            BackgroundColor = Parchment, Stroke = sealed_ ? Wax : ParchmentShade, StrokeThickness = sealed_ ? 3 : 2,
            StrokeShape = new Rectangle(), Padding = new Thickness(20, 16), Margin = new Thickness(14, -4),
            Content = paper
        };
        return new VerticalStackLayout { Spacing = 0, Children = { Rod(), sheet, Rod() } };
    }

    /// <summary>The wooden rod a scroll rolls onto: turned knobs at both ends.</summary>
    private static View Rod()
    {
        var bar = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center };
        foreach (var c in RodRows) bar.Children.Add(new BoxView { Color = c, HeightRequest = 4 });
        var rod = new Grid { HeightRequest = 40, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        rod.Add(new Image { Source = "scroll_knob.png", WidthRequest = 32, HeightRequest = 40 }, 0);
        rod.Add(bar, 1);
        rod.Add(new Image { Source = "scroll_knob_r.png", WidthRequest = 32, HeightRequest = 40 }, 2);
        return rod;
    }

    private async Task<View> ActiveScrollAsync(BlockView view, List<ProgramDefinition> versions)
    {
        var def = view.Program.Definition;
        var done = await _programs.CompletedDaysInBlockAsync(view.Program.BlockId);
        var needed = Forging.Needed(def.TrainingDaysPerWeek);
        var total = Forging.TotalDays(def.TrainingDaysPerWeek);

        var seal = new Border
        {
            BackgroundColor = Wax, StrokeThickness = 0, Padding = new Thickness(10, 4), HorizontalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Content = new Label { Text = "ACTIVE", FontFamily = "Display", FontSize = 18, TextColor = Parchment }
        };
        var header = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                seal,
                ScrollTitle(def.Name),
                Text(def.Subtitle, InkSoft),
                Text(view.BlockComplete
                    ? "Complete: ready for the forge"
                    : $"Week {view.Week} of {ProgramDefinition.WeeksPerBlock} · {ProgramDefinition.WeekName(view.Week)} · {def.VersionName}", Ink, bold: true),
                new ProgressBar { Progress = Math.Min(1, done / (double)needed), ProgressColor = Wax, BackgroundColor = ParchmentShade, HeightRequest = 12 },
                Text($"{done} of {needed} training days to earn a forging ({total} in all)", InkSoft, size: 16)
            }
        };

        View Details()
        {
            var stack = new VerticalStackLayout { Spacing = 14 };
            if (def.Description is { } d) stack.Children.Add(Text(d, Ink));
            var days = new VerticalStackLayout { Spacing = 12 };
            stack.Children.Add(days);
            _ = FillDaysAsync(days, def, view.Week);
            if (versions.Count > 1) stack.Children.Add(VersionPicker(versions, def, "SWITCH VERSION"));
            var restart = Button("RESTART THIS QUEST", primary: false);
            restart.Clicked += async (_, _) =>
            {
                if (!await ConfirmAsync(restart, "Start this quest again at week 1, today? History is kept.")) return;
                await _programs.StartProgramAsync(def.Id, DateOnly.FromDateTime(DateTime.Today));
                await Shell.Current.GoToAsync("..");
            };
            stack.Children.Add(restart);
            var library = Button("EXERCISE LIBRARY", primary: false);
            library.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(ExerciseLibraryPage));
            stack.Children.Add(library);
            return stack;
        }

        return Scroll(header, Details, sealed_: true);
    }

    private async Task FillDaysAsync(VerticalStackLayout days, ProgramDefinition def, int week)
    {
        for (var i = 0; i < def.Days.Count; i++)
        {
            var day = new VerticalStackLayout { Spacing = 2 };
            day.Children.Add(new Label { Text = $"{i + 1} · {def.Days[i].Name}", FontFamily = "Display", FontSize = 24, TextColor = Ink });
            try
            {
                var plan = await _training.PlanForDayAsync(i, week, UserSettings.LastLocation);
                foreach (var e in plan.Exercises)
                    day.Children.Add(Text($"{e.Name}  {e.Slot.Sets} × {(e.Slot.IsTimed ? $"{e.Slot.Seconds}s" : e.Slot.RepsMin == e.Slot.RepsMax ? $"{e.Slot.RepsMin}" : $"{e.Slot.RepsMin}–{e.Slot.RepsMax}")}",
                        e.IsMain ? Ink : InkSoft, size: 16));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Board plan failed: {ex.Message}"); }
            days.Children.Add(day);
        }
    }

    private View OtherScroll(List<ProgramDefinition> versions)
    {
        var first = versions[0];
        var days = versions.Select(p => p.TrainingDaysPerWeek).Distinct().OrderBy(d => d);
        var header = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                ScrollTitle(first.Name),
                Text(first.Subtitle, InkSoft),
                Text($"{string.Join(" or ", days)} days a week · {DayTiming.Range(first, _traits)} min · {Discipline(first)}", Wax, size: 16)
            }
        };

        View Details()
        {
            var stack = new VerticalStackLayout { Spacing = 14 };
            if (first.Description is { } d) stack.Children.Add(Text(d, Ink));
            stack.Children.Add(VersionPicker(versions, versions.FirstOrDefault(p => Fits(p)) ?? first, "ACCEPT QUEST"));
            return stack;
        }

        return Scroll(header, Details);
    }

    private static string Discipline(ProgramDefinition p) => p.Discipline switch
    {
        "Anvil" => "Strength",
        "Bellows" => "Conditioning",
        _ => "Balanced"
    };

    /// <summary>Version rows (radio style, gear fit marked) and one action, confirmed inline.</summary>
    private View VersionPicker(List<ProgramDefinition> versions, ProgramDefinition initial, string action)
    {
        var chosen = initial;
        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(new Label { Text = "VERSION", FontFamily = "Display", FontSize = 20, TextColor = InkSoft });
        var rows = new List<(ProgramDefinition P, Border B)>();
        void Mark()
        {
            foreach (var (p, b) in rows)
            {
                b.Stroke = p == chosen ? Wax : ParchmentShade;
                b.StrokeThickness = p == chosen ? 3 : 2;
                b.BackgroundColor = p == chosen ? Color.FromArgb("#E6D9B8") : Colors.Transparent;
            }
        }
        foreach (var v in versions.OrderBy(p => p.Implement).ThenBy(p => p.TrainingDaysPerWeek))
        {
            var label = new Label
            {
                Text = v.VersionName + (Fits(v) ? "   ✓ fits your gear" : ""), FontFamily = "Body", FontSize = 17, TextColor = Ink
            };
            var b = new Border { StrokeShape = new RoundRectangle { CornerRadius = 8 }, Padding = new Thickness(14, 12), Content = label };
            var version = v;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => { chosen = version; Mark(); };
            b.GestureRecognizers.Add(tap);
            rows.Add((v, b));
            stack.Children.Add(b);
        }
        Mark();

        var go = Button(action, primary: true);
        go.Clicked += async (_, _) =>
        {
            var current = (await _programs.GetActiveAsync())?.Definition;
            if (current?.Id == chosen.Id) { await Shell.Current.GoToAsync(".."); return; }
            var note = current is null
                ? $"Start {chosen.Name} ({chosen.VersionName}) at week 1, today?"
                : $"Start {chosen.Name} ({chosen.VersionName}) at week 1, today? {current.Name} ends; your history is kept.";
            if (!await ConfirmAsync(go, note)) return;
            await _programs.StartProgramAsync(chosen.Id, DateOnly.FromDateTime(DateTime.Today));
            await Shell.Current.GoToAsync("..");
        };
        stack.Children.Add(go);
        return stack;
    }

    /// <summary>
    /// Inline confirmation (no alert dialog): the first tap shows what will happen and turns the
    /// button into CONFIRM; the second tap goes ahead.
    /// </summary>
    private static async Task<bool> ConfirmAsync(Button button, string note)
    {
        if (button.CommandParameter is "armed") return true;
        button.CommandParameter = "armed";
        var original = button.Text;
        button.Text = "CONFIRM";
        if (button.Parent is Layout layout)
        {
            var label = new Label { Text = note, FontFamily = "Body", FontSize = 17, TextColor = Wax };
            layout.Children.Insert(layout.Children.IndexOf(button), label);
            await Task.Delay(8000);
            layout.Children.Remove(label);
        }
        button.CommandParameter = null;
        button.Text = original;
        return false;
    }

    private bool Fits(ProgramDefinition p)
        => p.Equipment.All(name => !Enum.TryParse<Equipment>(name, ignoreCase: true, out var e) || _homeGear.HasFlag(e));

    // --- small builders -------------------------------------------------------------------------

    private static View BoardLabel(string text) => new Border
    {
        BackgroundColor = Color.FromArgb("#CC101418"), StrokeThickness = 0, Padding = new Thickness(14, 6), HorizontalOptions = LayoutOptions.Start,
        StrokeShape = new RoundRectangle { CornerRadius = 6 },
        Content = new Label { Text = text, FontFamily = "Display", FontSize = 22, TextColor = (Color)Application.Current!.Resources["Accent"] }
    };

    private static Label ScrollTitle(string text) => new() { Text = text, FontFamily = "FantasyH", FontSize = 30, TextColor = Ink };

    private static Label Text(string text, Color colour, bool bold = false, double size = 17)
        => new() { Text = text, FontFamily = bold ? "BodyBold" : "Body", FontSize = size, TextColor = colour };

    private static Label Hint(string text) => new()
    {
        Text = text, FontFamily = "Body", FontSize = 15, TextColor = InkSoft, HorizontalTextAlignment = TextAlignment.Center
    };

    private static Button Button(string text, bool primary) => new()
    {
        Text = text, FontFamily = "Display", FontSize = primary ? 26 : 22, HeightRequest = primary ? 60 : 52, CornerRadius = 12,
        BackgroundColor = primary ? Wax : Colors.Transparent, TextColor = primary ? Parchment : Ink,
        BorderColor = primary ? Wax : InkSoft, BorderWidth = 2
    };
}
