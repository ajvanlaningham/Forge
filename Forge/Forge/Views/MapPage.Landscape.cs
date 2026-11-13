using Forge.Training;

namespace Forge.Views;

/// <summary>
/// The map's landscape (2026-11-13, second pass): parchment paper, each week a region with its own
/// scenery and name, rivers the trail crosses by bridge, a lake with something in it, and mountains
/// (and a dragon) before the anvil. Scenery is art/gen_mapscape.py's sprites at 3 dp per pixel;
/// water is drawn on the same 3 dp grid by <see cref="MapDrawable"/> so it matches the pixel art.
/// </summary>
public sealed partial class MapPage
{
    private const double ArtPx = 3, Edge = 186, PaperTile = 192;

    private static readonly string[] RegionNames = { "The Ember Fells", "Greenhollow Downs", "Quenchmere", "The Anvil Peaks" };
    private static readonly string?[] RiverNames = { null, "Silverrun", "River Slake", null };

    private readonly record struct Piece(string Sprite, double Dx, double Dy = 0);

    // Each week's vignettes: a few sprites placed together on the far side of the trail.
    private static readonly Piece[][][] Vignettes =
    {
        new[] // Kindling: ember fells
        {
            new Piece[] { new("volcano", 0) },
            new Piece[] { new("dead", -16), new("dead", 18, 4) },
            new Piece[] { new("tower", -12), new("dead", 26, 2) },
            new Piece[] { new("embers", -8), new("pine_b", 34, -2) },
            new Piece[] { new("stones", 0) },
            new Piece[] { new("crags", 0) },
            new Piece[] { new("embers", 0) },
        },
        new[] // Tempering: green downs
        {
            new Piece[] { new("oak_a", -22), new("oak_c", 10, 2), new("oak_b", 38, 6) },
            new Piece[] { new("hill_a", 0), new("oak_b", 30, 4) },
            new Piece[] { new("cottage", -14), new("oak_a", 30, 2) },
            new Piece[] { new("pine_a", -22), new("pine_c", 0, 2), new("pine_b", 22, 6) },
            new Piece[] { new("hill_b", -24), new("hill_a", 26, 4) },
            new Piece[] { new("oak_c", 0) },
            new Piece[] { new("stones", 0) },
        },
        new[] // Quenching: lake country
        {
            new Piece[] { new("reeds", -14), new("reeds", 16, 4) },
            new Piece[] { new("pine_c", -16), new("pine_a", 14, 4) },
            new Piece[] { new("oak_b", -18), new("oak_a", 16, 4) },
            new Piece[] { new("hill_a", 0) },
            new Piece[] { new("tower", 0), new("reeds", 28, 2) },
            new Piece[] { new("cottage", 0) },
        },
        new[] // Honing: the mountains before the forge
        {
            new Piece[] { new("range_b", 0) },
            new Piece[] { new("range_s", -6), new("pine_b", 50, 4) },
            new Piece[] { new("range_a", 0) },
            new Piece[] { new("peak_a", -14), new("peak_b", 30, 4) },
            new Piece[] { new("range_s", 0) },
            new Piece[] { new("crags", -6), new("pine_a", 42, 0) },
            new Piece[] { new("peak_b", -18), new("pine_b", 12, 4), new("pine_a", 30, 2) },
        },
    };

    private readonly VerticalStackLayout _paper = new() { Spacing = 0, IsClippedToBounds = true };
    private readonly HashSet<int> _namedWeeks = new();
    private bool _lakeDrawn;

    private void ResetLandscape()
    {
        _trail.Rivers.Clear();
        _trail.Lakes.Clear();
        _namedWeeks.Clear();
        _lakeDrawn = false;
    }

    /// <summary>Tiles the paper texture down the whole map.</summary>
    private void BuildPaper(double height)
    {
        _paper.Children.Clear();
        _paper.HeightRequest = height;
        for (var i = 0; i < Math.Ceiling(height / PaperTile); i++)
        {
            var line = new HorizontalStackLayout { Spacing = 0 };
            for (var j = 0; j < 4; j++)
                line.Add(new Image { Source = "map_paper.png", WidthRequest = PaperTile, HeightRequest = PaperTile, Aspect = Aspect.Fill });
            _paper.Add(line);
        }
    }

    /// <summary>The open ground on one side of a stone: its centre and width.</summary>
    private static (double Centre, double Room) FreeSpan(double x, int side, double halfWidth)
    {
        var inner = x + side * (halfWidth + 10);
        var outer = side * Edge;
        return ((inner + outer) / 2, Math.Abs(outer - inner));
    }

    private static Image Sprite(string name, double x, double dy, LayoutOptions vertical)
    {
        var (w, h) = MapScenery.Size[name];
        return new Image
        {
            Source = $"map_s_{name}.png", WidthRequest = w * ArtPx, HeightRequest = h * ArtPx, Aspect = Aspect.Fill,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = vertical, TranslationX = x, TranslationY = dy
        };
    }

