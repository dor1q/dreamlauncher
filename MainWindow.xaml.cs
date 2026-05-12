using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DreamLauncher.Models;
using DreamLauncher.Services;
using Forms = System.Windows.Forms;
using MediaBrush = System.Windows.Media.Brush;

namespace DreamLauncher;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly BuildManifestService _buildManifestService = new();
    private readonly StatusService _statusService = new();
    private readonly LaunchService _launchService = new();
    private readonly DiscordAuthService _discordAuthService = new();
    private readonly DiscordSessionService _discordSessionService = new();
    private readonly DreamBackendAuthService _dreamBackendAuthService = new();
    private readonly ObservableCollection<BuildDefinition> _builds = [];
    private readonly ObservableCollection<string> _logs = [];
    private LauncherSettings _settings = LauncherSettings.Default;
    private DiscordSession? _discordSession;
    private LaunchState _launchState = LaunchState.Idle;

    public MainWindow()
    {
        InitializeComponent();
        BuildsListBox.ItemsSource = _builds;
        LogListBox.ItemsSource = _logs;
        ManifestPathTextBlock.Text = _buildManifestService.ManifestPath;
        SettingsPathTextBlock.Text = _settingsService.SettingsPath;
        SessionPathTextBlock.Text = _discordSessionService.SessionPath;
        SetLaunchState(LaunchState.Idle);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSettingsAsync();
        await LoadDiscordSessionAsync();
        await LoadBuildsAsync();
        AddLog("Launcher loaded.");
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _settingsService.LoadAsync();
        BackendUrlTextBox.Text = _settings.BackendUrl;
        GameServerHostTextBox.Text = _settings.GameServerHost;
        GameServerPortTextBox.Text = _settings.GameServerPort.ToString();
        DiscordClientIdTextBox.Text = _settings.DiscordClientId;
        DiscordClientSecretPasswordBox.Password = _settings.DiscordClientSecret;
        DiscordRedirectPortTextBox.Text = _settings.DiscordRedirectPort.ToString();
    }

    private async Task LoadDiscordSessionAsync()
    {
        _discordSession = await _discordSessionService.LoadAsync();
        UpdateDiscordAuthUi();
    }

    private async Task LoadBuildsAsync()
    {
        _builds.Clear();
        var manifest = await _buildManifestService.LoadAsync();

        foreach (var build in manifest.Builds)
        {
            _builds.Add(build);
        }

        BuildsListBox.SelectedIndex = _builds.Count > 0 ? 0 : -1;
        UpdateLaunchButton();
        AddLog($"Loaded {_builds.Count} build(s).");
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = ReadSettingsFromInputs();
            await _settingsService.SaveAsync(_settings);
            AddLog("Settings saved.");
        }
        catch (Exception ex)
        {
            AddError("Settings", ex);
        }
    }

    private async void LoginDiscord_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = ReadSettingsFromInputs();
            await _settingsService.SaveAsync(_settings);

            LoginDiscordButton.IsEnabled = false;
            AddLog("Opening Discord login.");

            _discordSession = await _discordAuthService.SignInAsync(_settings);
            await _discordSessionService.SaveAsync(_discordSession);
            AddLog($"Signed in as {_discordSession.User.DisplayName}.");
        }
        catch (Exception ex)
        {
            AddError("Discord login", ex);
        }
        finally
        {
            UpdateDiscordAuthUi();
        }
    }

    private async void LogoutDiscord_Click(object sender, RoutedEventArgs e)
    {
        await _discordSessionService.ClearAsync();
        _discordSession = null;
        UpdateDiscordAuthUi();
        AddLog("Signed out of Discord.");
    }

    private async void CheckStatus_Click(object sender, RoutedEventArgs e)
    {
        await CheckStatusAsync();
    }

    private async Task CheckStatusAsync()
    {
        try
        {
            _settings = ReadSettingsFromInputs();

            SetStatus(BackendStatusPill, BackendStatusText, "Backend", ServiceCheckResult.NotChecked);
            SetStatus(GameServerStatusPill, GameServerStatusText, "Game server", ServiceCheckResult.NotChecked);

            var backendTask = _statusService.CheckBackendAsync(_settings.BackendUrl);
            var gameServerTask = _statusService.CheckTcpAsync(_settings.GameServerHost, _settings.GameServerPort);

            await Task.WhenAll(backendTask, gameServerTask);

            SetStatus(BackendStatusPill, BackendStatusText, "Backend", backendTask.Result);
            SetStatus(GameServerStatusPill, GameServerStatusText, "Game server", gameServerTask.Result);
            AddLog("Status checked.");
        }
        catch (Exception ex)
        {
            AddError("Status", ex);
        }
    }

    private async void RefreshBuilds_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await LoadBuildsAsync();
        }
        catch (Exception ex)
        {
            AddError("Build manifest", ex);
        }
    }

    private async void AddBuildFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "Select the Fortnite build root folder",
                ShowNewFolderButton = false,
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK)
            {
                return;
            }

            var build = await _buildManifestService.AddExistingBuildAsync(dialog.SelectedPath);
            await LoadBuildsAsync();
            BuildsListBox.SelectedItem = _builds.FirstOrDefault(item => item.Id == build.Id);
            AddLog($"Imported build: {build.Name}");
        }
        catch (Exception ex)
        {
            AddError("Import build", ex);
        }
    }

    private void BuildsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateLaunchButton();

        if (BuildsListBox.SelectedItem is BuildDefinition build)
        {
            AddLog($"Selected build: {build.Name}");
        }
    }

    private async void OpenManifest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _buildManifestService.EnsureManifestAsync();
            _launchService.OpenInExplorer(_buildManifestService.ManifestPath);
        }
        catch (Exception ex)
        {
            AddError("Open manifest", ex);
        }
    }

    private void CloseGame_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetLaunchState(LaunchState.Closing);
            var closed = _launchService.CloseGameProcesses();
            AddLog(closed == 0 ? "No game processes were running." : $"Closed {closed} game process(es).");
            SetLaunchState(LaunchState.Idle);
        }
        catch (Exception ex)
        {
            SetLaunchState(LaunchState.Error);
            AddError("Close game", ex);
        }
    }

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (_discordSession is null || _discordSession.IsExpired)
        {
            AddLog("Launch blocked: Discord login is required.");
            UpdateDiscordAuthUi();
            return;
        }

        if (BuildsListBox.SelectedItem is not BuildDefinition build)
        {
            return;
        }

        try
        {
            SetLaunchState(LaunchState.Launching);
            LaunchButton.IsEnabled = false;
            _settings = ReadSettingsFromInputs();

            if (_launchService.IsGameRunning())
            {
                AddLog("Game process is already running; close it first if launch fails.");
            }

            AddLog("Requesting Dream exchange code.");

            var exchange = await _dreamBackendAuthService.CreateExchangeCodeAsync(_settings, _discordSession);
            var context = new LaunchContext
            {
                ExchangeCode = exchange.Code,
                AccountId = exchange.AccountId,
                DisplayName = exchange.DisplayName,
                DiscordId = _discordSession.User.Id
            };

            if (!build.UsesExchangeCode)
            {
                AddLog("Build arguments do not contain {exchangeCode}; game auth args may be missing.");
            }

            var executable = _launchService.Launch(build, context);
            AddLog($"Launched {executable}");
            SetLaunchState(LaunchState.Launched);
        }
        catch (Exception ex)
        {
            SetLaunchState(LaunchState.Error);
            AddError("Launch", ex);
        }
        finally
        {
            UpdateLaunchButton();
        }
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        _logs.Clear();
    }

    private void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var dialog = new Forms.SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "txt",
                FileName = $"dream-launcher-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Export launcher report"
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, BuildReport(), Encoding.UTF8);
            AddLog($"Report exported: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            AddError("Export report", ex);
        }
    }

    private LauncherSettings ReadSettingsFromInputs()
    {
        return LauncherSettings.FromInput(
            BackendUrlTextBox.Text,
            GameServerHostTextBox.Text,
            GameServerPortTextBox.Text,
            DiscordClientIdTextBox.Text,
            DiscordClientSecretPasswordBox.Password,
            DiscordRedirectPortTextBox.Text);
    }

    private void UpdateDiscordAuthUi()
    {
        var signedIn = _discordSession is not null && !_discordSession.IsExpired;
        var muted = (MediaBrush)FindResource("MutedBrush");
        var accent = (MediaBrush)FindResource("AccentBrush");
        var border = (MediaBrush)FindResource("BorderBrushColor");

        DiscordAuthPill.BorderBrush = signedIn ? accent : border;
        DiscordAuthTextBlock.Foreground = signedIn ? accent : muted;
        DiscordAuthTextBlock.Text = signedIn
            ? $"Discord: {_discordSession!.User.DisplayName}"
            : "Discord: signed out";

        LoginDiscordButton.IsEnabled = !signedIn;
        LogoutDiscordButton.IsEnabled = signedIn;
        UpdateLaunchButton();
    }

    private void UpdateLaunchButton()
    {
        LaunchButton.IsEnabled =
            BuildsListBox.SelectedItem is BuildDefinition &&
            _discordSession is not null &&
            !_discordSession.IsExpired &&
            _launchState is not LaunchState.Launching and not LaunchState.Closing;
    }

    private void AddLog(string message)
    {
        _logs.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
    }

    private void AddError(string area, Exception ex)
    {
        AddLog($"{area} error: {FriendlyError(ex)}");
    }

    private void SetLaunchState(LaunchState state)
    {
        _launchState = state;

        var muted = (MediaBrush)FindResource("MutedBrush");
        var accent = (MediaBrush)FindResource("AccentBrush");
        var danger = (MediaBrush)FindResource("DangerBrush");
        var border = (MediaBrush)FindResource("BorderBrushColor");

        LaunchStatePill.BorderBrush = state switch
        {
            LaunchState.Launched => accent,
            LaunchState.Error => danger,
            LaunchState.Launching or LaunchState.Closing => accent,
            _ => border
        };

        LaunchStateText.Foreground = state switch
        {
            LaunchState.Launched => accent,
            LaunchState.Error => danger,
            LaunchState.Launching or LaunchState.Closing => accent,
            _ => muted
        };

        LaunchStateText.Text = state switch
        {
            LaunchState.Launching => "Launch: launching",
            LaunchState.Launched => "Launch: launched",
            LaunchState.Closing => "Launch: closing",
            LaunchState.Error => "Launch: error",
            _ => "Launch: idle"
        };

        UpdateLaunchButton();
    }

    private string BuildReport()
    {
        var selectedBuild = BuildsListBox.SelectedItem as BuildDefinition;
        var report = new StringBuilder();

        report.AppendLine("Dream Launcher Report");
        report.AppendLine($"Created: {DateTimeOffset.Now:O}");
        report.AppendLine($"Launch state: {_launchState}");
        report.AppendLine($"Backend URL: {_settings.BackendUrl}");
        report.AppendLine($"Game server: {_settings.GameServerHost}:{_settings.GameServerPort}");
        report.AppendLine($"Discord signed in: {_discordSession is not null && !_discordSession.IsExpired}");

        if (_discordSession is not null)
        {
            report.AppendLine($"Discord user: {_discordSession.User.DisplayName} ({_discordSession.User.Id})");
            report.AppendLine($"Discord token expires: {_discordSession.ExpiresAtUtc:O}");
        }

        if (selectedBuild is not null)
        {
            report.AppendLine($"Selected build: {selectedBuild.Name}");
            report.AppendLine($"Build path: {selectedBuild.Path}");
            report.AppendLine($"Build executable: {selectedBuild.Executable}");
        }

        report.AppendLine();
        report.AppendLine("Logs:");

        foreach (var item in _logs.Reverse())
        {
            report.AppendLine(item);
        }

        return report.ToString();
    }

    private static string FriendlyError(Exception ex)
    {
        return ex switch
        {
            FileNotFoundException fileNotFound => $"Required file is missing: {fileNotFound.FileName}",
            TimeoutException => "The operation timed out. Check Discord/backend connectivity and try again.",
            HttpRequestException => "Network request failed. Check backend URL, internet connection, or service availability.",
            InvalidOperationException invalid when invalid.Message.Contains("account_not_registered", StringComparison.OrdinalIgnoreCase) =>
                "This Discord account is not registered in Dream backend.",
            InvalidOperationException invalid when invalid.Message.Contains("Backend exchange failed", StringComparison.OrdinalIgnoreCase) =>
                $"Backend refused exchange-code login. Details: {invalid.Message}",
            InvalidOperationException invalid => invalid.Message,
            _ => ex.Message
        };
    }

    private void SetStatus(Border pill, TextBlock text, string label, ServiceCheckResult result)
    {
        var muted = (MediaBrush)FindResource("MutedBrush");
        var accent = (MediaBrush)FindResource("AccentBrush");
        var danger = (MediaBrush)FindResource("DangerBrush");
        var border = (MediaBrush)FindResource("BorderBrushColor");

        pill.BorderBrush = result.State switch
        {
            ServiceState.Online => accent,
            ServiceState.Offline => danger,
            _ => border
        };

        text.Foreground = result.State switch
        {
            ServiceState.Online => accent,
            ServiceState.Offline => danger,
            _ => muted
        };

        text.Text = result.State switch
        {
            ServiceState.Online => $"{label}: online ({result.LatencyMs} ms{FormatStatusCode(result)})",
            ServiceState.Offline => $"{label}: offline ({result.Error})",
            _ => $"{label}: checking..."
        };
    }

    private static string FormatStatusCode(ServiceCheckResult result)
    {
        return result.StatusCode is null ? string.Empty : $", HTTP {result.StatusCode}";
    }
}
