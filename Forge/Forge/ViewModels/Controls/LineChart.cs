namespace Forge.ViewModels.Controls
{
    /// <summary>
    /// A labelled line chart (exercise history): value above each point, date below, a faint
    /// fill under the line. The axis fits the data so a 5% strength gain is visible. Labels thin
    /// out when points crowd, always keeping the first and last.
    /// </summary>
    public sealed class LineChart : IDrawable
    {
        private readonly IReadOnlyList<(string Label, double Value, string ValueLabel)> _points;
        private readonly double _min, _max;
        private readonly Color _line, _text;

        public LineChart(IReadOnlyList<(string Label, double Value, string ValueLabel)> points, double min, double max, Color line, Color text)
        {
            _points = points;
            _min = min;
            _max = max > min ? max : min + 1;
            _line = line;
            _text = text;
        }

        public void Draw(ICanvas canvas, RectF rect)
        {
            if (_points.Count == 0) return;
            const float side = 24, top = 26, bottom = 28, fontSize = 13;
            var plot = new RectF(rect.Left + side, rect.Top + top, rect.Width - 2 * side, rect.Height - top - bottom);
            PointF At(int i) => new(
                plot.Left + (_points.Count == 1 ? plot.Width / 2 : i * plot.Width / (_points.Count - 1)),
                plot.Bottom - (float)((_points[i].Value - _min) / (_max - _min)) * plot.Height);

            // Fill under the line.
            var area = new PathF();
            area.MoveTo(At(0).X, plot.Bottom);
            for (var i = 0; i < _points.Count; i++) area.LineTo(At(i));
            area.LineTo(At(_points.Count - 1).X, plot.Bottom);
            area.Close();
            canvas.FillColor = _line.WithAlpha(0.12f);
            canvas.FillPath(area);

            canvas.StrokeColor = _line;
            canvas.StrokeSize = 3;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            for (var i = 1; i < _points.Count; i++) canvas.DrawLine(At(i - 1), At(i));

            canvas.FillColor = _line;
            for (var i = 0; i < _points.Count; i++) canvas.FillCircle(At(i), 4.5f);

            // Roughly 52 px per label; always the first and last.
            var every = Math.Max(1, (int)Math.Ceiling(_points.Count / Math.Max(1f, plot.Width / 52f)));
            canvas.FontColor = _text;
            canvas.FontSize = fontSize;
            for (var i = 0; i < _points.Count; i++)
            {
                if (i % every != 0 && i != _points.Count - 1) continue;
                if (i != _points.Count - 1 && _points.Count - 1 - i < every) continue; // don't crowd the last
                var p = At(i);
                canvas.DrawString(_points[i].ValueLabel, p.X - 30, p.Y - 22, 60, 16, HorizontalAlignment.Center, VerticalAlignment.Bottom);
                canvas.DrawString(_points[i].Label, p.X - 30, plot.Bottom + 8, 60, 16, HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }
    }
}
