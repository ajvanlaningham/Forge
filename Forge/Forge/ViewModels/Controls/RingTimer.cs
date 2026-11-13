using Forge.Training;

namespace Forge.ViewModels.Controls
{
    /// <summary>
    /// The round countdown (user, 2026-11-13): a thick ring that drains clockwise from the top as the
    /// clock runs, around the big number. A per-side set splits the ring into two halves, RIGHT then
    /// LEFT, with a tick where you switch. Bind a new instance to redraw.
    /// </summary>
    public sealed class RingTimer : IDrawable
    {
        private readonly double _remaining;
        private readonly Color _colour;
        private readonly double? _switchAt; // fraction of the ring where the switch tick sits

        public RingTimer(double remaining, Color colour, double? switchAt = null)
        {
            _remaining = Math.Clamp(remaining, 0, 1);
            _colour = colour;
            _switchAt = switchAt;
        }

        public static RingTimer Empty { get; } = new(0, Colors.Transparent);

        public void Draw(ICanvas canvas, RectF rect)
        {
            const float stroke = 18;
            var size = Math.Min(rect.Width, rect.Height) - stroke - 4;
            var x = rect.Center.X - size / 2;
            var y = rect.Center.Y - size / 2;

            canvas.StrokeSize = stroke;
            canvas.StrokeLineCap = LineCap.Butt;
            canvas.StrokeColor = Color.FromArgb("#2A3140");
            canvas.DrawEllipse(x, y, size, size);

            if (_remaining > 0.001)
            {
                // MAUI arcs: 0° is 3 o'clock, angles counter-clockwise. Start at 12 o'clock (90°) and
                // keep the remaining share, so the empty part grows clockwise from the top.
                var sweep = (float)(360 * _remaining);
                canvas.StrokeColor = _colour;
                if (_remaining >= 0.999) canvas.DrawEllipse(x, y, size, size);
                else canvas.DrawArc(x, y, size, size, 90, 90 + sweep, clockwise: false, closed: false);
            }

            if (_switchAt is double s)
            {
                // The switch point, measured clockwise from the top like the drain.
                var angle = Math.PI / 2 - 2 * Math.PI * s;
                var r = size / 2;
                var cx = rect.Center.X; var cy = rect.Center.Y;
                canvas.StrokeColor = Color.FromArgb("#E6E1CF");
                canvas.StrokeSize = 4;
                canvas.DrawLine(cx + (float)((r - stroke) * Math.Cos(angle)), cy - (float)((r - stroke) * Math.Sin(angle)),
                                cx + (float)((r + stroke) * Math.Cos(angle)), cy - (float)((r + stroke) * Math.Sin(angle)));
            }
        }
    }

    /// <summary>
    /// The day's foe (user, 2026-11-13): a segmented health bar, one segment per exercise, draining
    /// set by set. Skipped exercises grey out rather than turn a penalty colour. Bind a new instance.
    /// </summary>
    public sealed class HealthBar : IDrawable
    {
        private readonly IReadOnlyList<HealthSegment> _segments;

        public HealthBar(IReadOnlyList<HealthSegment> segments) => _segments = segments;

        public static HealthBar Empty { get; } = new([]);

        public void Draw(ICanvas canvas, RectF rect)
        {
            const float gap = 3;
            canvas.FillColor = Color.FromArgb("#000000");
            canvas.FillRoundedRectangle(rect, 6);
            var inner = rect.Inflate(-3, -3);
            foreach (var s in _segments)
            {
                var x = inner.X + (float)(s.Start * inner.Width) + gap / 2;
                var w = (float)(s.Width * inner.Width) - gap;
                canvas.FillColor = Color.FromArgb("#2A3140");
                canvas.FillRectangle(x, inner.Y, w, inner.Height);
                if (s.Skipped)
                {
                    canvas.FillColor = Color.FromArgb("#4A4F57");
                    canvas.FillRectangle(x, inner.Y, w, inner.Height);
                }
                else if (s.Remaining > 0)
                {
                    canvas.FillColor = Color.FromArgb("#E05A5A");
                    canvas.FillRectangle(x, inner.Y, (float)(w * s.Remaining), inner.Height);
                    canvas.FillColor = Color.FromArgb("#F08A7A"); // a lit top edge, like the stones
                    canvas.FillRectangle(x, inner.Y, (float)(w * s.Remaining), 3);
                }
            }
        }
    }
}
