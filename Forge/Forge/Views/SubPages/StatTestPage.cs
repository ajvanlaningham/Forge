using System.Globalization;
using Forge.Constants;
using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Views.SubPages;

/// <summary>
/// A stat test, protocol v2 (Decision 23, docs/baseline-tests.md). STR: one all-out set on three
/// test lifts plus push-ups to failure. DEX: knee-to-wall, sit-and-reach and shoulder reach with a
/// tape measure. CON: the 3-minute step test with a timed pulse count. A practice run is saved but
/// not scored.
/// </summary>
[QueryProperty(nameof(KindName), "kind")]
[QueryProperty(nameof(CalibrationText), "calibration")]
public sealed class StatTestPage : ContentPage
{
    private readonly IStatTestService _tests;
    private readonly ITrainingService _training;
    private readonly IWeightService _weights;
    private bool _built;

    public string KindName { get; set; } = nameof(StatKind.Strength);
    public string CalibrationText { get; set; } = "false";
    private StatKind Kind => Enum.TryParse<StatKind>(KindName, out var k) ? k : StatKind.Strength;
    private bool Calibration => bool.TryParse(CalibrationText, out var c) && c;

    private readonly Color _accent = (Color)Application.Current!.Resources["Accent"];
    private readonly Color _surface = (Color)Application.Current.Resources["Surface"];
    private readonly Color _muted = (Color)Application.Current.Resources["Muted"];
    private readonly Color _onBg = (Color)Application.Current.Resources["OnBg"];
    private readonly Color _bg = (Color)Application.Current.Resources["Bg"];

    private readonly VerticalStackLayout _body = new() { Padding = new Thickness(16, 12, 16, 28), Spacing = 14 };
    private readonly WeightUnit _unit = UserSettings.WeightUnit;
    private NumberField? _bodyweight;
    private Func<Task>? _save;
    private bool _away;                      // left the page: stop the step-test loop and its buzzing

