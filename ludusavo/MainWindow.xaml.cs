using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ludusavo.Services;
using ludusavo.ViewModels;
using ludusavo.Views.Pages;

using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ludusavo;

public partial class MainWindow : FluentWindow
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MainViewModel _viewModel;

    public MainWindow(
        MainViewModel viewModel,
        IServiceProvider serviceProvider)
    {
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;

        DataContext = viewModel;
        InitializeComponent();

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Watch for system theme and accent color changes (syncs the blue color to system accent)
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);

        RootNavigation.SetServiceProvider(_serviceProvider);
        RootNavigation.Navigate(typeof(GamesPage));

        // Disable any internal ScrollViewers inside NavigationView's content area
        // so that page-level ScrollViewers can receive mouse wheel events
        Dispatcher.InvokeAsync(() =>
        {
            DisableInternalScrollViewers(RootNavigation);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static void DisableInternalScrollViewers(DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.ScrollViewer sv)
            {
                sv.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                sv.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
            }
            else
            {
                DisableInternalScrollViewers(child);
            }
        }
    }
}
