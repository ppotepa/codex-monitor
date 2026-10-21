using System.Drawing.Drawing2D;
using System.Globalization;

namespace CodexMonitor;

internal sealed class TokenUsageChart : Control
{
    private IReadOnlyList<(DateOnly Date, long? Tokens)> _days = [];
    private readonly ToolTip _tip = new() { InitialDelay = 350, ReshowDelay = 100 };
    private int _hovered = -1;

    public TokenUsageChart()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(32, 39, 50);
        ForeColor = Color.FromArgb(83, 207, 192);
        Font = new Font("Segoe UI", 9);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.Chart;
        AccessibleDescription = "Seven calendar day token chart. Use the left and right arrow keys to select a day.";
    }

    public void SetBuckets(IReadOnlyList<DailyTokenUsage> buckets)
    {
        var lookup = buckets
            .GroupBy(x => x.Date).ToDictionary(group => group.Key, group => (long?)group.Last().Tokens);
        var latestDate = lookup.Keys.DefaultIfEmpty(DateOnly.FromDateTime(DateTime.Today)).Max();
        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = latestDate.AddDays(offset - 6);
            return (date, lookup.TryGetValue(date, out var tokens) ? tokens : null);
        }).ToArray();
        if (_days.SequenceEqual(days)) return;
        _days = days;
        _hovered = -1;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var baseline = Height - 27;
        using var axis = new Pen(Color.FromArgb(65, 75, 88));
        g.DrawLine(axis, 12, baseline, Width - 12, baseline);
        if (_days.All(x => x.Tokens is null))
        {
            TextRenderer.DrawText(g, "No daily token history reported yet", Font,
                new Rectangle(8, 8, Width - 16, Math.Max(1, Height - 35)), Color.FromArgb(155, 168, 185),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        var top = LogicalToDeviceUnits(48);
        var plotHeight = Math.Max(1, baseline - top);
        var max = Math.Max(1L, _days.Where(x => x.Tokens is not null).Max(x => x.Tokens!.Value));
        var slot = (Width - 30f) / _days.Count;
        var barWidth = Math.Min(28f, slot * 0.52f);
        using var normal = new SolidBrush(Color.FromArgb(83, 207, 192));
        using var hovered = new SolidBrush(Color.FromArgb(139, 231, 215));
        using var text = new SolidBrush(Color.FromArgb(180, 191, 205));
        for (var i = 0; i < _days.Count; i++)
        {
            var item = _days[i];
            var x = 15 + slot * i + (slot - barWidth) / 2;
            var valueY = baseline - LogicalToDeviceUnits(22);
            if (item.Tokens is { } count)
            {
                var barHeight = count == 0 ? 2f : Math.Max(3f, (float)(count / (double)max * plotHeight));
                var rectangle = new RectangleF(x, baseline - barHeight, barWidth, barHeight);
                valueY = (int)rectangle.Top - LogicalToDeviceUnits(22);
                using var path = RoundedBar(rectangle, Math.Min(5, barWidth / 2));
                g.FillPath(i == _hovered ? hovered : normal, path);
                if (Focused && i == _hovered)
                {
                    using var focus = new Pen(Color.White, 1);
                    g.DrawPath(focus, path);
                }
            }
            var value = item.Tokens is { } tokens ? CompactCount(tokens) : "—";
            TextRenderer.DrawText(g, value, Font,
                new Rectangle((int)(15 + slot * i), valueY, (int)slot, LogicalToDeviceUnits(21)),
                text.Color, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            var label = item.Date.ToString("ddd", CultureInfo.CurrentCulture);
            var labelRect = new Rectangle((int)(15 + slot * i), baseline + 5, (int)slot, 18);
            TextRenderer.DrawText(g, label, Font, labelRect, text.Color, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }
        if (Focused && _hovered >= 0 && _hovered < _days.Count)
        {
            var selected = _days[_hovered];
            var description = selected.Tokens is { } count
                ? $"{selected.Date.ToString("MMM d", CultureInfo.CurrentCulture)} · {count.ToString("N0", CultureInfo.CurrentCulture)} tokens"
                : $"{selected.Date.ToString("MMM d", CultureInfo.CurrentCulture)} · no data";
            TextRenderer.DrawText(g, description, Font, new Rectangle(12, 1, Width - 24, 17),
                Color.FromArgb(195, 207, 220), TextFormatFlags.Left | TextFormatFlags.NoPadding);
        }
    }

    private static string CompactCount(long count) => count switch
    {
        >= 1_000_000_000 => $"{count / 1_000_000_000d:0.#}B",
        >= 1_000_000 => $"{count / 1_000_000d:0.#}M",
        >= 1_000 => $"{count / 1_000d:0.#}k",
        _ => count.ToString(CultureInfo.CurrentCulture)
    };

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var slot = (Width - 30f) / Math.Max(1, _days.Count);
        var index = e.X < 15 || e.X >= Width - 15 ? -1 : (int)((e.X - 15) / slot);
        if (index < 0 || index >= _days.Count)
        {
            if (_hovered != -1) { _hovered = -1; Invalidate(); }
            _tip.SetToolTip(this, "");
            return;
        }
        if (_hovered != index)
        {
            _hovered = index;
            var item = _days[index];
            _tip.SetToolTip(this, item.Tokens is { } count
                ? $"{item.Date.ToString("D", CultureInfo.CurrentCulture)}: {count.ToString("N0", CultureInfo.CurrentCulture)} tokens"
                : $"{item.Date.ToString("D", CultureInfo.CurrentCulture)}: no data reported");
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = -1;
        _tip.SetToolTip(this, "");
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_days.Count > 0)
        {
            _hovered = e.KeyCode switch
            {
                Keys.Left => Math.Max(0, (_hovered < 0 ? 0 : _hovered) - 1),
                Keys.Right => Math.Min(_days.Count - 1, (_hovered < 0 ? -1 : _hovered) + 1),
                Keys.Home => 0,
                Keys.End => _days.Count - 1,
                _ => _hovered
            };
            if (e.KeyCode is Keys.Left or Keys.Right or Keys.Home or Keys.End)
            {
                var item = _days[_hovered];
                AccessibleDescription = item.Tokens is { } selectedTokens
                    ? $"Selected {item.Date.ToString("D", CultureInfo.CurrentCulture)}: {selectedTokens.ToString("N0", CultureInfo.CurrentCulture)} tokens. Use left and right arrow keys to change day."
                    : $"Selected {item.Date.ToString("D", CultureInfo.CurrentCulture)}: no data reported. Use left and right arrow keys to change day.";
                _tip.SetToolTip(this, item.Tokens is { } count
                    ? $"{item.Date.ToString("D", CultureInfo.CurrentCulture)}: {count.ToString("N0", CultureInfo.CurrentCulture)} tokens"
                    : $"{item.Date.ToString("D", CultureInfo.CurrentCulture)}: no data reported");
                Invalidate();
                e.Handled = true;
            }
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _hovered = _days.Count == 0 ? -1 : 0;
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    private static GraphicsPath RoundedBar(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (rect.Height <= radius * 2)
        {
            path.AddRectangle(rect);
            return path;
        }
        path.AddArc(rect.Left, rect.Top, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Top, radius * 2, radius * 2, 270, 90);
        path.AddLine(rect.Right, rect.Top + radius, rect.Right, rect.Bottom);
        path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
        path.AddLine(rect.Left, rect.Bottom, rect.Left, rect.Top + radius);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
