namespace CodexMonitor;

internal sealed class MonitorContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly UsagePopup _popup;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _installItem;
    private readonly ToolStripMenuItem _startupItem;
    private Icon? _icon;
    private UsageSnapshot? _snapshot;
    private string? _error;
    private bool _busy;
    private bool _missingCli;

    public MonitorContext()
    {
        _popup = new UsagePopup();
        _popup.RefreshRequested += (_, _) => _ = RefreshAsync();
        _popup.InstallRequested += (_, _) => _ = InstallAsync();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Pokaż limity", null, (_, _) => ShowPopup());
        menu.Items.Add("Odśwież", null, (_, _) => _ = RefreshAsync());
        _installItem = new ToolStripMenuItem("Zainstaluj codex-cli-usage", null, (_, _) => _ = InstallAsync());
        menu.Items.Add(_installItem);
        _startupItem = new ToolStripMenuItem("Uruchamiaj z Windows", null, (_, _) => ToggleAutoStart());
        _startupItem.Enabled = AutoStart.IsPackagedExecutable;
        _startupItem.Checked = AutoStart.IsPackagedExecutable && AutoStart.Enabled;
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Zamknij", null, (_, _) => ExitThread());

        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowPopup(); };
        _timer = new System.Windows.Forms.Timer { Interval = 300_000 };
        _timer.Tick += (_, _) => _ = RefreshAsync();
        _timer.Start();
        UpdateView();
        Application.Idle += StartFirstRefresh;
    }

    private void StartFirstRefresh(object? sender, EventArgs e)
    {
        Application.Idle -= StartFirstRefresh;
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        _missingCli = CliService.FindExecutable("codex-cli-usage.exe") is null;
        UpdateView();
        try
        {
            _snapshot = await CliService.ReadAsync(CancellationToken.None);
            _error = _snapshot.Status == "stale" ? "Dane są nieaktualne; CLI nie mogło pobrać świeżego wyniku." : null;
            _missingCli = false;
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            _missingCli = ex is FileNotFoundException;
        }
        finally
        {
            _busy = false;
            UpdateView();
        }
    }

    private async Task InstallAsync()
    {
        if (_busy) return;
        if (CliService.FindExecutable("codex-cli-usage.exe") is not null)
        {
            await RefreshAsync();
            return;
        }
        _busy = true;
        _error = null;
        UpdateView();
        try
        {
            await CliService.InstallAsync(CancellationToken.None);
            _missingCli = false;
        }
        catch (Exception ex) { _error = ex.Message; }
        finally
        {
            _busy = false;
            UpdateView();
        }
        if (_error is null) await RefreshAsync();
        else ShowPopup();
    }

    private void ToggleAutoStart()
    {
        try
        {
            AutoStart.Enabled = !_startupItem.Checked;
            _startupItem.Checked = AutoStart.Enabled;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Codex Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ShowPopup()
    {
        UpdatePopup();
        _popup.ShowNearCursor();
    }

    private void UpdateView()
    {
        var main = _snapshot?.MainWindow;
        var oldIcon = _icon;
        _icon = TrayIcon.Create(main?.UsedPercent, _error is not null);
        _tray.Icon = _icon;
        oldIcon?.Dispose();
        var summary = main is null ? "Codex: brak danych" : $"Codex: {main.UsedPercent:0}% użyte ({main.Name})";
        _tray.Text = summary.Length > 63 ? summary[..63] : summary;
        _installItem.Visible = _missingCli;
        UpdatePopup();
    }

    private void UpdatePopup() => _popup.ShowSnapshot(_snapshot, _error, _busy, _missingCli);

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _icon?.Dispose();
        _popup.Dispose();
        base.ExitThreadCore();
    }
}
