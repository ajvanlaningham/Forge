using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;
using Forge.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace Forge.Views;

/// <summary>
/// Home (plan phase 4, 2026-11-13; replaces Today): the quest drawn as a winding path, Duolingo-
/// style. Each week is a stretch in its own colour under a sticky banner; training days are big
/// stones, the week ends in a chest (opened by a perfect week) and the quest at the anvil. One
/// thing glows: today's stone, whose card has one START. Details live behind taps: the stone's
/// card, the banner's book (every day's exercises) and the ＋ sheet (cardio, open session,
/// weigh-in, change quest). Logic stays in <see cref="TodayViewModel"/>.
/// </summary>
public sealed class MapPage : ContentPage
{
    // The four weeks' colours, matching art/gen_map.py's stones (cream glyphs pass 4.5:1 on each).
    private static readonly Color[] WeekColours =
        { Color.FromArgb("#B8462A"), Color.FromArgb("#3F7A3A"), Color.FromArgb("#2A6496"), Color.FromArgb("#8A6A12") };
    private static readonly Color Cream = Color.FromArgb("#FFF8E7");
    private const string GlyphCheck = "", GlyphDumbbell = "", GlyphWalk = "", GlyphHeart = "",
        GlyphBook = "", GlyphFire = "", GlyphPlus = "", GlyphCampfire = "";

    private readonly TodayViewModel _vm;
    private readonly Color _accent, _muted, _onBg, _bg, _surface;

    private readonly Image _hero = new() { WidthRequest = 52, HeightRequest = 52, Aspect = Aspect.AspectFit };
    private readonly Label _level = new() { FontFamily = "Display", FontSize = 26 };
    private readonly ProgressBar _xp = new() { HeightRequest = 10 };
    private readonly Label _streak = new() { FontFamily = "Display", FontSize = 26, VerticalOptions = LayoutOptions.Center };
    private readonly Label _streakFlame = new() { FontFamily = "FA", Text = GlyphFire, FontSize = 24, VerticalOptions = LayoutOptions.Center };

    private readonly Border _banner = new() { Padding = new Thickness(18, 10), StrokeThickness = 0 };
    private readonly Label _bannerWeek = new() { FontFamily = "Display", FontSize = 26, TextColor = Cream };
    private readonly Label _bannerQuest = new() { FontFamily = "Body", FontSize = 16, TextColor = Cream };

    private readonly ScrollView _scroll = new();
    private readonly VerticalStackLayout _path = new() { Padding = new Thickness(0, TopPad, 0, 40), Spacing = 0 };
    private readonly GraphicsView _trailView = new() { InputTransparent = true };
    private readonly TrailDrawable _trail = new();
    private readonly Image _ribbonLeft = new() { WidthRequest = 36, HeightRequest = 72, Aspect = Aspect.Fill };
    private readonly Image _ribbonRight = new() { WidthRequest = 36, HeightRequest = 72, Aspect = Aspect.Fill };
    private const double TopPad = 16, DividerHeight = 64, Swing = 88;
    // Fantasy-map dressing: the map is warmer than the app's slate, and each week has its scenery.
    private static readonly Color MapGround = Color.FromArgb("#15130F");
    private static readonly string[][] Scenery =
    {
        new[] { "map_embers.png", "map_pine.png" },
        new[] { "map_pine.png", "map_peak.png" },
        new[] { "map_waves.png", "map_pine.png" },
        new[] { "map_peak.png", "map_pine.png" }
    };
    private double _y;
    private readonly List<(int Week, View Section)> _sections = new();
    private View? _currentView;