    private static double Extent(Piece[] v) => v.Max(p => Math.Abs(p.Dx) + MapScenery.Size[p.Sprite].W * ArtPx / 2) * 2;

    private static Label PlaceName(string text, double x, double width, double size = 19) => new()
    {
        Text = text, FontFamily = "FantasyH", FontSize = size, TextColor = InkSoft, HorizontalTextAlignment = TextAlignment.Center,
        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start, TranslationX = x, WidthRequest = width,
        Rotation = -4, Margin = new Thickness(0, 4, 0, 0)
    };

    /// <summary>Dresses a row of the path with the week's landscape on the open side(s) of its stone.</summary>
    private void Decorate(Grid row, MapNode node, double x, int index, double height)
    {
        var week = Math.Clamp(node.Week, 1, 4);
        var half = node.Kind switch { MapNodeKind.Anvil => 62.0, MapNodeKind.Chest => 46, _ => 54 };
        int[] sides = node.State == MapNodeState.Current
            ? (Math.Abs(x) < 10 ? new[] { -1 } : Array.Empty<int>())   // the hero stands on the other side
            : x > 10 ? new[] { -1 } : x < -10 ? new[] { 1 } : new[] { -1, 1 };

        foreach (var side in sides)
        {
            var (centre, room) = FreeSpan(x, side, half);
            if (room < 56) continue;

            // The anvil sits under the mountain gate; the last chest has the dragon over its peaks.
            if (node.Kind == MapNodeKind.Anvil)
            {
                if (room >= 96) row.Add(Sprite(side < 0 ? "gate" : "range_s", centre, -6, LayoutOptions.End));
                continue;
            }
            if (node.Kind == MapNodeKind.Chest && week == 4 && room >= 110)
            {
                row.Add(Sprite("range_a", centre, -6, LayoutOptions.End));
                row.Add(Sprite("dragon", centre + 14, 2, LayoutOptions.Start));
                continue;
            }

            // One lake, in the lake country, where there's room for it.
            if (week == 3 && !_lakeDrawn && room >= 130 && node.Kind == MapNodeKind.Training)
            {
                _lakeDrawn = true;
                var rx = Math.Min(room / 2 - 8, 84);
                var cy = _y - height / 2 + 16;
                _trail.Lakes.Add(((float)centre, (float)cy, (float)rx, (float)Math.Min(height / 2 - 20, 36)));
                row.Add(Sprite("serpent", centre + 6, 22, LayoutOptions.Center));
                row.Add(Sprite("reeds", centre - rx + 12, -4, LayoutOptions.End));
                row.Add(Sprite("reeds", centre + rx - 14, -12, LayoutOptions.End));
                _namedWeeks.Add(week);
                row.Add(PlaceName(RegionNames[week - 1], centre, room, 20));
                continue;
            }

            var named = false;
            if (!_namedWeeks.Contains(week) && room >= 140)
            {
                _namedWeeks.Add(week);
                row.Add(PlaceName(RegionNames[week - 1], centre, room));
                named = true;
            }
            var fits = Vignettes[week - 1].Where(v => Extent(v) <= room - 8).ToArray();
            if (fits.Length == 0) continue;
            var pick = fits[(index * 5 + (side > 0 ? 3 : 0) + week) % fits.Length];
            foreach (var p in pick)
            {
                var (_, h) = MapScenery.Size[p.Sprite];
                // Keep clear of the name lettered above, and of the row above.
                var lift = named ? 0 : -8;
                if (h * ArtPx > height - (named ? 40 : 12)) continue;
                row.Add(Sprite(p.Sprite, centre + p.Dx, lift + p.Dy, LayoutOptions.End));
            }
        }
    }

    /// <summary>A river across the map between two stones; the trail crosses it by a bridge.</summary>
    private View RiverRow(int week, int prevIndex)
    {
        const double height = 76;
        var xa = QuestMap.Wind(prevIndex) * Swing;
        var xb = QuestMap.Wind(prevIndex + 1) * Swing;
        var bx = (xa + xb) / 2;
        var cy = _y + height / 2;
        _trail.Points.Add(new PointF((float)bx, (float)cy));
        _trail.Rivers.Add(((float)cy, (float)bx));
        _y += height;

        var row = new Grid { HeightRequest = height };
        row.Add(Sprite("bridge", bx, 0, LayoutOptions.Center));
        if (RiverNames[week - 1] is { } name)
        {
            var side = bx > 0 ? -1 : 1;
            // Lettered just below the water, clear of the scenery above it.
            var label = PlaceName(name, side * 112, 140, 17);
            label.Rotation = 0;
            label.VerticalOptions = LayoutOptions.End;
            label.Margin = new Thickness(0, 0, 0, -2);
            row.Add(label);
        }
        return row;
    }
}

