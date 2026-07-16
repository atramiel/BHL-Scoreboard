using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scoreboard.Enums;
using Scoreboard.Models;
using Scoreboard.Services;
using Scoreboard.Windows;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Scoreboard.ViewModels;
public class ConfigurationViewModel : ObservableObject
{
    public string Title { get; set; } = "Configuration";
    public string ScoreboardUrl { get; } = GetScoreboardUrl();

    /// <summary>The live game — lets Settings show operator-only status (Challonge report, league queue).</summary>
    public MainWindowViewModel? Game { get; set; }

    private static string GetScoreboardUrl()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            var ip = ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
            return $"http://{ip}:{WebBroadcastService.Port}/";
        }
        catch
        {
            return $"http://localhost:{WebBroadcastService.Port}/";
        }
    }
    private GameAction keyBindAction = GameAction.None;
    public RelayCommand DismissError { get; set; }
    public IRelayCommand LoadCommand { get; set; }
    public IRelayCommand EditLedEffectCommand { get; set; }
    public IRelayCommand<GameAction> SetKeyCommand { get; set; }
    public IRelayCommand<KeyEventArgs?> InputCommand { get; set; }
    public IRelayCommand SaveCommand { get; set; }
    public IRelayCommand CancelCommand { get; set; }
    public IRelayCommand HomeColorCommand { get; set; }
    public IRelayCommand VisitorColorCommand { get; set; }
    public IRelayCommand DownloadLeagueDataCommand { get; set; }
    public IRelayCommand RetryQueuedPostsCommand { get; set; }
    public IRelayCommand PostRecapCommand { get; set; }
    public IRelayCommand AddSponsorLogoCommand { get; set; }
    public IRelayCommand<string> RemoveSponsorLogoCommand { get; set; }
    public IRelayCommand SetEventLogoCommand { get; set; }
    public IRelayCommand ExportSettingsCommand { get; set; }
    public IRelayCommand ImportSettingsCommand { get; set; }

    private string _leagueDownloadStatus = ""; public string LeagueDownloadStatus
    {
        get => _leagueDownloadStatus;
        set => SetProperty(ref _leagueDownloadStatus, value);
    }

    private string _exportImportStatus = ""; public string ExportImportStatus
    {
        get => _exportImportStatus;
        set => SetProperty(ref _exportImportStatus, value);
    }

    private string _recapStatus = ""; public string RecapStatus
    {
        get => _recapStatus;
        set => SetProperty(ref _recapStatus, value);
    }

    private bool _hasQueuedPosts; public bool HasQueuedPosts
    {
        get => _hasQueuedPosts;
        set => SetProperty(ref _hasQueuedPosts, value);
    }
    private string _retryQueuedLabel = "Retry Queued Posts"; public string RetryQueuedLabel
    {
        get => _retryQueuedLabel;
        set => SetProperty(ref _retryQueuedLabel, value);
    }
    private string _retryQueuedStatus = ""; public string RetryQueuedStatus
    {
        get => _retryQueuedStatus;
        set => SetProperty(ref _retryQueuedStatus, value);
    }

    private GameSettings _settings; public GameSettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }
    private ObservableCollection<KeyValuePair<GameAction, Key>>? _keyBindings;
    public ObservableCollection<KeyValuePair<GameAction, Key>>? KeyBindings
    {
        get => _keyBindings;
        set => SetProperty(ref _keyBindings, value);
    }
    private bool _showKeypressPrompt; public bool ShowKeypressPrompt
    {
        get => _showKeypressPrompt;
        set => SetProperty(ref _showKeypressPrompt, value);
    }
    private bool _showError; public bool ShowError
    {
        get => _showError;
        set => SetProperty(ref _showError, value);
    }
    private string _errorMessage = ""; public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }
    private Brush? _homeDisplayColor; public Brush? HomeDisplayColor
    {
        get => _homeDisplayColor;
        set => SetProperty(ref _homeDisplayColor, value);
    }
    private Brush? _visitorDisplayColor; public Brush? VisitorDisplayColor
    {
        get => _visitorDisplayColor;
        set => SetProperty(ref _visitorDisplayColor, value);
    }
    private ObservableCollection<string> _sponsorLogoPaths = [];
    public ObservableCollection<string> SponsorLogoPaths
    {
        get => _sponsorLogoPaths;
        set => SetProperty(ref _sponsorLogoPaths, value);
    }

    public ConfigurationViewModel()
    {
        _settings = new();
        SetKeyCommand = new RelayCommand<GameAction>(PromptKeypress);
        InputCommand = new RelayCommand<KeyEventArgs?>(SetKey);
        LoadCommand = new AsyncRelayCommand(LoadSettings);
        DismissError = new RelayCommand(HideError);
        SaveCommand = new AsyncRelayCommand(SaveSettings);
        CancelCommand = new RelayCommand(Close);
        HomeColorCommand = new RelayCommand(() => ChooseColor(TeamType.Home));
        VisitorColorCommand = new RelayCommand(() => ChooseColor(TeamType.Visitor));
        EditLedEffectCommand = new RelayCommand(ShowLedConfig);
        DownloadLeagueDataCommand = new AsyncRelayCommand(DownloadLeagueData);
        RetryQueuedPostsCommand = new AsyncRelayCommand(RetryQueuedPosts);
        PostRecapCommand = new AsyncRelayCommand(PostRecap);
        AddSponsorLogoCommand = new RelayCommand(AddSponsorLogo);
        RemoveSponsorLogoCommand = new RelayCommand<string>(RemoveSponsorLogo);
        SetEventLogoCommand = new RelayCommand(SetEventLogo);
        ExportSettingsCommand = new RelayCommand(ExportSettings);
        ImportSettingsCommand = new RelayCommand(ImportSettings);

        Title += $" V:{Assembly.GetExecutingAssembly().GetName().Version}";
        RefreshQueuedCount();
    }

    private async Task DownloadLeagueData()
    {
        if (string.IsNullOrWhiteSpace(Settings.SupabaseUrl) || string.IsNullOrWhiteSpace(Settings.SupabaseAnonKey))
        {
            LeagueDownloadStatus = "Set League Site URL and Public Key first.";
            return;
        }
        LeagueDownloadStatus = "Downloading…";
        var summary = await LeagueSiteService.DownloadBundleAsync(Settings);
        LeagueDownloadStatus = summary != null
            ? $"Saved for offline use: {summary}"
            : "Download failed — check the URL, key, and connection.";
    }

    private void RefreshQueuedCount()
    {
        var count = LeagueSiteService.QueuedCount();
        HasQueuedPosts = count > 0;
        RetryQueuedLabel = count > 0 ? $"Retry Queued Posts ({count})" : "Retry Queued Posts";
    }

    private async Task RetryQueuedPosts()
    {
        RetryQueuedStatus = "Retrying…";
        var sent = await LeagueSiteService.FlushQueueAsync(Settings);
        var remaining = LeagueSiteService.QueuedCount();
        RetryQueuedStatus = sent > 0
            ? $"✓ sent {sent}" + (remaining > 0 ? $", {remaining} still failing" : "")
            : remaining > 0 ? $"Failed: {LeagueSiteService.LastError}" : "Nothing queued";
        RefreshQueuedCount();
    }

    private async Task PostRecap()
    {
        if (!DiscordService.IsConfigured(Settings))
        {
            RecapStatus = "Set the Discord Webhook URL first.";
            return;
        }
        RecapStatus = "Posting…";
        var lines = LeagueSiteService.LoadAttractData().TodayResults.Select(l => l.Text).ToList();
        await DiscordService.PostRecapAsync(Settings, lines);
        RecapStatus = $"✓ Posted ({lines.Count} game{(lines.Count == 1 ? "" : "s")})";
    }

    private void ShowLedConfig()
    {
        var viewModel = new ConfigureLedEffectViewModel(ref _settings);

        var config = new ConfigureLedEffectWindow
        {
            Owner = App.Current.Windows[^2],
            DataContext = viewModel
        };

        config.ShowDialog();
    }

    private void Close()
    {
        foreach (var window in App.Current.Windows)
        {
            if (window.GetType() == typeof(ConfigurationWindow))
            {
                var w = (Window)window;
                w.Close();
            }
        }
    }

    private void PromptKeypress(GameAction action)
    {
        keyBindAction = action;
        ShowKeypressPrompt = true;
    }
    private void HideError()
    {
        ErrorMessage = string.Empty;
        ShowError = false;
    }
    private void SetKey(KeyEventArgs? args)
    {
        if (ShowError)
            HideError();

        if (args == null)
        {
            ErrorMessage = "Invalid key.";
            ShowError = true;
            return;
        }

        ShowKeypressPrompt = false;
        if (args != null && Settings.KeyBindings.ContainsValue(args.Key))
        {
            ShowError = true;
            ErrorMessage = $"The {args.Key} key is already being used for {Settings.KeyBindings
                .Where(b => b.Value == args.Key).First().Key}";
            return;
        }
        if (keyBindAction != GameAction.None && Settings.KeyBindings.ContainsKey(keyBindAction))
        {
            Settings.KeyBindings[keyBindAction] = args!.Key;
            keyBindAction = GameAction.None;
        }

        RefreshBindingDisplayList();
        args!.Handled = true;
    }
    private void RefreshBindingDisplayList()
    {
        KeyBindings = [];
        foreach (var binding in Settings.KeyBindings)
        {
            KeyBindings.Add(new KeyValuePair<GameAction, Key>(binding.Key, binding.Value));
        }
    }
    private async Task SaveSettings()
    {
        await File.WriteAllTextAsync("bindings.json", JsonSerializer.Serialize(Settings.KeyBindings));
        await File.WriteAllTextAsync("gameSettings.json", JsonSerializer.Serialize(Settings));
        Close();
    }
    private async Task LoadSettings()
    {
        if (!File.Exists("bindings.json"))
        {
            await File.WriteAllTextAsync("bindings.json", JsonSerializer.Serialize(Settings.KeyBindings));
        }
        if (!File.Exists("gameSettings.json"))
        {
            Settings = new();
        }
        else
        {
            var settingsString = await File.ReadAllTextAsync("gameSettings.json");
            var bindingsString = await File.ReadAllTextAsync("bindings.json");
            var bindings = JsonSerializer.Deserialize<Dictionary<GameAction, Key>>(bindingsString);
            Settings = JsonSerializer.Deserialize<GameSettings>(settingsString)
                ?? throw new ApplicationException("Could not load game settings from file.");
            if (bindings != null) Settings.KeyBindings = bindings;
        }
        RefreshBindingDisplayList();
        SetColors();
        SponsorLogoPaths = new ObservableCollection<string>(Settings.SponsorLogoPaths);
    }

    private void SetColors()
    {
        HomeDisplayColor = Settings.StringToColor[Settings.HomeColor ?? "Yellow"];
        VisitorDisplayColor = Settings.StringToColor[Settings.VisitorColor ?? "Green"];
    }

    private const string SponsorLogoDir = "SponsorLogos";

    private void AddSponsorLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add Sponsor Logo(s)",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;

        Directory.CreateDirectory(SponsorLogoDir);
        foreach (var source in dialog.FileNames)
        {
            // Copy into the app's own folder rather than referencing the original
            // location — keeps the logo available even if the source file moves.
            var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(source)}";
            var dest = Path.GetFullPath(Path.Combine(SponsorLogoDir, fileName));
            File.Copy(source, dest, overwrite: true);
            SponsorLogoPaths.Add(dest);
        }
        Settings.SponsorLogoPaths = [.. SponsorLogoPaths];
    }

    private void RemoveSponsorLogo(string? path)
    {
        if (path == null) return;
        SponsorLogoPaths.Remove(path);
        Settings.SponsorLogoPaths = [.. SponsorLogoPaths];
        try { File.Delete(path); } catch { }
    }

    private void SetEventLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Set Event Logo",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.gif;*.bmp"
        };
        if (dialog.ShowDialog() != true) return;

        Directory.CreateDirectory(SponsorLogoDir);
        var dest = Path.GetFullPath(Path.Combine(SponsorLogoDir, $"event-logo{Path.GetExtension(dialog.FileName)}"));
        File.Copy(dialog.FileName, dest, overwrite: true);
        Settings.EventLogoPath = dest;
        OnPropertyChanged(nameof(Settings));
    }

    // One-file backup so a whole setup (credentials, key bindings, colors, sponsor
    // logos, everything) can move to a new laptop without retyping any of it.
    private void ExportSettings()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export All Settings",
            Filter = "Scoreboard Settings Bundle|*.zip",
            FileName = $"ScoreboardSettings_{DateTime.Now:yyyy-MM-dd}.zip"
        };
        if (dialog.ShowDialog() != true) return;

        var tempDir = Path.Combine(Path.GetTempPath(), $"ScoreboardExport_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            if (File.Exists("gameSettings.json")) File.Copy("gameSettings.json", Path.Combine(tempDir, "gameSettings.json"));
            if (File.Exists("bindings.json")) File.Copy("bindings.json", Path.Combine(tempDir, "bindings.json"));
            if (Directory.Exists(SponsorLogoDir)) CopyDirectory(SponsorLogoDir, Path.Combine(tempDir, SponsorLogoDir));

            if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
            ZipFile.CreateFromDirectory(tempDir, dialog.FileName);
            ExportImportStatus = $"✓ Exported to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            ExportImportStatus = $"Export failed: {ex.Message}";
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    private void ImportSettings()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Settings Bundle",
            Filter = "Scoreboard Settings Bundle|*.zip"
        };
        if (dialog.ShowDialog() != true) return;

        var tempDir = Path.Combine(Path.GetTempPath(), $"ScoreboardImport_{Guid.NewGuid():N}");
        try
        {
            ZipFile.ExtractToDirectory(dialog.FileName, tempDir, overwriteFiles: true);

            var incomingSponsorDir = Path.Combine(tempDir, SponsorLogoDir);
            if (Directory.Exists(incomingSponsorDir))
            {
                Directory.CreateDirectory(SponsorLogoDir);
                CopyDirectory(incomingSponsorDir, SponsorLogoDir);
            }

            var incomingSettingsPath = Path.Combine(tempDir, "gameSettings.json");
            if (File.Exists(incomingSettingsPath))
            {
                var incoming = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(incomingSettingsPath))
                    ?? throw new ApplicationException("Bundle's gameSettings.json is invalid.");

                // Sponsor/event logo paths were absolute on the OLD machine — re-root
                // them to this machine's SponsorLogos folder, keyed by filename, since
                // the folder was just restored above with the same filenames.
                incoming.SponsorLogoPaths = [.. incoming.SponsorLogoPaths
                    .Select(p => Path.GetFullPath(Path.Combine(SponsorLogoDir, Path.GetFileName(p))))];
                if (!string.IsNullOrWhiteSpace(incoming.EventLogoPath))
                    incoming.EventLogoPath = Path.GetFullPath(Path.Combine(SponsorLogoDir, Path.GetFileName(incoming.EventLogoPath)));

                File.WriteAllText("gameSettings.json", JsonSerializer.Serialize(incoming));
            }

            var incomingBindingsPath = Path.Combine(tempDir, "bindings.json");
            if (File.Exists(incomingBindingsPath))
                File.Copy(incomingBindingsPath, "bindings.json", overwrite: true);

            ExportImportStatus = "✓ Imported — close and reopen Settings (or restart the app) to see everything.";
        }
        catch (Exception ex)
        {
            ExportImportStatus = $"Import failed: {ex.Message}";
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
    }

    public static async Task<GameSettings> LoadSettingsAsync()
    {
        GameSettings settings = new();
        if (File.Exists("gameSettings.json"))
        {
            var settingsString = await File.ReadAllTextAsync("gameSettings.json");
            var bindingsString = await File.ReadAllTextAsync("bindings.json");
            var bindings = JsonSerializer.Deserialize<Dictionary<GameAction, Key>>(bindingsString);
            settings = JsonSerializer.Deserialize<GameSettings>(settingsString)
                ?? throw new ApplicationException("Could not load game settings from file.");
            if (bindings != null) settings.KeyBindings = bindings;
        }

        return settings;
    }
    private void ChooseColor(TeamType teamType)
    {
        var viewModel = new ColorPickerViewModel();
        var colorPicker = new ColorPickerWindow()
        {
            Owner = App.Current.Windows[^2],
            DataContext = viewModel
        };

        colorPicker.ShowDialog();

        switch (teamType)
        {
            case TeamType.Home:
                Settings.HomeColor = viewModel.SelectedColor.Key;
                if (viewModel.SelectedColor.Key != null)
                    HomeDisplayColor = Settings.StringToColor[viewModel.SelectedColor.Key];
                break;
            case TeamType.Visitor:
                Settings.VisitorColor = viewModel.SelectedColor.Key;
                if (viewModel.SelectedColor.Key != null)
                    VisitorDisplayColor = Settings.StringToColor[viewModel.SelectedColor.Key];
                break;
        }
    }

}
