namespace Forge.ViewModels.Controls
{
    /// <summary>A small trend line of scores (oldest to newest) with the latest point marked.</summary>
    public sealed class Sparkline : IDrawable
    {
        private readonly IReadOnlyList<double> _points;
        private readonly Color _line;
        private readonly Color _dot;

        public Sparkline(IReadOnlyList<double> points, Color line, Color dot)
        {
            _points = points;
            _line = line;
            _dot = dot;
        }

        public void Draw(ICanvas canvas, RectF rect)
        {
            if (_points.Count == 0) return;
            const float pad = 4;
            var min = _points.Min();
            var max = _points.Max();
            if (max - min < 1) { min -= 1; max += 1; }
            PointF At(int i) => new(
                rect.Left + pad + (_points.Count == 1 ? (rect.Width - 2 * pad) : i * (rect.Width - 2 * pad) / (_points.Count - 1)),
                rect.Bottom - pad - (float)((_points[i] - min) / (max - min)) * (rect.Height - 2 * pad));
            canvas.StrokeColor = _line;
            canvas.StrokeSize = 2;
            for (var i = 1; i < _points.Count; i++)
                canvas.DrawLine(At(i - 1), At(i));
            var last = At(_points.Count - 1);
            canvas.FillColor = _dot;
            canvas.FillCircle(last, 3.5f);
        }
    }
}
