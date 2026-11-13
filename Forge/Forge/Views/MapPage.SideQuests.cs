using Forge.Models;
using Forge.Services;
using Forge.Training;
using Microsoft.Maui.Layouts;

namespace Forge.Views;

/// <summary>
/// Side quests on the map (user, 2026-11-13): "I trained my own way". Three hours of HEMA or a gym
/// class can fill today's stone (at most twice a week) and wears its activity's icon; a yoga
/// practice or a hike can be the day's active rest. Rest days follow each person's style: a guided
/// flow in the workout screen, or freestyle, which just logs your own practice.
/// </summary>
public sealed partial class MapPage
{
    private const string GlyphHike = "\uf6ec", GlyphRun = "\uf70c", GlyphSwim = "\uf5c4", GlyphBike = "\uf84a", GlyphSpa = "\uf5bb",
        GlyphMountain = "\uf6fc", GlyphBall = "\uf1e3", GlyphMusic = "\uf001", GlyphFist = "\uf6de", GlyphUsers = "\uf0c0", GlyphStar = "\uf005";

    /// <summary>The icon a side quest's stone wears: crossed swords for HEMA (a sprite), else a glyph.</summary>
    private View SideQuestIcon(string activity, Color colour, double size = 30)
    {
        var icon = SideQuests.Icon(activity);
        if (icon == "sword")
            return new Image { Source = "map_icon_sword.png", WidthRequest = size * 1.4, HeightRequest = size * 1.4 };
        return new Label
        {
            FontFamily = "FA", FontSize = size, TextColor = colour,
            Text = icon switch
            {
                "yoga" => GlyphSpa, "hike" => GlyphHike, "run" => GlyphRun, "climb" => GlyphMountain, "swim" => GlyphSwim,
                "bike" => GlyphBike, "dance" => GlyphMusic, "fist" => GlyphFist, "class" => GlyphUsers, "sport" => GlyphBall,
                _ => GlyphStar
            }
        };
    }

    private static string SideQuestLine(SideQuestRow q)
        => $"{q.Activity} · {SideQuests.Duration(q.Minutes)} · {(Effort)q.Effort}" + (q.Note is { } n ? $"\n{n}" : "");

