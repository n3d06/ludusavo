using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ludusavo.Services;

namespace ludusavo.Views.Dialogs;

public partial class CustomGameDialog : Window
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8),
        DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0" } }
    };

    public string GameTitle { get; private set; } = string.Empty;
    public string FolderPath { get; private set; } = string.Empty;
    public int? SteamId { get; private set; }
    public string? BannerUrl { get; private set; }

    private System.Threading.CancellationTokenSource? _steamLookupCts;

    public CustomGameDialog(
        string dialogTitle,
        string? initialName = null,
        string? initialFolder = null,
        int? initialSteamId = null,
        string? initialBanner = null)
    {
        InitializeComponent();

        Title = dialogTitle;
        DialogHeader.Text = dialogTitle;

        if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow != this)
        {
            Owner = System.Windows.Application.Current.MainWindow;
        }

        if (!string.IsNullOrEmpty(initialName))
        {
            TitleTextBox.Text = initialName;
        }

        if (!string.IsNullOrEmpty(initialFolder))
        {
            FolderTextBox.Text = initialFolder;
        }

        if (initialSteamId.HasValue && initialSteamId.Value > 0)
        {
            RbSteamId.IsChecked = true;
            SteamIdTextBox.Text = initialSteamId.Value.ToString();
        }
        else if (!string.IsNullOrEmpty(initialBanner))
        {
            RbCustomUrl.IsChecked = true;
            BannerTextBox.Text = initialBanner;
        }
        else
        {
            RbSteamId.IsChecked = true;
        }

        Loaded += (s, e) =>
        {
            if (string.IsNullOrEmpty(TitleTextBox.Text))
            {
                TitleTextBox.Focus();
            }
            else
            {
                FolderTextBox.Focus();
            }

            RefreshBannerPreview();
        };
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = "Select Game Save Folder",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(FolderTextBox.Text) && Directory.Exists(FolderTextBox.Text.Trim()))
        {
            folderDialog.InitialDirectory = FolderTextBox.Text.Trim();
        }

        if (folderDialog.ShowDialog(this) == true)
        {
            FolderTextBox.Text = folderDialog.FolderName;
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            // If title is currently empty, suggest folder name as title
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                var folderName = Path.GetFileName(folderDialog.FolderName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(folderName))
                {
                    TitleTextBox.Text = folderName;
                }
            }
        }
    }

    private void BannerType_Checked(object sender, RoutedEventArgs e)
    {
        if (SteamIdPanel == null || CustomUrlPanel == null) return;

        if (RbSteamId.IsChecked == true)
        {
            SteamIdPanel.Visibility = Visibility.Visible;
            CustomUrlPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            SteamIdPanel.Visibility = Visibility.Collapsed;
            CustomUrlPanel.Visibility = Visibility.Visible;
        }

        RefreshBannerPreview();
    }

    private void RefreshBannerPreview()
    {
        if (RbSteamId.IsChecked == true)
        {
            UpdateSteamPreview(SteamIdTextBox.Text);
        }
        else
        {
            UpdateUrlPreview(BannerTextBox.Text);
        }
    }

    private void SteamIdTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateSteamPreview(SteamIdTextBox.Text);
    }

    private void BannerTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateUrlPreview(BannerTextBox.Text);
    }

    private void UpdateSteamPreview(string? text)
    {
        _steamLookupCts?.Cancel();
        _steamLookupCts = new System.Threading.CancellationTokenSource();
        var token = _steamLookupCts.Token;

        var input = text?.Trim();
        if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input, out int steamId) || steamId <= 0)
        {
            SetBannerImage(null);
            return;
        }

        // Try local cache first
        var cached = BannerCache.GetBannerUrl(steamId, null);
        if (!string.IsNullOrEmpty(cached) && File.Exists(cached))
        {
            SetBannerImage(cached);
            return;
        }

        // Otherwise load from Steam Store CDN directly for immediate preview
        var cdnUrl = $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{steamId}/header.jpg";
        SetBannerImage(cdnUrl);

        // In background: fetch game details from Steam Store API to auto-fill title if empty
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                var apiUrl = $"https://store.steampowered.com/api/appdetails?appids={steamId}&cc=us&l=en";
                using var res = await HttpClient.GetAsync(apiUrl, token);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync(token);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty(steamId.ToString(), out var appObj) &&
                        appObj.TryGetProperty("success", out var succ) && succ.GetBoolean() &&
                        appObj.TryGetProperty("data", out var dataObj))
                    {
                        string? gameName = null;
                        if (dataObj.TryGetProperty("name", out var nameElem))
                        {
                            gameName = nameElem.GetString();
                        }

                        if (!string.IsNullOrEmpty(gameName))
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
                                {
                                    TitleTextBox.Text = gameName;
                                }
                            });
                        }
                    }
                }
            }
            catch { }
        }, token);
    }

    private void UpdateUrlPreview(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            SetBannerImage(null);
            return;
        }

        SetBannerImage(url.Trim());
    }

    private void SetBannerImage(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            BannerImage.Source = null;
            BannerPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var uri = new Uri(pathOrUrl, UriKind.RelativeOrAbsolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            BannerImage.Source = bmp;
            BannerPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            BannerImage.Source = null;
            BannerPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text.Trim();
        var folder = FolderTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError("Please enter a game title.");
            TitleTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            ShowError("Please select or enter the save files folder.");
            FolderTextBox.Focus();
            return;
        }

        if (!Directory.Exists(folder))
        {
            var result = System.Windows.MessageBox.Show(
                this,
                $"The folder \"{folder}\" does not exist yet. Do you want to use it anyway?",
                "Folder Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                FolderTextBox.Focus();
                return;
            }
        }

        GameTitle = title;
        FolderPath = folder;

        if (RbSteamId.IsChecked == true)
        {
            if (int.TryParse(SteamIdTextBox.Text.Trim(), out int sid) && sid > 0)
            {
                SteamId = sid;
            }
            else
            {
                SteamId = null;
            }
            BannerUrl = null;
        }
        else
        {
            SteamId = null;
            BannerUrl = string.IsNullOrWhiteSpace(BannerTextBox.Text) ? null : BannerTextBox.Text.Trim();
        }

        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }
}