    public StatTestPage(IStatTestService tests, ITrainingService training, IWeightService weights)
    {
        _tests = tests;
        _training = training;
        _weights = weights;
        Content = new ScrollView { Content = _body };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _away = true;
        DeviceDisplay.Current.KeepScreenOn = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _away = false;
        if (_built) return;
        _built = true;
        Title = Kind switch
        {
            StatKind.Strength => "Strength test",
            StatKind.Dexterity => "Mobility test",
            _ => "Conditioning test"
        };
        if (Calibration)
            _body.Add(Note("Practice run: saved, but not scored. It teaches you the test, so the real baseline in Honing week is honest. First tries always undersell."));
        try
        {
            switch (Kind)
            {
                case StatKind.Strength: await BuildStrengthAsync(); break;
                case StatKind.Dexterity: BuildMobility(); break;
                default: BuildConditioning(); break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Stat test build failed: {ex}");
            _body.Add(Note("Couldn't set up this test."));
            return;
        }
        var save = new Button
        {
            Text = Calibration ? "Save practice run" : "Save result", HeightRequest = 60, FontFamily = "PixelUI", FontSize = 24,
            BackgroundColor = _accent, TextColor = _bg, Margin = new Thickness(0, 8, 0, 0)
        };
        save.Clicked += async (_, _) => { if (_save is not null) await _save(); };
        _body.Add(save);
    }

    // ------------------------------------------------------------------ STR

    private async Task BuildStrengthAsync()
    {
        await AddBodyweightAsync();
        var lifts = UserSettings.TestLifts;
        var pickers = new List<Picker>();
        if (lifts is null || lifts.Length != 3)
        {
            var lib = await _training.GetLibraryAsync();
            var owned = await _training.GetEquipmentAsync(TrainingLocation.Home);
            bool Loaded(ExerciseInfo e) => e.Modality is Modality.Dumbbell or Modality.Kettlebell or Modality.Barbell;
            bool Doable(ExerciseInfo e) => ExerciseSwap.IsAvailable(e, owned);
            var groups = new[]
            {
                ("Squat or lunge", lib.Where(e => Loaded(e) && Doable(e) && e.Movement is MovementPattern.Squat or MovementPattern.Lunge)),
                ("Press", lib.Where(e => Loaded(e) && Doable(e) && e.Movement == MovementPattern.Push)),
                ("Row", lib.Where(e => Loaded(e) && Doable(e) && e.Movement == MovementPattern.Pull)),
            };
            _body.Add(Header("Your three test lifts"));
            _body.Add(Note("Chosen once and kept for every test, so results compare. Changing one later starts a new baseline for that lift."));
            foreach (var (label, options) in groups)
            {
                var names = options.Select(e => e.Name).Distinct().OrderBy(n => n).ToList();
                var picker = new Picker { Title = label, ItemsSource = names, FontSize = 18, TextColor = _onBg };
                var preferred = new[] { "Goblet Squat", "Kettlebell Front Squat", "Bulgarian Split Squat", "Dumbbell Floor Press", "Kettlebell Press", "Bent-over Row", "Kettlebell Row" };
                picker.SelectedIndex = Math.Max(0, names.FindIndex(n => preferred.Contains(n)));
                pickers.Add(picker);
                _body.Add(new VerticalStackLayout { Spacing = 2, Children = { Small(label), picker } });
            }
            lifts = pickers.Select(p => p.SelectedItem as string ?? "").ToArray();
        }

        _body.Add(Header("One all-out set each"));
        _body.Add(Note("After your warm-ups, pick a weight you can lift 3 to 10 times and do one set to the last clean rep. Rest fully between lifts. Dumbbells: the weight of one."));
        var gear = UserSettings.GearAt(TrainingLocation.Home);
        var rows = new List<(Func<string> Name, NumberField Load, NumberField Reps)>();
        var inProgress = await _training.GetInProgressAsync();
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            Func<string> name = () => pickers.Count == 3 ? pickers[index].SelectedItem as string ?? "" : lifts[index];
            var info = (await _training.GetLibraryAsync()).FirstOrDefault(e => e.Name == name());
            var loads = gear.For(info?.Modality ?? Modality.Dumbbell, _unit, info?.NeedsPair ?? false);
            // Prefer what you just lifted in today's session, then your last finished session.
            var today = inProgress?.Exercises.FirstOrDefault(e => e.Row.ExerciseName == name())?.Sets.Where(x => !x.IsWarmup).Select(x => (double?)x.LoadLb).Max();
            var history = await _training.GetRecentWorkingSetsAsync(name());
            var start = today ?? history.FirstOrDefault(h => h.Count > 0)?.Max(s => s.LoadLb) ?? loads.Next(0) ?? 0;
            var load = new NumberField(WeightMath.ToDisplay(start, _unit), step: 0, min: 0, max: 1000, unit: WeightMath.UnitLabel(_unit), loads: loads, unitOf: _unit);
            var reps = new NumberField(8, step: 1, min: 0, max: 40, unit: "reps");
            var title = new Label { Text = name(), FontFamily = "PixelUI", FontSize = 24, TextColor = _accent };
            if (pickers.Count == 3) pickers[index].SelectedIndexChanged += (_, _) => title.Text = name();
            _body.Add(Card(title, Row("Weight", load), Row("Reps", reps)));
            rows.Add((name, load, reps));
        }

        _body.Add(Header("Push-ups to failure"));
        var incline = UserSettings.PushupIncline;
        Picker? inclinePicker = null;
        if (incline is null)
        {
            inclinePicker = new Picker { Title = "Where", ItemsSource = new[] { "Floor", "Hands on a box or step", "Hands on a wall" }, SelectedIndex = 1, FontSize = 18, TextColor = _onBg };
            _body.Add(Note("Pick the version you can do at least a few of. It's kept for every test."));
            _body.Add(inclinePicker);
        }
        else _body.Add(Note($"Same as always: {incline}."));
        _body.Add(Note("Body straight, chest to the surface, arms fully straight at the top. Stop when form breaks."));
        var pushups = new NumberField(8, step: 1, min: 0, max: 100, unit: "reps");
        _body.Add(Row("Push-ups", pushups));

        _save = async () =>
        {
            var bw = BodyweightLb();
            if (bw <= 0) { await DisplayAlert("Bodyweight", "Enter your bodyweight: strength is scored relative to it.", "OK"); return; }
            var names = rows.Select(r => r.Name()).ToArray();
            if (UserSettings.TestLifts is null) UserSettings.TestLifts = names;
            var where = incline ?? inclinePicker?.SelectedItem as string ?? "Floor";
            UserSettings.PushupIncline ??= where;
            var measures = new Dictionary<string, double>();
            var detail = new Dictionary<string, string>();
            foreach (var r in rows)
            {
                var lb = WeightMath.ToStoredPounds(r.Load.Value, _unit);
                measures[$"lift:{r.Name()}"] = StatMeasures.RelativeStrength(lb, (int)r.Reps.Value, bw);
                detail[r.Name()] = $"{r.Load.Value.ToString("0.#", CultureInfo.CurrentCulture)} {WeightMath.UnitLabel(_unit)} × {r.Reps.Value:0}";
            }
            if (pushups.Value > 0) measures[$"pushups:{where}"] = pushups.Value;
            detail["Push-ups"] = $"{pushups.Value:0} ({where})";
            await SaveAsync(measures, Array.Empty<string>(), detail, bw);
        };
    }

