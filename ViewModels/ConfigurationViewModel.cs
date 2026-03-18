using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scoreboard.Enums;
using Scoreboard.Models;
using Scoreboard.Windows;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Scoreboard.ViewModels;
public class ConfigurationViewModel : ObservableObject
{
    public string Title { get; set; } = "Configuration";
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

        Title += $" V:{Assembly.GetExecutingAssembly().GetName().Version}";
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
    }

    private void SetColors()
    {
        HomeDisplayColor = Settings.StringToColor[Settings.HomeColor ?? "Yellow"];
        VisitorDisplayColor = Settings.StringToColor[Settings.VisitorColor ?? "Green"];
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
