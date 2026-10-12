using Spectre.Console;

namespace ludusavo.Commands;

public static class ConsoleHelper
{
    public static void ShowHeader()
    {
        // Giữ gọn gàng như git, không in banner chiếm màn hình
    }

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    public static string MaskToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "[red]Chưa cấu hình[/]";
        if (token.Length <= 8) return "********";
        return token.Substring(0, 4) + "..." + token.Substring(token.Length - 4);
    }

    public static string FormatDateTime(DateTime? dt)
    {
        if (!dt.HasValue || dt.Value == DateTime.MinValue || dt.Value.Year <= 1)
            return "-";
        return dt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }
}