    /// <summary>Log a side quest. <paramref name="prefer"/> presets what it counts as (from a rest day: rest).</summary>
    private async void ShowSideQuestSheet(SideQuestCounts? prefer = null)
    {
        var recent = await _vm.SideQuestActivitiesAsync();
        var canStandIn = await _vm.SideQuestCanStandInAsync();

        string? activity = recent.FirstOrDefault()?.Name;
        var minutes = recent.FirstOrDefault()?.Minutes ?? 60;
        var effort = recent.FirstOrDefault()?.Effort ?? Effort.Moderate;
        SideQuestCounts? chosen = prefer;

        var body = new VerticalStackLayout { Spacing = 10 };
        body.Children.Add(Heading("Side quest"));
        body.Children.Add(Caption("Trained your own way? Log it: it keeps your streak, and a big one can stand in for today's workout."));

        var activities = new FlexLayout { Wrap = FlexWrap.Wrap, JustifyContent = FlexJustify.Start };
        var other = new Entry { Placeholder = "What did you do?", FontFamily = "Body", FontSize = 18, IsVisible = false, TextColor = _onBg };
        var durations = new FlexLayout { Wrap = FlexWrap.Wrap, JustifyContent = FlexJustify.Start };
        var efforts = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 8 };
        var counts = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 8 };
        var countsNote = Caption("");
        var note = new Entry { Placeholder = "Note (optional)", FontFamily = "Body", FontSize = 17, TextColor = _onBg };
        var activityButtons = new List<(string Name, Button B)>();
        var durationButtons = new List<(int Minutes, Button B)>();
        var effortButtons = new List<(Effort E, Button B)>();
        var countButtons = new List<(SideQuestCounts C, Button B)>();
        Button? otherButton = null;

        SideQuestCounts Counts() => !canStandIn ? SideQuestCounts.Rest : chosen ?? SideQuests.Suggest(minutes, effort);

        void Refresh()
        {
            foreach (var (name, b) in activityButtons) Chip(b, !other.IsVisible && name == activity);
            if (otherButton is not null) Chip(otherButton, other.IsVisible);
            foreach (var (m, b) in durationButtons) Chip(b, m == minutes);
            foreach (var (e, b) in effortButtons) Chip(b, e == effort);
            var c = Counts();
            foreach (var (k, b) in countButtons)
            {
                Chip(b, k == c);
                b.Opacity = k == SideQuestCounts.Training && !canStandIn ? 0.4 : 1;
            }
            countsNote.Text = !canStandIn
                ? "Counts as a rest day: today's training is done, or two side quests already stood in this week."
                : c == SideQuestCounts.Training
                    ? "Fills today's stone instead of the quest workout (up to two a week)."
                    : "Counts as today's active rest: keeps your streak.";
        }

        // What: your own activities first, then a few to start from.
        var names = recent.Select(r => r.Name).Concat(SideQuests.Starters)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(9).ToList();
        foreach (var name in names)
        {
            var b = ChipButton(name);
            b.Clicked += (_, _) =>
            {
                activity = name;
                other.IsVisible = false;
                if (recent.FirstOrDefault(r => r.Name == name) is { } last) { minutes = last.Minutes; effort = last.Effort; }
                Refresh();
            };
            activityButtons.Add((name, b));
            activities.Children.Add(b);
        }
        otherButton = ChipButton("＋ Other");
        otherButton.Clicked += (_, _) => { other.IsVisible = true; other.Focus(); Refresh(); };
        activities.Children.Add(otherButton);

        foreach (var m in new[] { 30, 45, 60, 90, 120, 180 })
        {
            var b = ChipButton(SideQuests.Duration(m));
            b.Clicked += (_, _) => { minutes = m; Refresh(); };
            durationButtons.Add((m, b));
            durations.Children.Add(b);
        }
        var col = 0;
        foreach (var e in Enum.GetValues<Effort>())
        {
            var b = ChipButton(e.ToString().ToUpperInvariant(), display: true);
            b.Clicked += (_, _) => { effort = e; Refresh(); };
            effortButtons.Add((e, b));
            efforts.Add(b, col++);
        }
        col = 0;
        foreach (var (k, label) in new[] { (SideQuestCounts.Training, "TRAINING DAY"), (SideQuestCounts.Rest, "REST DAY") })
        {
            var b = ChipButton(label, display: true);
            b.Clicked += (_, _) => { if (k == SideQuestCounts.Training && !canStandIn) return; chosen = k; Refresh(); };
            countButtons.Add((k, b));
            counts.Add(b, col++);
        }

        body.Children.Add(Section("WHAT"));
        body.Children.Add(activities);
        body.Children.Add(other);
        body.Children.Add(Section("HOW LONG"));
        body.Children.Add(durations);
        body.Children.Add(Section("HOW HARD"));
        body.Children.Add(efforts);
        body.Children.Add(Section("COUNTS AS"));
        body.Children.Add(counts);
        body.Children.Add(countsNote);
        body.Children.Add(note);
        body.Children.Add(Primary("LOG IT", async () =>
        {
            var what = other.IsVisible ? other.Text?.Trim() : activity;
            if (string.IsNullOrWhiteSpace(what)) { await ToastAsync("What did you do?"); return; }
            CloseSheet();
            await _vm.LogSideQuestAsync(what, minutes, effort, Counts(), note.Text);
            await ToastAsync($"{what}: logged");
        }));
        Refresh();
        OpenSheet(new ScrollView { Content = body, MaximumHeightRequest = 640 });
    }

    private Label Section(string text) => new()
    {
        Text = text, FontFamily = "Display", FontSize = 18, TextColor = _accent, Margin = new Thickness(0, 6, 0, 0)
    };

    private Button ChipButton(string text, bool display = false) => new()
    {
        Text = text, FontFamily = display ? "Display" : "Body", FontSize = display ? 20 : 17, HeightRequest = 46,
        Padding = new Thickness(14, 0), Margin = display ? new Thickness(0) : new Thickness(0, 0, 8, 8),
        CornerRadius = 23, BorderWidth = 2, BorderColor = _accent, BackgroundColor = Colors.Transparent, TextColor = _onBg
    };

    private void Chip(Button b, bool on)
    {
        b.BackgroundColor = on ? _accent : Colors.Transparent;
        b.TextColor = on ? _bg : _onBg;
    }

    /// <summary>A rest day, as each person likes it (Settings): a guided flow, or your own practice.</summary>
    private void ShowRestSheet()
    {
        var body = new VerticalStackLayout { Spacing = 12 };
        var logged = _vm.TodaySideQuest is { CountsAs: (int)SideQuestCounts.Rest } q ? q : null;
        if (_vm.ActiveRestDone)
        {
            body.Children.Add(Heading("Rest done ✓"));
            body.Children.Add(Caption(logged is null ? "Your streak, and the ×1.2 XP bonus, carry on." : SideQuestLine(logged)));
            body.Children.Add(Secondary("UNDO", async () =>
            {
                CloseSheet();
                if (logged is not null) await _vm.UndoSideQuestAsync();
                else await _vm.ToggleRestAsync();
            }));
            OpenSheet(body);
            return;
        }

        if (UserSettings.ActiveRestStyle == ActiveRestStyle.Freestyle)
        {
            body.Children.Add(Heading("Rest day"));
            body.Children.Add(Caption("Your practice, your way: yoga, a walk, a stretch. Log it to keep your streak and the ×1.2 XP bonus."));
            body.Children.Add(Primary("LOG MY PRACTICE", () => ShowSideQuestSheet(SideQuestCounts.Rest)));
            body.Children.Add(Link("Just mark it done", async () => { CloseSheet(); await _vm.ToggleRestAsync(); }));
        }
        else
        {
            body.Children.Add(Heading("Active rest"));
            body.Children.Add(Caption("A guided mobility flow, about ten minutes. Follow along: the timer runs it, and every move says how."));
            var moves = new VerticalStackLayout { Spacing = 4 };
            foreach (var m in _vm.ActiveRestMoves) moves.Children.Add(new Label { Text = m, FontFamily = "Body", FontSize = 17, TextColor = _onBg });
            body.Children.Add(moves);
            body.Children.Add(Primary("START", async () => { CloseSheet(); await _vm.StartActiveRestAsync(); }));
            body.Children.Add(Link("I did my own thing", () => ShowSideQuestSheet(SideQuestCounts.Rest)));
            body.Children.Add(Link("Just mark it done", async () => { CloseSheet(); await _vm.ToggleRestAsync(); }));
        }
        OpenSheet(body);
    }
}
