using System.Windows.Input;

namespace CodexMonitor.UI.ViewModels;

// All inputs are local until Save; UI never writes platform settings directly.
public sealed class SettingsViewModel : BindableBase
{
    private readonly MonitorSettings _settings;
    private readonly IStartupService _startup;
    private string _warningText;
    private string _criticalText;
    private bool _alertsEnabled;
    private bool _autoRefreshTokens;
    private bool _startAtLogin;
    private int _limitsInterval;
    private int _tokensInterval;
    private int _agentsInterval;
    private string _errorText = "";
    private bool _hasError;

    public string WarningText { get => _warningText; set => Set(ref _warningText, value); }
    public string CriticalText { get => _criticalText; set => Set(ref _criticalText, value); }
    public bool AlertsEnabled { get => _alertsEnabled; set => Set(ref _alertsEnabled, value); }
    public bool AutoRefreshTokens { get => _autoRefreshTokens; set => Set(ref _autoRefreshTokens, value); }
    public bool StartAtLogin { get => _startAtLogin; set => Set(ref _startAtLogin, value); }
    public bool CanConfigureStartup => _startup.CanConfigure;
    public string StartupLabel => _startup.MenuLabel;
    public int LimitsInterval { get => _limitsInterval; set => Set(ref _limitsInterval, value); }
    public int TokensInterval { get => _tokensInterval; set => Set(ref _tokensInterval, value); }
    public int AgentsInterval { get => _agentsInterval; set => Set(ref _agentsInterval, value); }
    public string ErrorText { get => _errorText; private set => Set(ref _errorText, value); }
    public bool HasError { get => _hasError; private set => Set(ref _hasError, value); }

    public IReadOnlyList<int> LimitIntervals { get; } = [1, 5, 10, 15, 30, 60];
    public IReadOnlyList<int> TokenIntervals { get; } = [5, 15, 30, 60, 120];
    public IReadOnlyList<int> AgentIntervals { get; } = [0, 15, 30, 60];
    public ICommand SaveCommand { get; }
    public event Action? Saved;

    internal SettingsViewModel(MonitorSettings settings, IStartupService startup)
    {
        _settings = settings;
        _startup = startup;
        _warningText = settings.WarningThreshold.ToString();
        _criticalText = settings.CriticalThreshold.ToString();
        _alertsEnabled = settings.AlertsEnabled;
        _autoRefreshTokens = settings.AutoRefreshTokens;
        _startAtLogin = startup.CanConfigure && startup.Enabled;
        _limitsInterval = settings.LimitsRefreshMinutes;
        _tokensInterval = settings.TokenRefreshMinutes;
        _agentsInterval = settings.AgentScanMinutes;
        SaveCommand = new UiCommand(Save);
    }

    private void Save()
    {
        if (!int.TryParse(WarningText, out var warning) ||
            !int.TryParse(CriticalText, out var critical) ||
            warning is < 1 or > 99 || critical is < 2 or > 100 || warning >= critical ||
            !LimitIntervals.Contains(LimitsInterval) ||
            !TokenIntervals.Contains(TokensInterval) ||
            !AgentIntervals.Contains(AgentsInterval))
        {
            ErrorText = "Invalid thresholds or refresh interval. Warning must be below critical.";
            HasError = true;
            return;
        }

        var original = (
            _settings.WarningThreshold, _settings.CriticalThreshold, _settings.AlertsEnabled,
            _settings.AutoRefreshTokens, _settings.LimitsRefreshMinutes,
            _settings.TokenRefreshMinutes, _settings.AgentScanMinutes);
        var originalStartup = _startup.CanConfigure && _startup.Enabled;

        _settings.WarningThreshold = warning;
        _settings.CriticalThreshold = critical;
        _settings.AlertsEnabled = AlertsEnabled;
        _settings.AutoRefreshTokens = AutoRefreshTokens;
        _settings.LimitsRefreshMinutes = LimitsInterval;
        _settings.TokenRefreshMinutes = TokensInterval;
        _settings.AgentScanMinutes = AgentsInterval;
        try
        {
            if (_startup.CanConfigure && _startup.Enabled != StartAtLogin)
                _startup.Enabled = StartAtLogin;
            _settings.Save();
            ErrorText = "";
            HasError = false;
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            (_settings.WarningThreshold, _settings.CriticalThreshold, _settings.AlertsEnabled,
                _settings.AutoRefreshTokens, _settings.LimitsRefreshMinutes,
                _settings.TokenRefreshMinutes, _settings.AgentScanMinutes) = original;
            if (_startup.CanConfigure && _startup.Enabled != originalStartup)
            {
                try { _startup.Enabled = originalStartup; }
                catch (Exception rollbackError) { ex = new AggregateException(ex, rollbackError); }
            }
            ErrorText = ex.Message;
            HasError = true;
        }
    }
}
