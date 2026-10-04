using System.Windows.Controls;
using System.Windows.Input;
using ludusavo.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace ludusavo.Views.Pages;

public partial class GamesPage : UserControl
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
    }

    private void ListView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (sender is not System.Windows.Controls.ListView listView || e.OriginalSource is not System.Windows.DependencyObject dep)
            return;

        // If click is on a ListViewItem or any element inside it, let selection happen normally
        var item = System.Windows.Controls.ItemsControl.ContainerFromElement(listView, dep);
        if (item != null)
            return;

        // If clicked on ScrollBar or ColumnHeader, don't deselect
        if (FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(dep) != null ||
            FindVisualParent<System.Windows.Controls.GridViewColumnHeader>(dep) != null)
        {
            return;
        }

        // Clicked empty space in ListView: deselect
        listView.SelectedItem = null;
        ViewModel.SelectedGame = null;
        Keyboard.ClearFocus();
    }

    private void ListView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.ListView listView || e.OriginalSource is not System.Windows.DependencyObject dep)
            return;

        var item = System.Windows.Controls.ItemsControl.ContainerFromElement(listView, dep) as System.Windows.Controls.ListViewItem;
        if (item != null)
        {
            item.IsSelected = true;
            item.Focus();
        }
        else
        {
            // Right-click on empty space or column header: prevent ContextMenu from opening
            e.Handled = true;
        }
    }

    private void Page_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is not System.Windows.DependencyObject dep) return;

        // If click is within the ListView, handled by ListView_PreviewMouseDown
        if (FindVisualParent<System.Windows.Controls.ListView>(dep) != null)
            return;

        // Don't deselect when clicking buttons, searchbox, etc.
        if (FindVisualParent<System.Windows.Controls.Primitives.ButtonBase>(dep) != null ||
            FindVisualParent<System.Windows.Controls.TextBox>(dep) != null)
        {
            return;
        }

        ViewModel.SelectedGame = null;
        Keyboard.ClearFocus();
    }

    private static T? FindVisualParent<T>(System.Windows.DependencyObject child) where T : System.Windows.DependencyObject
    {
        System.Windows.DependencyObject? parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;
        if (parentObject is T parent) return parent;
        return FindVisualParent<T>(parentObject);
    }
}
