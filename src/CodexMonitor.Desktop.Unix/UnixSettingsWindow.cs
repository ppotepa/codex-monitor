using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CodexMonitor.Desktop.Unix;

// All values are edited in a dialog-local copy and applied only after Save.
internal sealed class UnixSettingsWindow : Window
{
    private readonly CheckBox _alerts;
    private readonly CheckBox _autoTokens;
    private readonly ComboBox _limits;
    private readonly ComboBox _tokens;
    private readonly ComboBox _agents;
    private readonly TextBox _warning;
    private readonly TextBox _critical;
    private readonly TextBlock _error = new();

    public UnixSettingsWindow(MonitorSettings settings)
    {
        Title = "Codex Monitor settings";
        Width = 370;
        Height = 465;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _alerts = new CheckBox { Content = "Usage alerts", IsChecked = settings.AlertsEnabled };
        _autoTokens = new CheckBox { Content = "Auto-refresh tokens while viewing", IsChecked = settings.AutoRefreshTokens };
        _warning = new TextBox { Text = settings.WarningThreshold.ToString(), Watermark = "Warning %" };
        _critical = new TextBox { Text = settings.CriticalThreshold.ToString(), Watermark = "Critical %" };
        _limits = Interval(settings.LimitsRefreshMinutes, [1, 5, 10, 15, 30, 60]);
        _tokens = Interval(settings.TokenRefreshMinutes, [5, 15, 30, 60, 120]);
        _agents = Interval(settings.AgentScanMinutes, [0, 15, 30, 60]);

        var form = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        form.Children.Add(_alerts);
        form.Children.Add(new TextBlock { Text = "Warning threshold (%)" });
        form.Children.Add(_warning);
        form.Children.Add(new TextBlock { Text = "Critical threshold (%)" });
        form.Children.Add(_critical);
        form.Children.Add(new TextBlock { Text = "Limits refresh (minutes)" });
        form.Children.Add(_limits);
        form.Children.Add(new TextBlock { Text = "Token refresh (minutes)" });
        form.Children.Add(_tokens);
        form.Children.Add(new TextBlock { Text = "Agent scan (minutes; 0 = disabled)" });
        form.Children.Add(_agents);
        form.Children.Add(_autoTokens);
        form.Children.Add(_error);
        var save = new Button { Content = "Save", HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) =>
        {
            if (!int.TryParse(_warning.Text, out var warning) ||
                !int.TryParse(_critical.Text, out var critical) ||
                warning < 1 || critical > 100 || warning >= critical)
            {
                _error.Text = "Thresholds must satisfy 1 <= warning < critical <= 100.";
                return;
            }
            settings.WarningThreshold = warning;
            settings.CriticalThreshold = critical;
            settings.AlertsEnabled = _alerts.IsChecked == true;
            settings.AutoRefreshTokens = _autoTokens.IsChecked == true;
            settings.LimitsRefreshMinutes = (int)(_limits.SelectedItem ?? 5);
            settings.TokenRefreshMinutes = (int)(_tokens.SelectedItem ?? 30);
            settings.AgentScanMinutes = (int)(_agents.SelectedItem ?? 30);
            try
            {
                settings.Save();
                Close(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _error.Text = ex.Message;
            }
        };
        form.Children.Add(save);
        Content = new ScrollViewer { Content = form };
    }

    private static ComboBox Interval(int current, int[] values)
    {
        var input = new ComboBox();
        foreach (var value in values) input.Items.Add(value);
        input.SelectedItem = values.Contains(current) ? current : values[0];
        return input;
    }
}
