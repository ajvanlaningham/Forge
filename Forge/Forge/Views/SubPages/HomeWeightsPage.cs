using System.Globalization;
using Forge.Constants;
using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Views.SubPages;

/// <summary>
/// Owning a weighted implement and saying which weights, in one place (opened from a My Gear
/// tile). Dumbbells as fixed pairs or one adjustable set, kettlebells as a checklist of standard
/// bells, a barbell as bar + max + smallest jump. "I have these" is ownership: saving updates both
/// the Home inventory and the owned weights (Decision 16). The Gym is always "everything".
/// </summary>
[QueryProperty(nameof(Kind), "kind")]
public sealed class HomeWeightsPage : ContentPage
{
    private readonly IInventoryService _inventory;
    private bool _built;

    /// <summary>Which implement to edit: Dumbbell, Kettlebell or Barbell.</summary>
    public string Kind { get; set; } = nameof(Equipment.Dumbbell);
    private Equipment Implement => Enum.TryParse<Equipment>(Kind, out var e) && GearLoads.IsWeighted(e) ? e : Equipment.Dumbbell;

    private readonly Color _accent = (Color)Application.Current!.Resources["Accent"];
    private readonly Color _surface = (Color)Application.Current.Resources["Surface"];
    private readonly Color _muted = (Color)Application.Current.Resources["Muted"];
    private readonly Color _onBg = (Color)Application.Current.Resources["OnBg"];
    private readonly Color _bg = (Color)Application.Current.Resources["Bg"];

    // Working copies, saved on "Save".
    private DumbbellGear _db = new();
    private KettlebellGear _kb = new();
    private BarbellGear _bb = new();
    private bool _hasDb, _hasKb, _hasBb;

    private readonly VerticalStackLayout _dbBody = new() { Spacing = 8 };
    private readonly VerticalStackLayout _kbBody = new() { Spacing = 8 };
    private readonly VerticalStackLayout _bbBody = new() { Spacing = 8 };

