using System.Windows.Controls;
using ludusavo.ViewModels;

namespace ludusavo.Views.Pages;

public partial class GamesPage : Page
{
    public GamesViewModel ViewModel { get; }

    public GamesPage(GamesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
        };

        SizeChanged += (s, e) =>
        {
            // Hide button text and show only icons when page width is narrow
            ViewModel.IsCompactMode = ActualWidth < 900;
        };
    }
}