    // ------------------------------------------------------------------ DEX

    private void BuildMobility()
    {
        _body.Add(Note("You need a wall, a tape measure, a step or box, and a ruler. Warm up first. Take the best of two tries on each side."));

        _body.Add(Header("1 · Knee to wall (ankle)"));
        _body.Add(Note("Face a wall, foot flat, toes pointing at it. Bend the knee to touch the wall without the heel lifting. Slide the foot back until the knee only just touches. Measure from the big toe to the wall."));
        var kneeL = new NumberField(8, step: 0.5, min: 0, max: 30, unit: "cm");
        var kneeR = new NumberField(8, step: 0.5, min: 0, max: 30, unit: "cm");
        _body.Add(Card(Row("Left", kneeL), Row("Right", kneeR)));

        _body.Add(Header("2 · Sit and reach (hamstrings)"));
        _body.Add(Note("Sit with one leg straight, its sole flat against a step or box, the other knee bent. Lay a ruler on the step with the 23 cm mark at its edge, numbers growing away from you. Reach slowly along it with both hands, hold for 2 seconds, read the ruler."));
        var reachL = new NumberField(20, step: 0.5, min: 0, max: 60, unit: "cm");
        var reachR = new NumberField(20, step: 0.5, min: 0, max: 60, unit: "cm");
        _body.Add(Card(Row("Left leg", reachL), Row("Right leg", reachR)));

        _body.Add(Header("3 · Shoulder reach"));
        _body.Add(Note("Make fists. One hand reaches over the shoulder down your back, the other up behind your back. Reach toward each other in one smooth move. Measure the gap between the knuckles; if they overlap, enter the overlap as a negative number. 'Left' means the left hand is on top."));
        var shL = new NumberField(15, step: 0.5, min: -30, max: 60, unit: "cm");
        var shR = new NumberField(15, step: 0.5, min: -30, max: 60, unit: "cm");
        _body.Add(Card(Row("Left on top", shL), Row("Right on top", shR)));

        _save = async () =>
        {
            var measures = new Dictionary<string, double>
            {
                ["kneeToWall"] = StatMeasures.BothSides(kneeL.Value, kneeR.Value),
                ["sitReach"] = StatMeasures.BothSides(reachL.Value, reachR.Value),
                ["shoulder"] = StatMeasures.ShoulderReach(StatMeasures.BothSides(shL.Value, shR.Value))
            };
            var detail = new Dictionary<string, string>
            {
                ["Knee to wall"] = $"L {kneeL.Value:0.#} · R {kneeR.Value:0.#} cm",
                ["Sit and reach"] = $"L {reachL.Value:0.#} · R {reachR.Value:0.#} cm",
                ["Shoulder gap"] = $"L {shL.Value:0.#} · R {shR.Value:0.#} cm"
            };
            await SaveAsync(measures, Array.Empty<string>(), detail, await LatestBodyweightLbAsync());
        };
    }

    // ------------------------------------------------------------------ CON

    private const int StepSeconds = 180;
    private const int BeatMs = 625;            // 96 beats a minute: up, up, down, down = 24 steps a minute

