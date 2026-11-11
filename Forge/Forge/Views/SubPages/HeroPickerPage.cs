using Forge.Services;

namespace Forge.Views.SubPages;

/// <summary>
/// Choose your hero's look: a race, then a class (art/gen_characters.py — every combination has
/// its own sprite on a shared frame, so forged gear can be worn by any of them later). Race tiles
/// show each race in the chosen class, class tiles show each class in the chosen race.
/// </summary>
public sealed class HeroPickerPage : ContentPage
{
    private string _race = UserSettings.HeroRace;
    private string _class = UserSettings.HeroClass;

    private readonly Image _preview = new() { HeightRequest = 230, Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Center };
    private readonly Label _name = new() { FontFamily = "PixelUI", FontSize = 30, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Grid _raceGrid = Tiles();
    private readonly Grid _classGrid = Tiles();
    private readonly Button _save = new() { HeightRequest = 64, FontFamily = "PixelUI", FontSize = 24 };

    private readonly Color _accent = (Color)Application.Current!.Resources["Accent"];
    private readonly Color _surface = (Color)Application.Current.Resources["Surface"];
    private readonly Color _muted = (Color)Application.Current.Resources["Muted"];
    private readonly Color _bg = (Color)Application.Current.Resources["Bg"];

    public HeroPickerPage()
    {
        Title = "Choose your hero";
        _name.TextColor = _accent;
        _save.BackgroundColor = _accent;
        _save.TextColor = _bg;
        _save.Clicked += async (_, _) =>
        {
            UserSettings.HeroRace = _race;
            UserSettings.HeroClass = _class;
            await Navigation.PopAsync();
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 10, 16, 24),
                Spacing = 10,
                Children =
                {
                    _preview, _name,
                    Header("Race"), _raceGrid,
                    Header("Class"), _classGrid,
                    _save
                }
            }
        };
        Render();
    }

    private static Grid Tiles() => new()
    {
        ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) },
        RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto) },
        ColumnSpacing = 8,
        RowSpacing = 8
    };

    private View Header(string text) => new Label
    {
        Text = text, FontFamily = "PixelUI", FontSize = 26, TextColor = _accent, Margin = new Thickness(2, 8, 0, 0)
    };

    private void Render()
    {
        _preview.Source = UserSettings.HeroSprite(_race, _class);
        var raceLabel = UserSettings.Races.First(r => r.Id == _race).Label;
        var classLabel = UserSettings.Classes.First(c => c.Id == _class).Label;
        _name.Text = $"{raceLabel} {classLabel}";
        _save.Text = $"Become the {raceLabel} {classLabel}";

        Fill(_raceGrid, UserSettings.Races, id => UserSettings.HeroSprite(id, _class), _race, id => { _race = id; Render(); });
        Fill(_classGrid, UserSettings.Classes, id => UserSettings.HeroSprite(_race, id), _class, id => { _class = id; Render(); });
    }

    private void Fill(Grid grid, (string Id, string Label)[] options, Func<string, string> sprite, string selected, Action<string> pick)
    {
        grid.Children.Clear();
        for (var i = 0; i < options.Length; i++)
        {
            var (id, label) = options[i];
            var on = id == selected;
            var tile = new Border
            {
                BackgroundColor = _surface,
                Stroke = on ? _accent : _muted,
                StrokeThickness = on ? 3 : 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(4, 6),
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Image { Source = sprite(id), HeightRequest = 84, Aspect = Aspect.AspectFit },
                        new Label { Text = label, FontFamily = "PixelUI", FontSize = 19, HorizontalTextAlignment = TextAlignment.Center,
                                    TextColor = on ? _accent : (Color)Application.Current!.Resources["OnBg"] }
                    }
                }
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => pick(id);
            tile.GestureRecognizers.Add(tap);
            grid.Add(tile, i % 3, i / 3);
        }
    }
}
