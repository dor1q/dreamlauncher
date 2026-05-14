using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DreamLauncher.Models;
using DreamLauncher.Services;
using Forms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace DreamLauncher;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly BuildManifestService _buildManifestService = new();
    private readonly BuildVerificationService _buildVerificationService = new();
    private readonly StatusService _statusService = new();
    private readonly LaunchService _launchService = new();
    private readonly DiscordAuthService _discordAuthService = new();
    private readonly DiscordSessionService _discordSessionService = new();
    private readonly DreamBackendAuthService _dreamBackendAuthService = new();
    private readonly ObservableCollection<BuildDefinition> _builds = [];
    private readonly ObservableCollection<string> _logs = [];
    private readonly ObservableCollection<BuildVerificationItem> _verificationResults = [];
    private readonly ObservableCollection<LauncherServiceStatus> _backendServices = [];
    private LauncherSettings _settings = LauncherSettings.Default;
    private DiscordSession? _discordSession;
    private LaunchState _launchState = LaunchState.Idle;
    private string _activePage = "Home";

    public MainWindow()
    {
        InitializeComponent();
        BuildsListBox.ItemsSource = _builds;
        LogListBox.ItemsSource = _logs;
        VerificationResultsListBox.ItemsSource = _verificationResults;
        BackendServicesListBox.ItemsSource = _backendServices;
        ManifestPathTextBlock.Text = _buildManifestService.ManifestPath;
        SettingsPathTextBlock.Text = _settingsService.SettingsPath;
        SessionPathTextBlock.Text = _discordSessionService.SessionPath;
        SetLaunchState(LaunchState.Idle);
        ShowPage("Home");
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
        DiscordRedirectPortTextBox.Text = _settings.DiscordRedirectPort.ToString();
        ContentDirectoryTextBox.Text = _settings.ContentDirectory;
        AutoDownloadCheckBox.IsChecked = _settings.AutoDownload;
        DetailedDownloadsCheckBox.IsChecked = _settings.DetailedDownloads;
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
        UpdateBuildSummary();
        AddLog($"Loaded {_builds.Count} build(s).");
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = ReadSettingsFromInputs();
            await _settingsService.SaveAsync(_settings);
            UpdateBuildSummary();
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
            UpdateBackendServices(backendTask.Result);
            UpdateHomeStatusSummary(backendTask.Result, gameServerTask.Result);
            AddStatusSummary("Backend services", backendTask.Result);
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
        UpdateBuildSummary();

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

    private void VerifySelectedBuild_Click(object sender, RoutedEventArgs e)
    {
        if (BuildsListBox.SelectedItem is not BuildDefinition build)
        {
            AddLog("Verify skipped: select a build first.");
            ShowPage("Downloads");
            return;
        }

        try
        {
            var result = _buildVerificationService.Verify(build);
            _verificationResults.Clear();

            foreach (var item in result.Items)
            {
                _verificationResults.Add(item);
            }

            DownloadStatusText.Text = result.Summary;
            DownloadProgressBar.Value = result.CanLaunch ? 100 : 45;
            AddLog($"Verify: {result.Summary}");
            ShowPage("Downloads");
        }
        catch (Exception ex)
        {
            AddError("Verify build", ex);
        }
    }

    private void OpenBuildFolder_Click(object sender, RoutedEventArgs e)
    {
        if (BuildsListBox.SelectedItem is not BuildDefinition build)
        {
            AddLog("Open folder skipped: select a build first.");
            return;
        }

        try
        {
            _launchService.OpenInExplorer(build.Path);
        }
        catch (Exception ex)
        {
            AddError("Open folder", ex);
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

    private void BrowseContentDirectory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "Select the Dream content directory",
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true
            };

            if (Directory.Exists(ContentDirectoryTextBox.Text))
            {
                dialog.SelectedPath = ContentDirectoryTextBox.Text;
            }

            if (dialog.ShowDialog() == Forms.DialogResult.OK)
            {
                ContentDirectoryTextBox.Text = dialog.SelectedPath;
                AddLog($"Content directory selected: {dialog.SelectedPath}");
            }
        }
        catch (Exception ex)
        {
            AddError("Content directory", ex);
        }
    }

    private LauncherSettings ReadSettingsFromInputs()
    {
        return LauncherSettings.FromInput(
            BackendUrlTextBox.Text,
            GameServerHostTextBox.Text,
            GameServerPortTextBox.Text,
            DiscordRedirectPortTextBox.Text,
            ContentDirectoryTextBox.Text,
            AutoDownloadCheckBox.IsChecked == true,
            DetailedDownloadsCheckBox.IsChecked == true);
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string page })
        {
            ShowPage(page);
        }
    }

    private void Donate_Click(object sender, RoutedEventArgs e)
    {
        AddLog("Donate page is not connected yet.");
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void WindowChrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ShowPage(string page)
    {
        _activePage = page;

        HomePage.Visibility = page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        LeaderboardPage.Visibility = page == "Leaderboard" ? Visibility.Visible : Visibility.Collapsed;
        LibraryPage.Visibility = page == "Library" ? Visibility.Visible : Visibility.Collapsed;
        DownloadsPage.Visibility = page == "Downloads" ? Visibility.Visible : Visibility.Collapsed;
        StatusPage.Visibility = page == "Status" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        SetNavButton(HomeNavButton, page == "Home");
        SetNavButton(LeaderboardNavButton, page == "Leaderboard");
        SetNavButton(LibraryNavButton, page == "Library");
        SetNavButton(DownloadsNavButton, page == "Downloads");
        SetNavButton(StatusNavButton, page == "Status");
        SetNavButton(SettingsNavButton, page == "Settings");
    }

    private void SetNavButton(WpfButton button, bool active)
    {
        button.Background = active
            ? (MediaBrush)FindResource("PanelStrongBrush")
            : new SolidColorBrush(MediaColor.FromRgb(17, 23, 19));
        button.BorderBrush = active
            ? (MediaBrush)FindResource("BorderBrushColor")
            : button.Background;
        button.Foreground = active
            ? (MediaBrush)FindResource("TextBrush")
            : (MediaBrush)FindResource("MutedBrush");
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

        var displayName = signedIn ? _discordSession!.User.DisplayName : "Guest";
        WelcomeNameText.Text = displayName;
        HeaderUserText.Text = signedIn
            ? $"#{ShortDiscordId(_discordSession!.User.Id)} {displayName}"
            : "#00000 Guest";

        LoginDiscordButton.IsEnabled = !signedIn;
        LogoutDiscordButton.IsEnabled = signedIn;
        UpdateLaunchButton();
    }

    private void UpdateLaunchButton()
    {
        var canLaunch =
            BuildsListBox.SelectedItem is BuildDefinition &&
            _discordSession is not null &&
            !_discordSession.IsExpired &&
            _launchState is not LaunchState.Launching and not LaunchState.Closing;

        LaunchButton.IsEnabled = canLaunch;
        LaunchButton.Content = _launchState switch
        {
            LaunchState.Launching => "LAUNCHING DREAM",
            LaunchState.Launched => "DREAM IS RUNNING",
            LaunchState.Closing => "CLOSING GAME",
            _ when _discordSession is null || _discordSession.IsExpired => "LOGIN WITH DISCORD TO LAUNCH",
            _ when BuildsListBox.SelectedItem is not BuildDefinition => "SELECT A BUILD TO LAUNCH",
            _ => "LAUNCH DREAM"
        };
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

    private void UpdateBuildSummary()
    {
        BuildCountText.Text = _builds.Count == 1 ? "1 build installed" : $"{_builds.Count} builds installed";

        if (BuildsListBox.SelectedItem is BuildDefinition build)
        {
            SelectedBuildText.Text = build.Name;
            HomeBuildText.Text = build.Name;
            DownloadsSelectedBuildText.Text = build.Name;
        }
        else
        {
            SelectedBuildText.Text = "No build selected";
            HomeBuildText.Text = "No build selected";
            DownloadsSelectedBuildText.Text = "No build selected";
        }

        DownloadsContentDirectoryText.Text = $"Content directory: {_settings.ContentDirectory}";
    }

    private void UpdateHomeStatusSummary(ServiceCheckResult backend, ServiceCheckResult gameServer)
    {
        HomeStatusTitleText.Text = backend.State == ServiceState.Online
            ? "Backend services are online"
            : "Backend needs attention";
        HomeStatusSubtitleText.Text = gameServer.State == ServiceState.Online
            ? "Game server is reachable. You can launch when account and build are ready."
            : "Backend status was checked. Game server is still offline or not started.";
    }

    private void UpdateBackendServices(ServiceCheckResult backend)
    {
        _backendServices.Clear();

        if (backend.BackendStatus?.Services.Count > 0)
        {
            foreach (var service in backend.BackendStatus.Services)
            {
                _backendServices.Add(service);
            }

            var connectedClients = backend.BackendStatus.Services
                .Where(service => service.ConnectedClients is not null)
                .Sum(service => service.ConnectedClients ?? 0);

            HeaderOnlineText.Text = $"{connectedClients} Players Online";
            return;
        }

        _backendServices.Add(new LauncherServiceStatus
        {
            Id = "backend",
            Label = "Backend API",
            State = backend.State.ToString().ToLowerInvariant(),
            Details = backend.Error ?? backend.Summary ?? "No backend details available"
        });
        HeaderOnlineText.Text = "0 Players Online";
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
        report.AppendLine($"Content directory: {_settings.ContentDirectory}");
        report.AppendLine($"Auto download: {_settings.AutoDownload}");
        report.AppendLine($"Detailed downloads: {_settings.DetailedDownloads}");
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
            InvalidOperationException invalid when invalid.Message.Contains("discord_oauth_not_configured", StringComparison.OrdinalIgnoreCase) =>
                "Discord OAuth is not configured on the backend. Add DISCORD_CLIENT_ID and DISCORD_CLIENT_SECRET to backend .env.",
            InvalidOperationException invalid when invalid.Message.Contains("invalid_launcher_token", StringComparison.OrdinalIgnoreCase) =>
                "Saved launcher session is invalid or expired. Sign out and log in with Discord again.",
            InvalidOperationException invalid when invalid.Message.Contains("discord_login_failed", StringComparison.OrdinalIgnoreCase) =>
                "Discord login failed on the backend. Check the Discord application redirect URI and backend .env.",
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
            ServiceState.Offline => $"{label}: offline ({result.Error ?? result.Summary})",
            _ => $"{label}: checking..."
        };
    }

    private static string FormatStatusCode(ServiceCheckResult result)
    {
        return result.StatusCode is null ? string.Empty : $", HTTP {result.StatusCode}";
    }

    private static string ShortDiscordId(string id)
    {
        return id.Length <= 5 ? id : id[^5..];
    }

    private void AddStatusSummary(string label, ServiceCheckResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Summary))
        {
            AddLog($"{label}: {result.Summary}");
        }
    }
}
