using Forge.Models;
using Forge.Services;

namespace Forge.Views.SubPages;

/// <summary>
/// Which equipment the Gym location has. Home uses My Gear (the owned inventory); this is the
/// other half of "locations + swaps" (PBI 8.1). Built in code: it is a plain list of switches.
/// </summary>
public sealed class GymEquipmentPage : ContentPage
{
    public GymEquipmentPage()
    {
        Title = "Gym equipment";
        var list = new VerticalStackLayout { Padding = new Thickness(20, 12), Spacing = 4 };
        list.Add(new Label
        {
            Text = "What the gym has. Exercises needing anything switched off are swapped when you train at the Gym.",
            FontSize = 14,
            Opacity = 0.75,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var current = UserSettings.GymEquipment;
        foreach (var group in Enum.GetValues<Equipment>()
                     .Where(e => e != Equipment.None)
                     .GroupBy(EquipmentGroups.GetGroup)
                     .OrderBy(g => g.Key))
        {
            list.Add(new Label { Text = group.Key, FontSize = 20, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 2) });
            foreach (var item in group.OrderBy(e => e.ToString()))
            {
                var toggle = new Switch { IsToggled = current.HasFlag(item), VerticalOptions = LayoutOptions.Center };
                toggle.Toggled += (_, e) =>
                {
                    UserSettings.GymEquipment = e.Value
                        ? UserSettings.GymEquipment | item
                        : UserSettings.GymEquipment & ~item;
                };
                var row = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, HeightRequest = 48 };
                row.Add(new Label { Text = Humanize(item), VerticalOptions = LayoutOptions.Center, FontSize = 18 });
                row.Add(toggle, 1);
                list.Add(row);
            }
        }
        Content = new ScrollView { Content = list };
    }

    /// <summary>"SquatRack" → "Squat rack".</summary>
    private static string Humanize(Equipment e)
    {
        var s = e.ToString();
        var spaced = string.Concat(s.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
        return spaced;
    }
}
