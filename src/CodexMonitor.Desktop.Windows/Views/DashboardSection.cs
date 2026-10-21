namespace CodexMonitor;

// Reusable WinForms-only styling; not part of shared application contracts.
internal abstract class DashboardSection : Panel
{
    protected static readonly Color Background = Color.FromArgb(22, 27, 35);
    protected static readonly Color Card = Color.FromArgb(32, 39, 50);
    protected static readonly Color Muted = Color.FromArgb(155, 168, 185);

    protected DashboardSection()
    {
        SetBounds(18, 124, 384, 520);
        BackColor = Background;
    }

    protected void ConfigureLabel(Label label, int x, int y, int width, int height, float size = 9,
        FontStyle style = FontStyle.Regular, Color? backColor = null, Color? color = null, Control? parent = null)
    {
        label.SetBounds(x, y, width, height);
        label.Font = new Font("Segoe UI", size, style);
        label.BackColor = backColor ?? Background;
        label.ForeColor = color ?? Color.White;
        label.UseMnemonic = false;
        label.AutoEllipsis = true;
        (parent ?? this).Controls.Add(label);
    }

    protected static void StyleButton(Button button, string text, int x, int y, int width, Control parent)
    {
        button.Text = text;
        button.SetBounds(x, y, width, 32);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(77, 95, 113);
        button.BackColor = Card;
        button.ForeColor = Color.White;
        button.Cursor = Cursors.Hand;
        parent.Controls.Add(button);
    }
}
