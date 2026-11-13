using System.Xml.Linq;
using Forge.Constants;

namespace Forge.Tests;

/// <summary>
/// Legibility pass (2026-11-13): every text colour the theme uses must stay readable on the
/// surfaces it sits on. Reads the shipped Colors.xaml so a palette tweak can't quietly fail.
/// </summary>
public class ContrastTests
{
    private static readonly Dictionary<string, string> Palette = Load();

    private static Dictionary<string, string> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Styles", "Colors.xaml");
        XNamespace x = "http://schemas.microsoft.com/winfx/2009/xaml";
        return XDocument.Load(path).Root!.Elements()
            .Where(e => e.Name.LocalName == "Color" && e.Value.Trim().StartsWith('#'))
            .ToDictionary(e => (string)e.Attribute(x + "Key")!, e => e.Value.Trim());
    }

    [Fact]
    public void Ratio_MatchesKnownValues()
    {
        Assert.Equal(21.0, Contrast.Ratio("#000000", "#FFFFFF"), 1);
        Assert.Equal(1.0, Contrast.Ratio("#777777", "#777777"), 3);
    }

    [Theory]
    [InlineData("OnBg", "Bg")]
    [InlineData("OnBg", "Surface")]
    [InlineData("Muted", "Bg")]
    [InlineData("Muted", "Surface")]
    [InlineData("Accent", "Bg")]
    [InlineData("Accent", "Surface")]
    [InlineData("Leaf", "Bg")]
    [InlineData("Leaf", "Surface")]
    [InlineData("Danger", "Bg")]
    [InlineData("Danger", "Surface")]
    [InlineData("Aether", "Surface")]
    [InlineData("Bg", "Accent")]          // text on the primary button
    [InlineData("Gray300", "Gray600")]    // disabled button
    [InlineData("Bg", "Aether")]          // the level-up banner
    [InlineData("Leaf", "Surface")]       // intro and summary tiles
    [InlineData("Bg", "Leaf")]            // the chosen EASY on the summary
    [InlineData("Bg", "Danger")]          // the chosen HARD
    [InlineData("Ink", "Parchment")]      // the map and the quest scrolls
    [InlineData("InkSoft", "Parchment")]
    public void TextColours_PassAA(string fg, string bg)
    {
        var ratio = Contrast.Ratio(Palette[fg], Palette[bg]);
        Assert.True(ratio >= Contrast.Text, $"{fg} on {bg} is {ratio:0.00}:1, needs {Contrast.Text}:1");
    }
}
