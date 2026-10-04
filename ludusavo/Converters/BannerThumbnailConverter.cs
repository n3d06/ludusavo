using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ludusavo.Converters;

public class BannerThumbnailConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, BitmapImage> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string pathOrUrl || string.IsNullOrWhiteSpace(pathOrUrl))
            return null;

        var key = pathOrUrl.Trim();
        if (ImageCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            Uri uri;
            if (Uri.TryCreate(key, UriKind.Absolute, out var parsedUri))
            {
                uri = parsedUri;
            }
            else
            {
                var full = Path.GetFullPath(key);
                if (!File.Exists(full)) return null;
                uri = new Uri(full, UriKind.Absolute);
            }

            if (uri.IsFile && !File.Exists(uri.LocalPath))
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            // 92px display width * 2 = 184px for sharp high-DPI rendering with minimal RAM
            bitmap.DecodePixelWidth = 184;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.EndInit();
            bitmap.Freeze(); // Read-only, cross-thread accessible, minimal memory footprint

            ImageCache[key] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
