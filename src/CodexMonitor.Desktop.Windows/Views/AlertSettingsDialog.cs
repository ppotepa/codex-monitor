namespace CodexMonitor;

internal sealed class AlertSettingsDialog : Form
{
    private sealed record RefreshOption(string Label, int Minutes);

    private readonly NumericUpDown _warning = new() { Minimum = 1, Maximum = 99 };
    private readonly NumericUpDown _critical = new() { Minimum = 2, Maximum = 100 };
    private readonly ComboBox _limitsRefresh = CreateCombo();
    private readonly ComboBox _tokensRefresh = CreateCombo();
    private readonly ComboBox _agentScan = CreateCombo();
    private readonly CheckBox _autoTokens = new() { Text = "Refresh token usage while the Tokens view is open", AutoSize = true };
    private readonly CheckBox _alerts = new() { Text = "Enable usage alerts", AutoSize = true };
    private readonly CheckBox _startup = new() { Text = "Start with Windows", AutoSize = true };

    public int WarningThreshold => (int)_warning.Value;
    public int CriticalThreshold => (int)_critical.Value;
    public int LimitsRefreshMinutes => SelectedMinutes(_limitsRefresh);
    public int TokenRefreshMinutes => SelectedMinutes(_tokensRefresh);
    public int AgentScanMinutes => SelectedMinutes(_agentScan);
    public bool AutoRefreshTokens => _autoTokens.Checked;
    public bool AlertsEnabled => _alerts.Checked;
    public bool StartWithWindows => _startup.Checked;

    public AlertSettingsDialog(int warning, int critical, int limitsRefreshMinutes, int tokenRefreshMinutes,
        int agentScanMinutes, bool autoRefreshTokens, bool alertsEnabled, bool startWithWindows, bool canConfigureStartup)
    {
        Text = "Codex Monitor settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(470, 470);
        Font = new Font("Segoe UI", 10);

        var refreshGroup = new GroupBox { Text = "Smart refresh", Bounds = new Rectangle(16, 12, 438, 182) };
        refreshGroup.Controls.Add(new Label { Text = "Usage limits", Bounds = new Rectangle(16, 28, 175, 25) });
        refreshGroup.Controls.Add(new Label { Text = "Token usage", Bounds = new Rectangle(16, 70, 175, 25) });
        refreshGroup.Controls.Add(new Label { Text = "AI agent detection", Bounds = new Rectangle(16, 112, 175, 25) });
        ConfigureCombo(_limitsRefresh, refreshGroup, 200, 24,
            [new("1 minute", 1), new("5 minutes (recommended)", 5), new("10 minutes", 10), new("15 minutes", 15), new("30 minutes", 30), new("60 minutes", 60)], limitsRefreshMinutes);
        ConfigureCombo(_tokensRefresh, refreshGroup, 200, 66,
            [new("5 minutes", 5), new("15 minutes", 15), new("30 minutes (recommended)", 30), new("60 minutes", 60), new("120 minutes", 120)], tokenRefreshMinutes);
        ConfigureCombo(_agentScan, refreshGroup, 200, 108,
            [new("On startup only", 0), new("Every 15 minutes", 15), new("Every 30 minutes (recommended)", 30), new("Every 60 minutes", 60)], agentScanMinutes);
        _autoTokens.SetBounds(16, 145, 400, 25);
        _autoTokens.Checked = autoRefreshTokens;
        refreshGroup.Controls.Add(_autoTokens);
        Controls.Add(refreshGroup);

        var alertGroup = new GroupBox { Text = "Alerts", Bounds = new Rectangle(16, 204, 438, 128) };
        alertGroup.Controls.Add(new Label { Text = "Warning at", Bounds = new Rectangle(16, 29, 145, 28) });
        alertGroup.Controls.Add(new Label { Text = "Critical at", Bounds = new Rectangle(16, 67, 145, 28) });
        _warning.SetBounds(175, 26, 92, 28);
        _critical.SetBounds(175, 64, 92, 28);
        _warning.Value = Math.Clamp(warning, 1, 99);
        _critical.Value = Math.Clamp(critical, 2, 100);
        alertGroup.Controls.Add(_warning);
        alertGroup.Controls.Add(_critical);
        _alerts.SetBounds(286, 30, 135, 25);
        _alerts.Checked = alertsEnabled;
        alertGroup.Controls.Add(_alerts);
        Controls.Add(alertGroup);

        _startup.SetBounds(18, 348, 280, 25);
        _startup.Checked = startWithWindows;
        _startup.Enabled = canConfigureStartup;
        Controls.Add(_startup);
        Controls.Add(new Label { Text = "Token reads are less frequent by default because they may wake the CLI.", ForeColor = Color.DimGray, Bounds = new Rectangle(18, 382, 430, 25) });

        var save = new Button { Text = "Save", DialogResult = DialogResult.None, Bounds = new Rectangle(346, 425, 104, 32) };
        save.Click += (_, _) =>
        {
            if (WarningThreshold >= CriticalThreshold)
            {
                MessageBox.Show(this, "Critical must be higher than warning.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(save);
        AcceptButton = save;
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(18, 425, 104, 32) };
        Controls.Add(cancel);
        CancelButton = cancel;
    }

    private static ComboBox CreateCombo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, FormattingEnabled = true };

    private static void ConfigureCombo(ComboBox combo, Control parent, int x, int y, IReadOnlyList<RefreshOption> options, int selectedMinutes)
    {
        combo.SetBounds(x, y, 220, 29);
        combo.Items.AddRange(options.Cast<object>().ToArray());
        combo.DisplayMember = nameof(RefreshOption.Label);
        combo.SelectedItem = options.FirstOrDefault(option => option.Minutes == selectedMinutes) ?? options[0];
        parent.Controls.Add(combo);
    }

    private static int SelectedMinutes(ComboBox combo) => combo.SelectedItem is RefreshOption option ? option.Minutes : 5;
}