/// <summary>The banner's band: stitched cloth with a lit top edge and a shaded bottom, in pen.</summary>
internal sealed class BannerDrawable : IDrawable
{
    public Color Face { get; set; } = Colors.Red;
    public Color Light { get; set; } = Colors.Pink;
    public Color Dark { get; set; } = Colors.DarkRed;

    // The top corners curve down, in pixel steps: how far the pen line is inset on each of the top
    // rows (in 4 dp pixels). The cloth inside follows a pixel inside it.
    private static readonly int[] Corner = { 4, 2, 1, 1 };

    private static int Outline(int row) => row >= 0 && row < Corner.Length ? Corner[row] : 0;

    private static int Cloth(int row) => Math.Max(Outline(row) + 1, Math.Max(Outline(row - 1), Outline(row + 1)));

    public void Draw(ICanvas canvas, RectF r)
    {
        const float p = 4;
        void Row(Color colour, int row, int inset, float height = p)
        {
            canvas.FillColor = colour;
            canvas.FillRectangle(r.X + inset * p, r.Y + row * p, r.Width - 2 * inset * p, height);
        }

        // Pen line: the curved top rows, then straight down.
        for (var row = 0; row < Corner.Length; row++) Row(Colors.Black, row, Outline(row));
        Row(Colors.Black, Corner.Length, 0, r.Height - Corner.Length * p);
        // Cloth, a pixel inside the line; the lit top edge follows the curve.
        for (var row = 1; row <= Corner.Length; row++) Row(row == 1 ? Light : Face, row, Cloth(row));
        canvas.FillColor = Face;
        canvas.FillRectangle(r.X + p, r.Y + (Corner.Length + 1) * p, r.Width - 2 * p, r.Height - (Corner.Length + 3) * p);
        Row(Dark, (int)(r.Height / p) - 2, 1);
        // Running stitches along both edges.
        canvas.FillColor = Light.WithAlpha(0.75f);
        for (var x = r.X + 4 * p; x < r.Right - 5 * p; x += 3 * p)
        {
            canvas.FillRectangle(x, r.Y + 2.5f * p, 2 * p, p / 2);
            canvas.FillRectangle(x, r.Bottom - 3 * p, 2 * p, p / 2);
        }
    }
}

/// <summary>
/// The map under the path: a wash per week's region, a faint graticule, rhumb lines from the compass
/// rose, rivers and a lake on the 3 dp pixel grid, burnt edges and a ruled frame, and the trail in ink.
/// The paper itself is the tiled texture beneath. Deterministic, so it never shimmers.
/// </summary>
internal sealed class MapDrawable : IDrawable
{
    private const float P = 3;
    private static readonly Color Ink = Color.FromArgb("#2B1D14"), Burn = Color.FromArgb("#5A3A1C"),
        Water = Color.FromArgb("#7FA3B8"), Deep = Color.FromArgb("#6A90A6"), Ripple = Color.FromArgb("#B4CFDB"), Bank = Color.FromArgb("#3E5566");

    /// <summary>How strongly a week's colour tints its region of the map.</summary>
    public const float RegionWash = 0.07f;

    /// <summary>x is the offset from the centre line; y is from the top of the path.</summary>
    public List<PointF> Points { get; } = new();

    /// <summary>Where each week's region begins, and its colour.</summary>
    public List<(float Top, Color Tint)> Regions { get; } = new();

    /// <summary>Rivers across the map: the river's centre line, and where the bridge carries the trail.</summary>
    public List<(float Y, float BridgeX)> Rivers { get; } = new();

    /// <summary>Lakes: centre (x from the centre line), and radii.</summary>
    public List<(float Cx, float Cy, float Rx, float Ry)> Lakes { get; } = new();

    /// <summary>The compass rose's centre (x from the right edge, y from the top), for its rhumb lines.</summary>
    public PointF? Compass { get; set; }

    private static float Snap(float v) => MathF.Round(v / P) * P;

