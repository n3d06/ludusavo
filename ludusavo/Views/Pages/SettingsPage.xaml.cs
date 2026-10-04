using System.Windows.Controls;
using ludusavo.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace ludusavo.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        // PasswordBox doesn't support direct binding, so sync manually
        TokenBox.Password = viewModel.GitHubToken;
    }

    private void TokenBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.GitHubToken = TokenBox.Password;
        }
    }
}