    private readonly Grid _overlay = new() { IsVisible = false };
    private readonly Border _sheet = new() { Padding = new Thickness(22, 20, 22, 28), StrokeThickness = 0, VerticalOptions = LayoutOptions.End };
    private readonly Label _toast = new() { FontFamily = "Display", FontSize = 24, Opacity = 0, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 0, 24), Padding = new Thickness(18, 8) };
    private bool _forging;
    private View? _bannerRow;

    public MapPage(TodayViewModel vm)
    {
        _vm = vm;
        Title = "Map";
        var res = Application.Current!.Resources;
        _accent = (Color)res["Accent"];
        _muted = (Color)res["Muted"];
        _onBg = (Color)res["OnBg"];
        _bg = (Color)res["Bg"];
        _surface = (Color)res["Surface"];
        BackgroundColor = _bg;
        TabChrome.AddSettingsCog(this);

        _level.TextColor = _accent;
        _xp.ProgressColor = (Color)res["Aether"];
        _xp.BackgroundColor = Color.FromArgb("#2A3140");
        _toast.TextColor = _bg;
        _toast.BackgroundColor = _accent;

        // Header: hero · level · streak · ＋
        var levelStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, Children = { _level, _xp } };
        var streak = new HorizontalStackLayout { Spacing = 6, Children = { _streakFlame, _streak } };
        var plus = RoundIcon(GlyphPlus, ShowMoreSheet);
        var header = new Grid
        {
            Padding = new Thickness(16, 8), ColumnSpacing = 14,
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) }
        };
        var heroTap = new TapGestureRecognizer();
        heroTap.Tapped += async (_, _) => await Shell.Current.GoToAsync("//hero");
        _hero.GestureRecognizers.Add(heroTap);
        header.Add(_hero, 0);
        header.Add(levelStack, 1);
        header.Add(streak, 2);
        header.Add(plus, 3);

        // Banner: this stretch of the path, with the book (every day's exercises).
        var book = RoundIcon(GlyphBook, ShowQuestDrawer, Cream, Colors.Transparent);
        var bannerGrid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        bannerGrid.Add(new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center, Children = { _bannerWeek, _bannerQuest } }, 0);
        bannerGrid.Add(book, 1);
        _banner.Content = bannerGrid;
        _banner.StrokeShape = new Rectangle();
        _banner.HeightRequest = 72;
        var ribbon = new Grid
        {
            Margin = new Thickness(6, 4, 6, 0), ColumnSpacing = -1,
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 4), Radius = 0, Opacity = 0.35f }
        };
        ribbon.Add(_ribbonLeft, 0);
        ribbon.Add(_banner, 1);
        ribbon.Add(_ribbonRight, 2);

        _trailView.Drawable = _trail;
        _scroll.Content = new Grid { Children = { _trailView, _path } };
        _scroll.BackgroundColor = MapGround;
        _scroll.Scrolled += (_, e) => UpdateBanner(e.ScrollY);

        // The sheet: a card rising from the bottom over a dimmed map; tap outside to close.
        var dim = new BoxView { Color = Colors.Black, Opacity = 0.55 };
        var dimTap = new TapGestureRecognizer();
        dimTap.Tapped += (_, _) => CloseSheet();
        dim.GestureRecognizers.Add(dimTap);
        _sheet.BackgroundColor = _surface;
        _sheet.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) };
        _overlay.Add(dim);
        _overlay.Add(_sheet);

        var root = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) } };
        root.Add(header, 0, 0);
        root.Add(ribbon, 0, 1);
        _bannerRow = ribbon;
        root.Add(_scroll, 0, 2);
        root.Add(_toast, 0, 2);
        root.Add(_overlay, 0, 0);
        Grid.SetRowSpan(_overlay, 3);
        Content = root;

        _vm.Refreshed += (_, _) => MainThread.BeginInvokeOnMainThread(Rebuild);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.XpAwarded -= OnXpAwarded;
        _vm.XpAwarded += OnXpAwarded;
        try { await _vm.RefreshAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Map load failed: {ex}"); }
        await CheckLevelUpAsync();
        await ShowForgingIfDueAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.XpAwarded -= OnXpAwarded;
        CloseSheet();
    }

    private async void OnXpAwarded(object? sender, int xp)
    {
        await ToastAsync($"+{xp} XP");
        await CheckLevelUpAsync();
    }

    // --- building -------------------------------------------------------------------------------

    private void Rebuild()
    {
        _hero.Source = _vm.SpriteFile;
        _level.Text = $"{_vm.LevelText} · {_vm.XpText}";
        _xp.Progress = _vm.XpProgress;
        var bonus = _vm.Multiplier > 1.0;
        _streak.Text = bonus ? $"{_vm.StreakDays} ×{_vm.Multiplier:0.0}" : $"{_vm.StreakDays}";
        _streak.TextColor = _streakFlame.TextColor = _vm.StreakDays > 0 ? _accent : _muted;

        _path.Children.Clear();
        _sections.Clear();
        _currentView = null;
        _trail.Points.Clear();
        _y = TopPad;

        var quest = _vm.Quest;
        if (quest is null || _vm.Nodes.Count == 0)
        {
            _bannerRow!.IsVisible = false;
            _path.Children.Add(Centered(new Label { Text = "No quest yet", FontFamily = "Display", FontSize = 30, TextColor = _onBg, HorizontalTextAlignment = TextAlignment.Center }));
            _path.Children.Add(Centered(Caption("Pick a four-week plan to put a path on this map.")));
            _path.Children.Add(Padded(Primary("CHOOSE A QUEST", async () => await Shell.Current.GoToAsync(nameof(QuestPage)))));
            return;
        }

        _bannerRow!.IsVisible = true;
        // A compass rose where the trail begins.
        _path.Children.Add(new Image { Source = "map_compass.png", WidthRequest = 72, HeightRequest = 72, HorizontalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 28, -40), Opacity = 0.85 });
        _y += 72 - 40;
        var index = 0;
        for (var week = 1; week <= ProgramDefinition.WeeksPerBlock; week++)
        {
            var section = new VerticalStackLayout { Spacing = 0 };
            if (week > 1)
            {
                section.Children.Add(Divider($"WEEK {week} · {ProgramDefinition.WeekName(week).ToUpperInvariant()}", WeekColours[week - 1]));
                _y += DividerHeight;
            }
            var weekNodes = _vm.Nodes.Where(n => n.Week == week && n.Kind != MapNodeKind.Anvil).ToList();
            var campfireAfter = CampfireSlot(weekNodes);
            foreach (var node in weekNodes)
            {
                section.Children.Add(NodeRow(node, index++));
                if (ReferenceEquals(node, campfireAfter)) section.Children.Add(CampfireRow(index++));
            }
            _path.Children.Add(section);
            _sections.Add((week, section));
        }
        _path.Children.Add(NodeRow(_vm.Nodes[^1], index));
        _trailView.HeightRequest = _y + 40;
        _trailView.Invalidate();
        if (_vm.BlockComplete)
            _path.Children.Add(Padded(Primary("CHOOSE YOUR NEXT QUEST", async () => await Shell.Current.GoToAsync(nameof(QuestPage)))));

        UpdateBanner(0);
        _ = ScrollToCurrentAsync();
    }

    /// <summary>
    /// On a day with no training to offer (the week's training is done, and none was done today),
    /// today is a campfire: active rest, placed after the week's last done day.
    /// </summary>
    private MapNode? CampfireSlot(List<MapNode> weekNodes)
    {
        if (_vm.BlockComplete || _vm.TrainingDoneToday || _vm.InProgressTitle is not null) return null;
        if (weekNodes.Count == 0 || weekNodes[0].Week != _vm.CurrentWeek) return null;
        if (weekNodes.Any(n => n.State == MapNodeState.Current)) return null;
        return weekNodes.LastOrDefault(n => n.Kind == MapNodeKind.Training && n.State == MapNodeState.Done);
    }

    private async Task ScrollToCurrentAsync()
    {
        await Task.Delay(120);
        if (_currentView is not null)
            await _scroll.ScrollToAsync(_currentView, ScrollToPosition.Center, false);
    }

    private void UpdateBanner(double scrollY)
    {
        if (_vm.Quest is null) return;
        // Before layout every section sits at 0: show the week we're in until positions are known.
        var laidOut = _sections.Count > 1 && _sections[^1].Section.Y > 0;
        var week = laidOut ? 1 : Math.Clamp(_vm.CurrentWeek, 1, ProgramDefinition.WeeksPerBlock);
        if (laidOut)
            foreach (var (w, section) in _sections)
                if (section.Y <= scrollY + 40) week = w;
        _banner.BackgroundColor = WeekColours[week - 1];
        _ribbonLeft.Source = $"map_ribbon_w{week}_l.png";
        _ribbonRight.Source = $"map_ribbon_w{week}_r.png";
        _bannerWeek.Text = $"WEEK {week} · {ProgramDefinition.WeekName(week).ToUpperInvariant()}";
        _bannerQuest.Text = _vm.Quest.Name;
    }

    private View NodeRow(MapNode node, int index)
    {
        var x = QuestMap.Wind(index) * Swing;
        var isCurrent = node.State == MapNodeState.Current;
        var height = isCurrent ? 150.0 : node.Kind == MapNodeKind.Anvil ? 140 : 118;
        var row = new Grid { HeightRequest = height };
        var visualHeight = node.Kind switch { MapNodeKind.Training => 92.0, MapNodeKind.Chest => 58, _ => 110 };
        _trail.Points.Add(new PointF((float)x, (float)(_y + height - visualHeight / 2)));
        _y += height;
        if (!isCurrent && index % 2 == 1) AddScenery(row, node.Week, x, index);
        View visual = node.Kind switch
        {
            MapNodeKind.Training => Stone(node),
            MapNodeKind.Chest => new Image
            {
                Source = node.State == MapNodeState.Done ? "map_chest_open.png" : "map_chest.png",
                WidthRequest = 84, HeightRequest = 58, Opacity = node.State == MapNodeState.Passed ? 0.45 : node.State == MapNodeState.Ahead ? 0.8 : 1
            },
            _ => new Image { Source = "forge_0.png", WidthRequest = 110, HeightRequest = 110, Opacity = node.State == MapNodeState.Done ? 1 : 0.7 }
        };
        visual.HorizontalOptions = LayoutOptions.Center;
        visual.VerticalOptions = LayoutOptions.End;
        visual.TranslationX = x;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => ShowNodeSheet(node);
        visual.GestureRecognizers.Add(tap);
        row.Add(visual);

        if (isCurrent)
        {
            var bubble = new Border
            {
                BackgroundColor = _onBg, StrokeThickness = 0, Padding = new Thickness(14, 6),
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start, TranslationX = x,
                Content = new Label { Text = _vm.InProgressTitle is null ? "START" : "RESUME", FontFamily = "Display", FontSize = 22, TextColor = WeekColours[node.Week - 1] }
            };
            bubble.GestureRecognizers.Add(tap);
            row.Add(bubble);
            // The hero waits beside today's stone.
            row.Add(new Image
            {
                Source = _vm.SpriteFile, WidthRequest = 64, HeightRequest = 64, HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End, TranslationX = x + (x > 0 ? -120 : 120), TranslationY = -6
            });
            _currentView = row;
            _ = PulseAsync(visual);
        }
        else if (node.State == MapNodeState.Done && node.DoneOn == DateOnly.FromDateTime(DateTime.Today))
        {
            _currentView = row;
        }
        return row;
    }

    private View CampfireRow(int index)
    {
        var x = QuestMap.Wind(index) * Swing;
        _trail.Points.Add(new PointF((float)x, (float)(_y + 60)));
        _y += 120;
        var done = _vm.ActiveRestDone;
        var fire = new Image { Source = "map_campfire.png", WidthRequest = 80, HeightRequest = 60 };
        var label = new Label
        {
            Text = done ? "REST DONE ✓" : "REST DAY", FontFamily = "Display", FontSize = 20,
            TextColor = done ? _muted : _accent, HorizontalTextAlignment = TextAlignment.Center
        };
        var stack = new VerticalStackLayout
        {
            Spacing = 2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, TranslationX = x,
            Children = { fire, label }
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => ShowRestSheet();
        stack.GestureRecognizers.Add(tap);
        var row = new Grid { HeightRequest = 120 };
        row.Add(stack);
        if (!done) _currentView = row;
        return row;
    }

    /// <summary>A bit of the week's landscape on the far side of the trail.</summary>
    private static void AddScenery(Grid row, int week, double x, int index)
    {
        var set = Scenery[Math.Clamp(week, 1, 4) - 1];
        var side = x > 10 ? -1 : 1;
        row.Add(new Image
        {
            Source = set[index / 2 % set.Length], WidthRequest = 80, HeightRequest = 70, Opacity = 0.95,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            TranslationX = side * (Swing + 70) * 0.9
        });
    }

    private View Stone(MapNode node)
    {
        var sprite = node.State switch
        {
            MapNodeState.Done or MapNodeState.Current => $"map_stone_w{node.Week}.png",
            MapNodeState.Passed => "map_stone_passed.png",
            _ => "map_stone_ahead.png"
        };
        var glyph = node.State == MapNodeState.Done ? GlyphCheck : DayGlyph(node.DayIndex ?? 0);
        var grid = new Grid { WidthRequest = 104, HeightRequest = 92 };
        grid.Add(new Image { Source = sprite, WidthRequest = 104, HeightRequest = 92, Aspect = Aspect.AspectFit });
        grid.Add(new Label
        {
            Text = glyph, FontFamily = "FA", FontSize = 30, TranslationY = -10,
            TextColor = node.State is MapNodeState.Done or MapNodeState.Current ? Cream : Color.FromArgb("#9AA3AF"),
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        });
        if (node.State == MapNodeState.Passed) grid.Opacity = 0.6;
        return grid;
    }

    private string DayGlyph(int dayIndex)
    {
        if (_vm.Quest is not { } q) return GlyphDumbbell;
        if (q.Discipline == "Bellows") return GlyphHeart;
        var name = q.Days[Math.Clamp(dayIndex, 0, q.Days.Count - 1)].Name;
        return name.Contains("Lower", StringComparison.OrdinalIgnoreCase) ? GlyphWalk : GlyphDumbbell;
    }

    private static async Task PulseAsync(View v)
    {
        // A slow breath on today's stone, until the page rebuilds.
        try
        {
            while (v.Parent is not null)
            {
                await v.ScaleToAsync(1.07, 700, Easing.SinInOut);
                await v.ScaleToAsync(1.0, 700, Easing.SinInOut);
            }
        }
        catch { /* view torn down */ }
    }

    // --- sheets ---------------------------------------------------------------------------------

    private void ShowNodeSheet(MapNode node)
    {
        var q = _vm.Quest;
        if (q is null) return;
        var body = new VerticalStackLayout { Spacing = 14 };
        var week = $"Week {node.Week} · {ProgramDefinition.WeekName(node.Week)}";
        switch (node.Kind)
        {
            case MapNodeKind.Training:
            {
                var day = q.Resolve(node.DayIndex ?? 0, node.Week);
                body.Children.Add(Heading(day.Name + (day.IsLight ? " (light)" : "")));
                var meta = $"{week} · {day.Slots.Count} exercises · {q.Minutes ?? "?"} min";
                if (node.State == MapNodeState.Current)
                {
                    body.Children.Add(Caption(meta));
                    if (_vm.HasTest) body.Children.Add(Pill($"Then: {_vm.TestTitle}", _accent));
                    body.Children.Add(Primary(_vm.InProgressTitle is null ? "START" : "RESUME", async () => { CloseSheet(); await _vm.StartTodayAsync(); }));
                    if (_vm.InProgressTitle is null)
                        body.Children.Add(Link("Rest day instead", ShowRestSheet));
                }
                else if (node.State == MapNodeState.Done)
                {
                    body.Children.Add(Pill($"Done {node.DoneOn:dddd, MMM d}", Color.FromArgb("#5FB97E")));
                    body.Children.Add(Caption(week));
                }
                else if (node.State == MapNodeState.Passed)
                    body.Children.Add(Caption("Missed, and that's fine: every week starts fresh."));
                else
                    body.Children.Add(Caption(meta));
                _ = AddExercisesAsync(body, node.DayIndex ?? 0, node.Week);
                break;
            }
            case MapNodeKind.Chest:
                body.Children.Add(Heading(node.State == MapNodeState.Done ? "Perfect week!" : $"{ProgramDefinition.WeekName(node.Week)} chest"));
                body.Children.Add(Caption(node.State switch
                {
                    MapNodeState.Done => "Every day moved and every training day done. The chest is open.",
                    MapNodeState.Passed => "This one stayed shut. The next chest is waiting.",
                    _ => "Move every day this week, training or active rest, and finish its training days to open it."
                }));
                break;
            default:
            {
                var total = Forging.TotalDays(q.TrainingDaysPerWeek);
                var needed = Forging.Needed(q.TrainingDaysPerWeek);
                var done = _vm.Nodes.Count(n => n.Kind == MapNodeKind.Training && n.State == MapNodeState.Done);
                body.Children.Add(Heading("The forge"));
                body.Children.Add(Caption($"Finish the quest with {needed} of {total} training days to earn a forging: raise any piece of your gear one metal."));
                body.Children.Add(new ProgressBar { Progress = Math.Min(1, done / (double)needed), ProgressColor = _accent, HeightRequest = 12 });
                body.Children.Add(Caption($"{done} of {needed} so far."));
                break;
            }
        }
        if (node.State != MapNodeState.Current) body.Children.Add(Secondary("CLOSE", CloseSheet));
        OpenSheet(body);
    }

    /// <summary>The day's exercises as they'll be done (swaps applied), appended once planned.</summary>
    private async Task AddExercisesAsync(VerticalStackLayout body, int dayIndex, int week)
    {
        try
        {
            var plan = await _vm.PlanDayAsync(dayIndex, week);
            var list = new VerticalStackLayout { Spacing = 4 };
            foreach (var e in plan.Exercises)
                list.Children.Add(new Label
                {
                    Text = $"{e.Name}  {SetsText(e.Slot)}", FontFamily = "Body", FontSize = 17,
                    TextColor = e.IsMain ? _onBg : _muted
                });
            // After the heading and its first line, so the action stays at the bottom.
            body.Children.Insert(Math.Min(2, body.Children.Count), list);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Map plan failed: {ex.Message}"); }
    }

    private static string SetsText(ProgramSlot s)
        => $"{s.Sets} × " + (s.IsTimed ? $"{s.Seconds}s" : s.RepsMin == s.RepsMax ? $"{s.RepsMin}" : $"{s.RepsMin}–{s.RepsMax}");

    private void ShowRestSheet()
    {
        var body = new VerticalStackLayout { Spacing = 12 };
        body.Children.Add(Heading(_vm.ActiveRestDone ? "Rest done ✓" : "Active rest"));
        body.Children.Add(Caption("A gentle flow, about ten minutes. It keeps your streak, and the ×1.2 XP bonus, alive."));
        var moves = new VerticalStackLayout { Spacing = 4 };
        foreach (var m in _vm.ActiveRestMoves) moves.Children.Add(new Label { Text = m, FontFamily = "Body", FontSize = 17, TextColor = _onBg });
        if (_vm.ActiveRestAlternatives.Count > 0)
            moves.Children.Add(new Label { Text = "Or: " + string.Join(" · ", _vm.ActiveRestAlternatives), FontFamily = "Body", FontSize = 16, TextColor = _muted });
        body.Children.Add(moves);
        if (_vm.ActiveRestDone)
            body.Children.Add(Secondary("UNDO", async () => { CloseSheet(); await _vm.ToggleRestAsync(); }));
        else
            body.Children.Add(Primary("MARK DONE", async () => { CloseSheet(); await _vm.ToggleRestAsync(); }));
        OpenSheet(body);
    }

    private void ShowMoreSheet()
    {
        var body = new VerticalStackLayout { Spacing = 12 };
        body.Children.Add(Heading("More"));
        if (_vm.HasTest) body.Children.Add(Primary(_vm.TestTitle.ToUpperInvariant(), async () => { CloseSheet(); await _vm.TakeTestAsync(); }));
        body.Children.Add(Secondary("LOG CARDIO", ShowCardioSheet));
        body.Children.Add(Secondary("WEIGH IN", async () => { CloseSheet(); await Shell.Current.GoToAsync(nameof(CheckInPage)); }));
        if (_vm.CanStartOpen) body.Children.Add(Secondary("OPEN SESSION", async () => { CloseSheet(); await _vm.StartOpenSessionAsync(); }));
        body.Children.Add(Secondary("CHANGE QUEST", async () => { CloseSheet(); await Shell.Current.GoToAsync(nameof(QuestPage)); }));
        OpenSheet(body);
    }

    private void ShowCardioSheet()
    {
        var c = _vm.Conditioning;
        var body = new VerticalStackLayout { Spacing = 12 };
        body.Children.Add(Heading("Cardio this week"));
        body.Children.Add(new Label { Text = $"{c.Minutes} / {c.GoalMinutes} min", FontFamily = "Display", FontSize = 40, TextColor = _accent });
        body.Children.Add(new ProgressBar { Progress = c.GoalMinutes == 0 ? 0 : Math.Min(1, c.Minutes / (double)c.GoalMinutes), ProgressColor = _accent, HeightRequest = 12 });
        body.Children.Add(Caption("Bike, bag rounds, brisk walks. Add what you did:"));
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 8 };
        var col = 0;
        foreach (var m in new[] { 10, 20, 30, 45 })
        {
            var minutes = m;
            grid.Add(Secondary($"+{m}", async () => { await _vm.AddCardioAsync(minutes); await ToastAsync($"+{minutes} min"); ShowCardioSheet(); }), col++);
        }
        body.Children.Add(grid);
        body.Children.Add(Secondary("DONE", CloseSheet));
        OpenSheet(body);
    }

    private async void ShowQuestDrawer()
    {
        var q = _vm.Quest;
        if (q is null) return;
        var body = new VerticalStackLayout { Spacing = 14 };
        body.Children.Add(new Label { Text = q.Name, FontFamily = "FantasyH", FontSize = 30, TextColor = _accent });
        body.Children.Add(Caption($"{q.Subtitle}\n{q.VersionName} · {q.Minutes ?? "?"} min"));
        var scroll = new ScrollView { MaximumHeightRequest = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density * 0.38 };
        var days = new VerticalStackLayout { Spacing = 16 };
        scroll.Content = days;
        body.Children.Add(scroll);
        body.Children.Add(Secondary("CHANGE QUEST", async () => { CloseSheet(); await Shell.Current.GoToAsync(nameof(QuestPage)); }));
        body.Children.Add(Secondary("CLOSE", CloseSheet));
        OpenSheet(body);

        for (var i = 0; i < q.Days.Count; i++)
        {
            var day = new VerticalStackLayout { Spacing = 4 };
            day.Children.Add(new Label { Text = $"{i + 1} · {q.Days[i].Name}", FontFamily = "Display", FontSize = 24, TextColor = _onBg });
            try
            {
                var plan = await _vm.PlanDayAsync(i, _vm.CurrentWeek);
                foreach (var e in plan.Exercises)
                    day.Children.Add(new Label { Text = $"{e.Name}  {SetsText(e.Slot)}", FontFamily = "Body", FontSize = 17, TextColor = e.IsMain ? _onBg : _muted });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Drawer plan failed: {ex.Message}"); }
            days.Children.Add(day);
        }
    }

    private void OpenSheet(View body)
    {
        _sheet.Content = body;
        _overlay.IsVisible = true;
        _sheet.TranslationY = 400;
        _ = _sheet.TranslateToAsync(0, 0, 200, Easing.CubicOut);
    }

    private void CloseSheet() => _overlay.IsVisible = false;

    protected override bool OnBackButtonPressed()
    {
        if (!_overlay.IsVisible) return base.OnBackButtonPressed();
        CloseSheet();
        return true;
    }

    private async Task ToastAsync(string text)
    {
        _toast.Text = text;
        await _toast.FadeToAsync(1, 150);
        await Task.Delay(1400);
        await _toast.FadeToAsync(0, 300);
    }

    // --- small builders -------------------------------------------------------------------------

    private Label Heading(string text) => new() { Text = text, FontFamily = "Display", FontSize = 32, TextColor = _onBg };
    private Label Caption(string text) => new() { Text = text, FontFamily = "Body", FontSize = 17, TextColor = _muted };
    private static View Centered(View v) { v.HorizontalOptions = LayoutOptions.Center; v.Margin = new Thickness(24, 8); return v; }
    private static View Padded(View v) { v.Margin = new Thickness(20, 16); return v; }

    private Border Pill(string text, Color colour) => new()
    {
        StrokeThickness = 2, Stroke = colour, Padding = new Thickness(12, 6), HorizontalOptions = LayoutOptions.Start,
        StrokeShape = new RoundRectangle { CornerRadius = 12 },
        Content = new Label { Text = text, FontFamily = "Body", FontSize = 17, TextColor = colour }
    };

    private View Divider(string text, Color colour) => new Border
    {
        StrokeThickness = 0, BackgroundColor = Colors.Transparent, Padding = new Thickness(0, 18, 0, 6), HeightRequest = DividerHeight,
        Content = new Label { Text = text, FontFamily = "Display", FontSize = 22, TextColor = Color.FromRgba(colour.Red * 0.5 + 0.5, colour.Green * 0.5 + 0.5, colour.Blue * 0.5 + 0.5, 1), HorizontalTextAlignment = TextAlignment.Center }
    };

    private Button Primary(string text, Func<Task> act)
    {
        var b = new Button { Text = text, Style = (Style)Application.Current!.Resources["PrimaryButton"] };
        b.Clicked += async (_, _) => await act();
        return b;
    }

    private Button Primary(string text, Action act) => Primary(text, () => { act(); return Task.CompletedTask; });

    private Button Secondary(string text, Func<Task> act)
    {
        var b = new Button { Text = text, Style = (Style)Application.Current!.Resources["SecondaryButton"] };
        b.Clicked += async (_, _) => await act();
        return b;
    }

    private Button Secondary(string text, Action act) => Secondary(text, () => { act(); return Task.CompletedTask; });

    private Button Link(string text, Action act)
    {
        var b = new Button { Text = text, BackgroundColor = Colors.Transparent, BorderWidth = 0, TextColor = _muted, FontFamily = "Body", FontSize = 17, HeightRequest = 48 };
        b.Clicked += (_, _) => act();
        return b;
    }

    private View RoundIcon(string glyph, Action act, Color? fg = null, Color? bg = null)
    {
        var label = new Label
        {
            Text = glyph, FontFamily = "FA", FontSize = 22, TextColor = fg ?? _onBg,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };
        var border = new Border
        {
            WidthRequest = 48, HeightRequest = 48, StrokeThickness = 2, Stroke = fg ?? _muted, BackgroundColor = bg ?? Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 24 }, Content = label, VerticalOptions = LayoutOptions.Center
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => act();
        border.GestureRecognizers.Add(tap);
        return border;
    }

    // --- end-of-quest and level-up moments (moved from Today) ---------------------------------

    /// <summary>A block whose four weeks are up is judged here, and its forging moment shown once.</summary>
    private async Task ShowForgingIfDueAsync()
    {
        if (_forging) return;
        var services = IPlatformApplication.Current?.Services;
        if (services?.GetService<IForgeService>() is not { } forge || services.GetService<IProgramService>() is not { } programs) return;
        try
        {
            _forging = true;
            if (await forge.JudgeIfDueAsync(DateOnly.FromDateTime(DateTime.Today)) is { } result)
            {
                await forge.MarkSeenAsync(result.Id);
                await Navigation.PushModalAsync(new ForgingPage(result, programs, forge));
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Forge judgment failed: {ex}"); }
        finally { _forging = false; }
    }

    private static async Task CheckLevelUpAsync()
    {
        var progress = IPlatformApplication.Current?.Services.GetService<ProgressService>();
        if (progress is not null) await progress.CheckLevelUpAsync();
    }
}

/// <summary>The dashed ink trail between the map's stops, like a route on a treasure map.</summary>
internal sealed class TrailDrawable : IDrawable
{
    /// <summary>x is the offset from the centre line; y is from the top of the path.</summary>
    public List<PointF> Points { get; } = new();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Points.Count < 2) return;
        var mid = dirtyRect.Width / 2;
        canvas.StrokeColor = Color.FromArgb("#5A4A33");
        canvas.StrokeSize = 7;
        canvas.StrokeLineCap = LineCap.Butt;
        canvas.StrokeDashPattern = new float[] { 1.6f, 1.4f };
        var path = new PathF();
        path.MoveTo(mid + Points[0].X, Points[0].Y);
        for (var i = 1; i < Points.Count; i++)
        {
            // A gentle curve between stops instead of straight segments.
            var a = Points[i - 1];
            var b = Points[i];
            var my = (a.Y + b.Y) / 2;
            path.CurveTo(mid + a.X, my, mid + b.X, my, mid + b.X, b.Y);
        }
        canvas.DrawPath(path);
    }
}
