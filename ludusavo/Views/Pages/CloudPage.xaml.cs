using System.Windows.Controls;
using ludusavo.ViewModels;

namespace ludusavo.Views.Pages;

public partial class CloudPage : Page
{
    public CloudViewModel ViewModel { get; }

    public CloudPage(CloudViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += async (s, e) =>
        {
            if (!ViewModel.IsInitialized)
            {
                await ViewModel.InitializeAsync();
            }
        };

        SizeChanged += (s, e) =>
        {
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