    private void BuildConditioning()
    {
        _body.Add(Note("The 3-minute step test: step up and down in time with the beat for 3 minutes, then sit and count your pulse for one minute. It's submaximal; keep to the beat, don't race it."));

        _body.Add(Header("Your step"));
        var known = UserSettings.StepHeightCm;
        var step = new NumberField(known ?? 30, step: 1, min: 10, max: 50, unit: "cm");
        if (known is null)
        {
            _body.Add(Note("Use the same step every time: a sturdy box, a low bench or the bottom stair (about 18 cm). Measure its height."));
            _body.Add(Row("Step height", step));
        }
        else _body.Add(Note($"Same step as always: {known:0} cm."));

        _body.Add(Header("Resting pulse (optional)"));
        _body.Add(Note("If you know your resting heart rate (first thing in the morning, or from a watch), enter it. It gives the result context."));
        var resting = new NumberField(0, step: 1, min: 0, max: 140, unit: "bpm");
        _body.Add(Row("Resting", resting));

        _body.Add(Header("The test"));
        var cue = new Label { Text = "Ready", FontFamily = "PixelH", FontSize = 34, TextColor = _accent, HorizontalTextAlignment = TextAlignment.Center };
        var clock = new Label { Text = "3:00", FontFamily = "GymNum", FontSize = 64, TextColor = _onBg, HorizontalTextAlignment = TextAlignment.Center };
        var hint = new Label { Text = "Up with one foot, up with the other, down, down. One step per beat.", FontFamily = "PixelUI", FontSize = 19, TextColor = _muted, HorizontalTextAlignment = TextAlignment.Center };
        var start = new Button { Text = "Start the 3 minutes", HeightRequest = 60, FontFamily = "PixelUI", FontSize = 22 };
        var pulse = new NumberField(100, step: 1, min: 30, max: 220, unit: "beats");
        var pulseRow = Row("Pulse in 1 minute", pulse);
        pulseRow.IsVisible = false;
        _body.Add(Card(cue, clock, hint, start, pulseRow));

        var running = false;
        start.Clicked += async (_, _) =>
        {
            if (running) return;
            running = true;
            start.IsEnabled = false;
            DeviceDisplay.Current.KeepScreenOn = true;
            try
            {
                // 3 minutes on the beat
                string[] words = { "UP", "UP", "DOWN", "DOWN" };
                var end = DateTime.UtcNow.AddSeconds(StepSeconds);
                var beat = 0;
                while (DateTime.UtcNow < end && !_away)
                {
                    cue.Text = words[beat % 4];
                    var left = (int)Math.Ceiling((end - DateTime.UtcNow).TotalSeconds);
                    clock.Text = $"{left / 60}:{left % 60:00}";
                    Buzz(beat % 4 == 0 ? 40 : 15);
                    beat++;
                    await Task.Delay(BeatMs);
                }
                if (_away) { cue.Text = "Ready"; clock.Text = "3:00"; start.IsEnabled = true; return; }
                // stop, sit, find the pulse
                Buzz(400);
                cue.Text = "STOP · SIT";
                hint.Text = "Sit down and find your pulse: two fingers on the wrist below the thumb, or the side of the neck.";
                for (var s = 5; s > 0; s--) { clock.Text = $"0:0{s}"; await Task.Delay(1000); }
                // a full minute of counting
                Buzz(200);
                cue.Text = "COUNT";
                hint.Text = "Count every beat until the buzz.";
                var countEnd = DateTime.UtcNow.AddSeconds(60);
                while (DateTime.UtcNow < countEnd && !_away)
                {
                    var left = (int)Math.Ceiling((countEnd - DateTime.UtcNow).TotalSeconds);
                    clock.Text = $"0:{left:00}";
                    await Task.Delay(250);
                }
                Buzz(600);
                cue.Text = "DONE";
                clock.Text = "";
                hint.Text = "Enter the number of beats you counted.";
                pulseRow.IsVisible = true;
            }
            finally
            {
                DeviceDisplay.Current.KeepScreenOn = false;
                running = false;
            }
        };

        _save = async () =>
        {
            if (!pulseRow.IsVisible)
            {
                if (!await DisplayAlert("Not finished", "Run the 3 minutes and the pulse count first, or enter a pulse you counted another way?", "Enter it", "Cancel")) return;
                pulseRow.IsVisible = true;
                return;
            }
            UserSettings.StepHeightCm ??= step.Value;
            var detail = new Dictionary<string, string>
            {
                ["Recovery pulse"] = $"{pulse.Value:0} bpm",
                ["Step"] = $"{(UserSettings.StepHeightCm ?? step.Value):0} cm"
            };
            if (resting.Value > 0) detail["Resting pulse"] = $"{resting.Value:0} bpm";
            await SaveAsync(new Dictionary<string, double> { ["recoveryHr"] = pulse.Value }, new[] { "recoveryHr" }, detail, await LatestBodyweightLbAsync());
        };
    }

    // ------------------------------------------------------------------ saving

    private async Task SaveAsync(Dictionary<string, double> measures, IReadOnlyCollection<string> smaller, Dictionary<string, string> detail, double bodyweightLb)
    {
        var outcome = await _tests.RecordAsync(Kind, Calibration, measures, smaller, detail, bodyweightLb);
        var stat = Kind switch { StatKind.Strength => "STR", StatKind.Dexterity => "DEX", _ => "CON" };
        var message = outcome.Calibration ? "Practice run saved. The real baseline comes in Honing week."
            : outcome.IsBaseline ? $"Baseline set: {stat} {outcome.Score}. Every later test is measured against today."
            : $"{stat} {outcome.Score} ({(outcome.Change >= 0 ? "+" : "")}{outcome.Change}).";
        await DisplayAlert(Title, message, "OK");
        await Navigation.PopAsync();
    }

