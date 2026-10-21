namespace CodexMonitor;

internal sealed class UsagePopup : Form
{
    private readonly Label _title = new();
    private readonly Label _status = new();
    private readonly Label _windows = new();
    private readonly Label _error = new();
    private readonly Label _updated = new();
    private readonly Button _refresh = new();
    private readonly Button _install = new();
    private readonly ToolTip _errorTip = new();

    public event EventHandler? RefreshRequested;
    public event EventHandler? InstallRequested;

    public UsagePopup()
    {
        Text = "Codex Monitor";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(300, 245);
        BackColor = Color.FromArgb(28, 32, 40);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9);

        ConfigureLabel(_title, 12, 10, 276, 26, 12, FontStyle.Bold);
        ConfigureLabel(_status, 12, 40, 276, 26);
        ConfigureLabel(_windows, 12, 72, 276, 75);
        ConfigureLabel(_error, 12, 150, 276, 38, 8);
        ConfigureLabel(_updated, 12, 191, 276, 18, 8);

        _refresh.Text = "Odśwież";
        _refresh.SetBounds(12, 214, 88, 25);
        _refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _install.Text = "Zainstaluj CLI";
        _install.SetBounds(108, 214, 140, 25);
        _install.Click += (_, _) => InstallRequested?.Invoke(this, EventArgs.Empty);
        Controls.AddRange([_refresh, _install]);
        Deactivate += (_, _) => Hide();
    }

    public void ShowSnapshot(UsageSnapshot? snapshot, string? error, bool busy, CliProblem cliProblem)
    {
        _title.Text = snapshot?.Plan is { Length: > 0 } plan ? $"Codex · {plan}" : "Codex Monitor";
        _status.Text = busy ? "Pobieranie danych…" : error is not null ? "Problem z odczytem" : "Zużycie limitów";
        _status.ForeColor = error is null ? Color.White : Color.Salmon;
        _windows.Text = snapshot is null ? "Brak danych" : string.Join(Environment.NewLine,
            snapshot.Windows.Select(w => $"{WindowName(w.Name)}: {w.UsedPercent:0}% użyte" +
                (w.ResetsAt is { } reset ? $"  ·  reset {reset.ToLocalTime():dd.MM HH:mm}" : "")));
        _error.Text = error is null ? "" : error.Length > 105 ? error[..105] + "…" : error;
        _errorTip.SetToolTip(_error, error);
        _error.ForeColor = Color.Salmon;
        _updated.Text = snapshot?.RetrievedAt is { } at
            ? $"Dane z {at.ToLocalTime():dd.MM.yyyy HH:mm}" : "";
        _updated.ForeColor = Color.Silver;
        _refresh.Enabled = !busy;
        _install.Visible = cliProblem != CliProblem.None;
        _install.Text = cliProblem == CliProblem.Broken ? "Napraw CLI" : "Zainstaluj CLI";
        _install.Enabled = !busy;
    }

    public void ShowNearCursor()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(
            Math.Clamp(Cursor.Position.X - Width / 2, area.Left, area.Right - Width),
            Math.Clamp(Cursor.Position.Y - Height - 8, area.Top, area.Bottom - Height));
        Show();
        Activate();
    }

    private static string WindowName(string name) => name.ToLowerInvariant() switch
    {
        "5-hour" or "5h" => "5 godzin",
        "weekly" => "Tydzień",
        "daily" => "Dzień",
        _ => name
    };

    private void ConfigureLabel(Label label, int x, int y, int width, int height, int size = 9, FontStyle style = FontStyle.Regular)
    {
        label.SetBounds(x, y, width, height);
        label.Font = new Font("Segoe UI", size, style);
        label.ForeColor = Color.White;
        Controls.Add(label);
    }
}
