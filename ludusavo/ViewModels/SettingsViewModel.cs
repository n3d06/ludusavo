using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ludusavo.Models;
using ludusavo.Services;
using System.ComponentModel.DataAnnotations;

namespace ludusavo.ViewModels;

public partial class SettingsViewModel : ObservableValidator
{
    private readonly IConfigService _configService;
    private readonly IGitHubService _gitHubService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "GitHub Token cannot be empty")]
    private string _gitHubToken = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "GitHub Owner cannot be empty")]
    private string _gitHubOwner = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "GitHub Repo cannot be empty")]
    private string _gitHubRepo = string.Empty;


    [ObservableProperty]
    private bool _minimizeToTray = false;

    [ObservableProperty]
    private bool _startWithWindows = false;

    [ObservableProperty]
    private string _testStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private bool? _testSuccess;

    public SettingsViewModel(
        IConfigService configService,
        IGitHubService gitHubService,
        MainViewModel mainViewModel)
    {
        _configService = configService;
        _gitHubService = gitHubService;
        _mainViewModel = mainViewModel;

        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var s = _configService.Settings;
        GitHubToken = s.GitHubToken;
        GitHubOwner = s.GitHubOwner;
        GitHubRepo = s.GitHubRepo;
        MinimizeToTray = s.MinimizeToTray;
        StartWithWindows = s.StartWithWindows;
    }

    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        IsTesting = true;
        TestStatusMessage = "Testing GitHub connection...";
        TestSuccess = null;

        // Save temporarily in memory to test
        _configService.Settings.GitHubToken = GitHubToken.Trim();
        _configService.Settings.GitHubOwner = GitHubOwner.Trim();
        _configService.Settings.GitHubRepo = GitHubRepo.Trim();

        var (success, msg) = await _gitHubService.TestConnectionAsync();
        TestSuccess = success;
        TestStatusMessage = msg ?? (success ? "Connected successfully!" : "Connection failed");
        IsTesting = false;

        _mainViewModel.UpdateGitHubStatus();
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var current = _configService.Settings;
        current.GitHubToken = GitHubToken.Trim();
        current.GitHubOwner = GitHubOwner.Trim();
        current.GitHubRepo = GitHubRepo.Trim();
        current.MinimizeToTray = MinimizeToTray;
        current.StartWithWindows = StartWithWindows;

        _configService.SaveSettings(current);
        _mainViewModel.UpdateGitHubStatus();

        // Configure startup with Windows registry if requested
        ConfigureStartup(StartWithWindows);

        TestStatusMessage = "Settings saved successfully!";
        TestSuccess = true;
    }

    private void ConfigureStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            var appPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(appPath))
            {
                if (enable)
                {
                    key?.SetValue("ludusavo", $"\"{appPath}\" --minimized");
                }
                else
                {
                    key?.DeleteValue("ludusavo", false);
                }
            }
        }
        catch { }
    }
}