    public void Draw(ICanvas canvas, RectF r)
    {
        var mid = r.Width / 2;
        for (var i = 0; i < Regions.Count; i++)
        {
            var top = Regions[i].Top;
            var bottom = i + 1 < Regions.Count ? Regions[i + 1].Top : r.Height;
            canvas.FillColor = Regions[i].Tint.WithAlpha(RegionWash);
            canvas.FillRectangle(0, top, r.Width, bottom - top);
        }

        // A faint graticule, and rhumb lines fanning from the compass rose, as on an old chart.
        canvas.StrokeColor = Ink.WithAlpha(0.07f);
        canvas.StrokeSize = 1;
        canvas.StrokeDashPattern = new float[] { 4, 4 };
        for (var y = 60f; y < r.Height; y += 180) canvas.DrawLine(0, y, r.Width, y);
        canvas.DrawLine(r.Width * 0.2f, 0, r.Width * 0.2f, r.Height);
        canvas.DrawLine(r.Width * 0.8f, 0, r.Width * 0.8f, r.Height);
        canvas.StrokeDashPattern = null;
        if (Compass is { } c)
        {
            var cx = r.Width - c.X;
            canvas.StrokeColor = Ink.WithAlpha(0.09f);
            for (var k = 0; k < 16; k++)
            {
                var a = k * MathF.PI / 8;
                canvas.DrawLine(cx, c.Y, cx + MathF.Cos(a) * 900, c.Y + MathF.Sin(a) * 900);
            }
        }

        foreach (var (ry, bridge) in Rivers) DrawRiver(canvas, r, mid, ry, mid + bridge);
        foreach (var lake in Lakes) DrawLake(canvas, mid + lake.Cx, lake.Cy, lake.Rx, lake.Ry);

        // Burnt edges and a double-ruled frame.
        for (var k = 0; k < 10; k++)
        {
            canvas.FillColor = Burn.WithAlpha(0.05f * (10 - k) / 3);
            canvas.FillRectangle(k * 2, 0, 2, r.Height);
            canvas.FillRectangle(r.Width - (k + 1) * 2, 0, 2, r.Height);
        }
        canvas.FillColor = Ink.WithAlpha(0.6f);
        canvas.FillRectangle(9, 0, P, r.Height);
        canvas.FillRectangle(r.Width - 9 - P, 0, P, r.Height);
        canvas.FillColor = Ink.WithAlpha(0.35f);
        canvas.FillRectangle(15, 0, 1.5f, r.Height);
        canvas.FillRectangle(r.Width - 16.5f, 0, 1.5f, r.Height);

        if (Points.Count < 2) return;
        canvas.StrokeColor = Color.FromArgb("#6B3A22");
        canvas.StrokeSize = 6;
        canvas.StrokeLineCap = LineCap.Butt;
        canvas.StrokeDashPattern = new float[] { 1.6f, 1.2f };
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
        canvas.StrokeDashPattern = null;
    }

    /// <summary>A meandering river, calm where the bridge crosses it: banks in pen, ripples in light.</summary>
    private static void DrawRiver(ICanvas canvas, RectF r, float mid, float ry, float bridgeX)
    {
        for (var x = 0f; x < r.Width; x += P)
        {
            var calm = Math.Clamp((MathF.Abs(x - bridgeX) - 30) / 70f, 0, 1);
            var cy = ry + calm * 9 * MathF.Sin(x * 0.03f + ry * 0.013f);
            var half = 9 + 2.5f * MathF.Sin(x * 0.05f + 1);
            var top = Snap(cy - half);
            var bottom = Snap(cy + half);
            canvas.FillColor = Bank;
            canvas.FillRectangle(x, top - P, P, bottom - top + 2 * P);
            canvas.FillColor = Water;
            canvas.FillRectangle(x, top, P, bottom - top);
            var col = (int)(x / P);
            if ((col + (int)(ry / P)) % 9 < 3)
            {
                canvas.FillColor = Ripple;
                canvas.FillRectangle(x, Snap(cy - 3), P, P);
            }
            else if ((col + 4) % 11 < 2)
            {
                canvas.FillColor = Ripple;
                canvas.FillRectangle(x, Snap(cy + 3), P, P);
            }
        }
    }

    /// <summary>A lake with a ragged pen-line shore, deeper water in the middle, and ripples.</summary>
    private static void DrawLake(ICanvas canvas, float cx, float cy, float rx, float ry)
    {
        void Body(float grow, Color colour, float inset)
        {
            for (var y = Snap(cy - ry - grow); y <= cy + ry + grow; y += P)
            {
                var t = (y - cy) / (ry + grow);
                if (MathF.Abs(t) > 1) continue;
                var half = (rx + grow) * MathF.Sqrt(1 - t * t) + 5 * MathF.Sin(y * 0.08f) - inset;
                if (half < P) continue;
                var l = Snap(cx - half);
                var rr = Snap(cx + half + 3 * MathF.Sin(y * 0.13f + 2));
                canvas.FillColor = colour;
                canvas.FillRectangle(l, y, rr - l, P);
            }
        }
        Body(P, Bank, 0);
        Body(0, Water, 0);
        Body(-ry * 0.35f, Deep, rx * 0.25f);
        canvas.FillColor = Ripple;
        for (var k = 0; k < 6; k++)
        {
            var y = Snap(cy - ry * 0.6f + k * ry * 0.24f);
            var x = Snap(cx - rx * 0.55f + (k * 37 % 5) * rx * 0.2f);
            canvas.FillRectangle(x, y, 3 * P, P);
        }
    }
}
