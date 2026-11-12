namespace Forge.ViewModels.Controls
{
    /// <summary>
    /// A small trend line (oldest to newest) with the latest point marked, or every point when
    /// <c>allDots</c> is set. Drawn with MAUI Graphics, so no native chart library is needed.
    /// </summary>
    public sealed class Sparkline : IDrawable
    {
        private readonly IReadOnlyList<double> _points;
        private readonly Color _line;
        private readonly Color _dot;
        private readonly bool _allDots;

        public Sparkline(IReadOnlyList<double> points, Color line, Color dot, bool allDots = false)
        {
            _points = points;
            _line = line;
            _dot = dot;
            _allDots = allDots;
        }

        public static Sparkline Empty { get; } = new(Array.Empty<double>(), Colors.Transparent, Colors.Transparent);

        public void Draw(ICanvas canvas, RectF rect)
        {
            if (_points.Count == 0) return;
            const float pad = 6;
            var min = _points.Min();
            var max = _points.Max();
            if (max - min < 1) { min -= 1; max += 1; }
            PointF At(int i) => new(
                rect.Left + pad + (_points.Count == 1 ? (rect.Width - 2 * pad) / 2 : i * (rect.Width - 2 * pad) / (_points.Count - 1)),
                rect.Bottom - pad - (float)((_points[i] - min) / (max - min)) * (rect.Height - 2 * pad));
            canvas.StrokeColor = _line;
            canvas.StrokeSize = 2;
            canvas.StrokeLineCap = LineCap.Round;
            for (var i = 1; i < _points.Count; i++)
                canvas.DrawLine(At(i - 1), At(i));
            canvas.FillColor = _dot;
            if (_allDots)
                for (var i = 0; i < _points.Count; i++) canvas.FillCircle(At(i), 3f);
            else
                canvas.FillCircle(At(_points.Count - 1), 3.5f);
        }
    }
}