    private async Task AddBodyweightAsync()
    {
        var latest = await _weights.GetLatestAsync();
        var shown = latest is null ? 0 : WeightMath.ToDisplay((double)latest.Pounds, _unit);
        _bodyweight = new NumberField(Math.Round(shown, 1), step: 0.5, min: 0, max: 700, unit: WeightMath.UnitLabel(_unit));
        _body.Add(Header("Bodyweight today"));
        _body.Add(Note(latest is null ? "Strength is scored relative to bodyweight. Enter today's." : $"From your last weigh-in ({latest.Date:MMM d}). Adjust if it's changed."));
        _body.Add(Row("Bodyweight", _bodyweight));
    }

    private double BodyweightLb() => _bodyweight is null ? 0 : WeightMath.ToStoredPounds(_bodyweight.Value, _unit);

    private async Task<double> LatestBodyweightLbAsync() => (double)((await _weights.GetLatestAsync())?.Pounds ?? 0);

    // ------------------------------------------------------------------ building blocks

    private View Header(string text) => new Label { Text = text, FontFamily = "PixelUI", FontSize = 26, TextColor = _accent, Margin = new Thickness(0, 6, 0, 0) };
    private View Note(string text) => new Label { Text = text, FontSize = 14, TextColor = _muted };
    private View Small(string text) => new Label { Text = text, FontFamily = "PixelUI", FontSize = 18, TextColor = _muted };

    private Grid Row(string label, View field)
    {
        var g = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
        g.Add(new Label { Text = label, FontFamily = "PixelUI", FontSize = 21, TextColor = _onBg, VerticalOptions = LayoutOptions.Center });
        g.Add(field, 1);
        return g;
    }

    private View Card(params View[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        foreach (var c in children) stack.Add(c);
        return new Border
        {
            BackgroundColor = _surface, Stroke = _muted, StrokeThickness = 1, Padding = 12,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }, Content = stack
        };
    }

    private static void Buzz(int ms)
    {
        try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(ms)); } catch { /* no vibrator */ }
    }

    /// <summary>A number with − / + steppers and tap-to-type. With a LoadSet, ± steps through owned weights.</summary>
    private sealed class NumberField : HorizontalStackLayout
    {
        private readonly Entry _entry;
        private readonly double _step, _min, _max;
        private readonly LoadSet? _loads;
        private readonly WeightUnit _unitOf;

        public double Value { get; private set; }

        public NumberField(double value, double step, double min, double max, string unit, LoadSet? loads = null, WeightUnit unitOf = WeightUnit.Pounds)
        {
            _step = step; _min = min; _max = max; _loads = loads; _unitOf = unitOf;
            Spacing = 6;
            var res = Application.Current!.Resources;
            _entry = new Entry
            {
                Keyboard = Keyboard.Numeric, WidthRequest = 84, FontFamily = "GymNum", FontSize = 26,
                HorizontalTextAlignment = TextAlignment.Center, TextColor = (Color)res["OnBg"]
            };
            _entry.TextChanged += (_, e) =>
            {
                if (double.TryParse(e.NewTextValue, NumberStyles.Float, CultureInfo.CurrentCulture, out var v))
                    Value = Math.Clamp(v, _min, _max);
            };
            var minus = new Button { Text = "−", WidthRequest = 48, HeightRequest = 48, Padding = 0, FontSize = 22 };
            var plus = new Button { Text = "+", WidthRequest = 48, HeightRequest = 48, Padding = 0, FontSize = 22 };
            minus.Clicked += (_, _) => Set(Stepped(-1));
            plus.Clicked += (_, _) => Set(Stepped(+1));
            Children.Add(minus);
            Children.Add(_entry);
            Children.Add(plus);
            Children.Add(new Label { Text = unit, FontFamily = "PixelUI", FontSize = 18, TextColor = (Color)res["Muted"], VerticalOptions = LayoutOptions.Center });
            Set(value);
        }

        private double Stepped(int sign)
        {
            if (_loads is not null)
            {
                var lb = WeightMath.ToStoredPounds(Value, _unitOf);
                var next = sign > 0 ? _loads.Next(lb) : _loads.Prev(lb);
                return next is double n ? Math.Round(WeightMath.ToDisplay(n, _unitOf), 2) : Value;
            }
            return Value + sign * _step;
        }

        private void Set(double v)
        {
            Value = Math.Clamp(v, _min, _max);
            _entry.Text = Value.ToString("0.##", CultureInfo.CurrentCulture);
        }
    }
}