    public HomeWeightsPage(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_built) return;
        _built = true;
        await _inventory.InitializeAsync();
        var owned = (await _inventory.GetOwnedFlagsAsync()).HasFlag(Implement);
        Build(owned);
    }

    private void Build(bool ownedInInventory)
    {
        var saved = UserSettings.HomeGear;
        var unit = UserSettings.WeightUnit;
        // Ownership is the inventory's call; weights already entered are kept either way.
        _hasDb = ownedInInventory && Implement == Equipment.Dumbbell;
        _hasKb = ownedInInventory && Implement == Equipment.Kettlebell;
        _hasBb = ownedInInventory && Implement == Equipment.Barbell;
        _db = saved.Dumbbells ?? new DumbbellGear { Unit = unit, Min = unit == WeightUnit.Kilograms ? 2 : 5, Max = unit == WeightUnit.Kilograms ? 24 : 52.5, Step = unit == WeightUnit.Kilograms ? 1 : 2.5 };
        _kb = saved.Kettlebells ?? new KettlebellGear { Unit = unit };
        _bb = saved.Barbell ?? (unit == WeightUnit.Kilograms
            ? new BarbellGear { Unit = WeightUnit.Kilograms, Bar = 20, Max = 100, Step = 2.5 }
            : new BarbellGear { Unit = WeightUnit.Pounds, Bar = 45, Max = 225, Step = 5 });

        var save = new Button { Text = "Save", HeightRequest = 56, FontFamily = "PixelUI", FontSize = 22, BackgroundColor = _accent, TextColor = Colors.Black };
        save.Clicked += async (_, _) => await SaveAsync();

        var (title, intro, section) = Implement switch
        {
            Equipment.Kettlebell => ("Kettlebells", "Tap every bell you own. Single bells are fine — programs use one at a time.",
                Section("Kettlebells", _hasKb, v => { _hasKb = v; RenderKb(); }, _kbBody)),
            Equipment.Barbell => ("Barbell", "Your bar, the heaviest you can load, and your smallest plate pair.",
                Section("Barbell", _hasBb, v => { _hasBb = v; RenderBb(); }, _bbBody)),
            _ => ("Dumbbells", "Fixed pairs or one adjustable set. Suggestions and the +/− buttons only land on these.",
                Section("Dumbbells", _hasDb, v => { _hasDb = v; RenderDb(); }, _dbBody))
        };
        Title = $"My Gear · {title}";

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 12, 16, 28),
                Spacing = 14,
                Children = { new Label { Text = intro, FontSize = 15, TextColor = _muted }, section, save }
            }
        };
        RenderDb(); RenderKb(); RenderBb();
    }

    // --- sections ---

    private View Section(string title, bool on, Action<bool> toggled, View body)
    {
        var sw = new Switch { IsToggled = on, OnColor = _accent, VerticalOptions = LayoutOptions.Center };
        sw.Toggled += (_, e) => toggled(e.Value);
        var head = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) }, ColumnSpacing = 8 };
        head.Add(new Label { Text = title, FontFamily = "PixelUI", FontSize = 26, TextColor = _accent, VerticalOptions = LayoutOptions.Center });
        head.Add(new Label { Text = "I have these", FontSize = 14, TextColor = _muted, VerticalOptions = LayoutOptions.Center }, 1);
        head.Add(sw, 2);
        return new Border
        {
            BackgroundColor = _surface, Stroke = _muted, StrokeThickness = 1, Padding = 14,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = new VerticalStackLayout { Spacing = 10, Children = { head, body } }
        };
    }

    private void RenderDb()
    {
        _dbBody.Clear();
        _dbBody.IsVisible = _hasDb;
        if (!_hasDb) return;

        _dbBody.Add(Segmented(new[] { "Fixed pairs", "Adjustable" }, _db.Adjustable ? 1 : 0, i => { _db.Adjustable = i == 1; RenderDb(); }));
        _dbBody.Add(UnitToggle(_db.Unit, u =>
        {
            if (u == _db.Unit) return;
            _db.Unit = u; _db.Weights.Clear();
            (_db.Min, _db.Max, _db.Step) = u == WeightUnit.Kilograms ? (2, 24, 1) : (5, 52.5, 2.5);
            RenderDb();
        }));

        if (_db.Adjustable)
        {
            var u = WeightMath.UnitLabel(_db.Unit);
            _dbBody.Add(NumberRow($"Lightest ({u})", _db.Min, v => _db.Min = v));
            _dbBody.Add(NumberRow($"Heaviest ({u})", _db.Max, v => _db.Max = v));
            _dbBody.Add(NumberRow($"Step ({u})", _db.Step, v => _db.Step = Math.Max(0.5, v)));
        }
        else
        {
            var std = _db.Unit == WeightUnit.Kilograms ? GearLoads.StandardDumbbellsKg : GearLoads.StandardDumbbellsLb;
            _dbBody.Add(new Label { Text = "Tap every pair you own.", FontSize = 14, TextColor = _muted });
            _dbBody.Add(Chips(std, _db.Weights, WeightMath.UnitLabel(_db.Unit)));
        }
    }

    private void RenderKb()
    {
        _kbBody.Clear();
        _kbBody.IsVisible = _hasKb;
        if (!_hasKb) return;

        _kbBody.Add(UnitToggle(_kb.Unit, u => { if (u == _kb.Unit) return; _kb.Unit = u; _kb.Weights.Clear(); RenderKb(); }));
        var std = _kb.Unit == WeightUnit.Kilograms ? GearLoads.StandardKettlebellsKg : GearLoads.StandardKettlebellsLb;
        _kbBody.Add(Chips(std, _kb.Weights, WeightMath.UnitLabel(_kb.Unit)));
    }

    private void RenderBb()
    {
        _bbBody.Clear();
        _bbBody.IsVisible = _hasBb;
        if (!_hasBb) return;

        var bars = new[] { ("45 lb", WeightUnit.Pounds, 45.0), ("35 lb", WeightUnit.Pounds, 35.0), ("20 kg", WeightUnit.Kilograms, 20.0), ("15 kg", WeightUnit.Kilograms, 15.0) };
        var current = Array.FindIndex(bars, b => b.Item2 == _bb.Unit && Math.Abs(b.Item3 - _bb.Bar) < 0.01);
        _bbBody.Add(new Label { Text = "Bar", FontSize = 15, TextColor = _onBg });
        _bbBody.Add(Segmented(bars.Select(b => b.Item1).ToArray(), Math.Max(0, current), i =>
        {
            var (_, unit, bar) = bars[i];
            if (unit != _bb.Unit)
                (_bb.Max, _bb.Step) = unit == WeightUnit.Kilograms ? (100, 2.5) : (225, 5);
            _bb.Unit = unit; _bb.Bar = bar;
            RenderBb();
        }));

        var u = WeightMath.UnitLabel(_bb.Unit);
        _bbBody.Add(NumberRow($"Heaviest you can load ({u})", _bb.Max, v => _bb.Max = Math.Max(_bb.Bar, v)));

        var jumps = _bb.Unit == WeightUnit.Kilograms ? new[] { 1.0, 2.5, 5.0 } : new[] { 2.5, 5.0, 10.0 };
        _bbBody.Add(new Label { Text = "Smallest jump (a pair of your smallest plates)", FontSize = 15, TextColor = _onBg });
        _bbBody.Add(Segmented(jumps.Select(j => $"{Fmt(j)} {u}").ToArray(), Math.Max(0, Array.IndexOf(jumps, _bb.Step)), i => { _bb.Step = jumps[i]; RenderBb(); }));
    }

    // --- controls ---

    private View Chips(IReadOnlyList<double> options, List<double> selected, string unit)
    {
        var flex = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start };
        foreach (var w in options)
        {
            var b = new Button { Text = $"{Fmt(w)}", FontFamily = "GymNum", FontSize = 22, Margin = 3, Padding = new Thickness(10, 2), MinimumWidthRequest = 62, HeightRequest = 46 };
            void Paint()
            {
                var on = selected.Any(s => Math.Abs(s - w) < 0.001);
                b.BackgroundColor = on ? _accent : _bg;
                b.TextColor = on ? Colors.Black : _onBg;
            }
            b.Clicked += (_, _) =>
            {
                var hit = selected.FindIndex(s => Math.Abs(s - w) < 0.001);
                if (hit >= 0) selected.RemoveAt(hit); else selected.Add(w);
                Paint();
            };
            Paint();
            flex.Add(b);
        }
        return new VerticalStackLayout { Children = { flex, new Label { Text = unit, FontSize = 13, TextColor = _muted } } };
    }

    private View Segmented(string[] labels, int selected, Action<int> picked)
    {
        var row = new HorizontalStackLayout { Spacing = 6 };
        for (var i = 0; i < labels.Length; i++)
        {
            var index = i;
            var on = i == selected;
            var b = new Button
            {
                Text = labels[i], FontSize = 15, Padding = new Thickness(12, 4), HeightRequest = 44,
                BackgroundColor = on ? _accent : _bg, TextColor = on ? Colors.Black : _onBg
            };
            b.Clicked += (_, _) => picked(index);
            row.Add(b);
        }
        return new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = row };
    }

    private View UnitToggle(WeightUnit current, Action<WeightUnit> picked)
        => Segmented(new[] { "lb", "kg" }, current == WeightUnit.Kilograms ? 1 : 0,
            i => picked(i == 1 ? WeightUnit.Kilograms : WeightUnit.Pounds));

    private View NumberRow(string label, double value, Action<double> changed)
    {
        var entry = new Entry
        {
            Text = Fmt(value), Keyboard = Keyboard.Numeric, WidthRequest = 110, FontFamily = "GymNum", FontSize = 24,
            HorizontalTextAlignment = TextAlignment.Center, TextColor = _onBg
        };
        entry.TextChanged += (_, e) =>
        {
            if (double.TryParse(e.NewTextValue, NumberStyles.Float, CultureInfo.CurrentCulture, out var v) && v >= 0)
                changed(v);
        };
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        grid.Add(new Label { Text = label, FontSize = 15, TextColor = _onBg, VerticalOptions = LayoutOptions.Center });
        grid.Add(entry, 1);
        return grid;
    }

    private static string Fmt(double v) => v.ToString("0.##", CultureInfo.CurrentCulture);

    private async Task SaveAsync()
    {
        var has = Implement switch { Equipment.Kettlebell => _hasKb, Equipment.Barbell => _hasBb, _ => _hasDb };
        if (has && Implement == Equipment.Dumbbell && !_db.Adjustable && _db.Weights.Count == 0)
        {
            await DisplayAlert("Dumbbells", "Tap the pairs you own, or switch \"I have these\" off.", "OK");
            return;
        }
        if (has && Implement == Equipment.Dumbbell && _db.Adjustable && _db.Max <= _db.Min)
        {
            await DisplayAlert("Dumbbells", "Heaviest must be more than lightest.", "OK");
            return;
        }
        if (has && Implement == Equipment.Kettlebell && _kb.Weights.Count == 0)
        {
            await DisplayAlert("Kettlebells", "Tap the bells you own, or switch \"I have these\" off.", "OK");
            return;
        }

        // Owning it and its weights are one fact: write both.
        var gear = UserSettings.HomeGear;
        switch (Implement)
        {
            case Equipment.Kettlebell: gear.Kettlebells = has ? _kb : null; break;
            case Equipment.Barbell: gear.Barbell = has ? _bb : null; break;
            default: gear.Dumbbells = has ? _db : null; break;
        }
        UserSettings.HomeGear = gear;
        await _inventory.SetOwnedAsync(Implement, has);
        await Navigation.PopAsync();
    }
}
