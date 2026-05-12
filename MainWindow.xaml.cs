using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DreamLauncher.Models;
using DreamLauncher.Services;

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

    public MainWindow()
    {
        InitializeComponent();
        BuildsListBox.ItemsSource = _builds;
        LogListBox.ItemsSource = _logs;
        ManifestPathTextBlock.Text = _buildManifestService.ManifestPath;
        SettingsPathTextBlock.Text = _settingsService.SettingsPath;
        SessionPathTextBlock.Text = _discordSessionService.SessionPath;
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
            AddLog($"Settings error: {ex.Message}");
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
            AddLog($"Discord login error: {ex.Message}");
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
            AddLog($"Status error: {ex.Message}");
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
            AddLog($"Build manifest error: {ex.Message}");
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
            AddLog($"Open manifest error: {ex.Message}");
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
            LaunchButton.IsEnabled = false;
            _settings = ReadSettingsFromInputs();
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
        }
        catch (Exception ex)
        {
            AddLog($"Launch error: {ex.Message}");
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
        var muted = (Brush)FindResource("MutedBrush");
        var accent = (Brush)FindResource("AccentBrush");
        var border = (Brush)FindResource("BorderBrushColor");

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
            !_discordSession.IsExpired;
    }

    private void AddLog(string message)
    {
        _logs.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
    }

    private void SetStatus(Border pill, TextBlock text, string label, ServiceCheckResult result)
    {
        var muted = (Brush)FindResource("MutedBrush");
        var accent = (Brush)FindResource("AccentBrush");
        var danger = (Brush)FindResource("DangerBrush");
        var border = (Brush)FindResource("BorderBrushColor");

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
