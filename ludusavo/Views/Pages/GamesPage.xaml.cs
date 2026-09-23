using System.Windows.Controls;
using SaveSync.Desktop.ViewModels;

namespace SaveSync.Desktop.Views.Pages;

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

    private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
