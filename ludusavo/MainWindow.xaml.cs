using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ludusavo.Services;
using ludusavo.ViewModels;
using ludusavo.Views.Pages;

namespace ludusavo;

public partial class MainWindow : Window
{
    private readonly IConfigService _configService;
    private readonly MainViewModel _viewModel;
    private bool _isExplicitExit = false;

    public MainWindow(
        MainViewModel viewModel,
        IServiceProvider serviceProvider,
        IConfigService configService)
    {
        _viewModel = viewModel;
        _configService = configService;

        DataContext = viewModel;
        InitializeComponent();

        // Views are DI singletons, so their state is preserved between tab switches
        GamesTab.Content = serviceProvider.GetRequiredService<GamesPage>();
        CloudTab.Content = serviceProvider.GetRequiredService<CloudPage>();
        SettingsTab.Content = serviceProvider.GetRequiredService<SettingsPage>();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit && _configService.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    public void ForceClose()
    {
        _isExplicitExit = true;
        Close();
    }
}
