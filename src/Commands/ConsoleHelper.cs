using System.Text.RegularExpressions;

namespace ludusavo.Commands;

public static class Markup
{
    public static string Escape(string text) => text ?? string.Empty;
}

public static class Spinner
{
    public static class Known
    {
        public static object Dots => new();
    }
}

public static class ConsoleHelper
{
    public static void ShowHeader() { }

    public static void WriteLine(string text = "")
    {
        Console.WriteLine(text);
    }

    public static void Write(string text)
    {
        Console.Write(text);
    }

    public static void MarkupLine(string text)
    {
        Markup(text);
        Console.WriteLine();
    }

    public static void Markup(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Parse tags like [red], [green], [yellow], [cyan], [blue], [magenta], [dim], [grey], [/]
        var pattern = @"(\[(?:red|green|yellow|cyan|blue|magenta|grey|gray|dim|\/)\])";
        var parts = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

        foreach (var part in parts)
        {
            if (part.StartsWith("[") && part.EndsWith("]"))
            {
                var tag = part.Substring(1, part.Length - 2).ToLowerInvariant();
                switch (tag)
                {
                    case "red":
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case "green":
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case "yellow":
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case "cyan":
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        break;
                    case "blue":
                        Console.ForegroundColor = ConsoleColor.Blue;
                        break;
                    case "magenta":
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        break;
                    case "grey":
                    case "gray":
                    case "dim":
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        break;
                    case "/":
                    default:
                        Console.ResetColor();
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(part))
            {
                Console.Write(part);
            }
        }
        Console.ResetColor();
    }

    public static async Task StatusAsync(string initialStatus, Func<Action<string>, Task> action)
    {
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine(initialStatus);
            await action(newStatus => Console.WriteLine(newStatus));
            return;
        }

        bool originalCursorVisible = true;
        try
        {
            originalCursorVisible = Console.CursorVisible;
            Console.CursorVisible = false;
        }
        catch { }

        var spinnerFrames = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        var currentMsg = initialStatus;
        var cts = new CancellationTokenSource();
        int frameIdx = 0;
        int maxPrintedLength = 0;

        var spinnerTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var frame = spinnerFrames[frameIdx % spinnerFrames.Length];
                frameIdx++;
                try
                {
                    int winWidth = 80;
                    try
                    {
                        if (Console.WindowWidth > 1) winWidth = Console.WindowWidth - 1;
                    }
                    catch { }

                    var line = $"{frame} {currentMsg}";
                    if (line.Length > winWidth)
                    {
                        line = line.Substring(0, Math.Max(0, winWidth - 3)) + "...";
                    }

                    int padding = Math.Max(0, maxPrintedLength - line.Length);
                    if (line.Length + padding > winWidth)
                    {
                        padding = Math.Max(0, winWidth - line.Length);
                    }

                    Console.Write("\r" + line + new string(' ', padding + 2));
                    maxPrintedLength = Math.Max(maxPrintedLength, line.Length);
                }
                catch { }

                try
                {
                    await Task.Delay(80, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        try
        {
            await action(newStatus => { currentMsg = newStatus; });
        }
        finally
        {
            cts.Cancel();
            try
            {
                await spinnerTask;
            }
            catch { }

            try
            {
                int winWidth = 80;
                try
                {
                    if (Console.WindowWidth > 1) winWidth = Console.WindowWidth - 1;
                }
                catch { }

                var clearWidth = Math.Min(winWidth, Math.Max(maxPrintedLength + 5, 80));
                Console.Write("\r" + new string(' ', clearWidth) + "\r");
            }
            catch
            {
                Console.Write("\r                                                                                \r");
            }

            try
            {
                Console.CursorVisible = originalCursorVisible;
            }
            catch { }
        }
    }

    public static string Prompt(string label, string? defaultValue = null, bool secret = false)
    {
        Markup(label + " ");
        if (!string.IsNullOrEmpty(defaultValue))
        {
            Markup($"[dim]({defaultValue})[/] ");
        }

        if (secret)
        {
            var pass = string.Empty;
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    pass = pass.Substring(0, pass.Length - 1);
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    pass += key.KeyChar;
                    Console.Write("*");
                }
            }
            return string.IsNullOrEmpty(pass) && !string.IsNullOrEmpty(defaultValue) ? defaultValue : pass;
        }

        var input = Console.ReadLine() ?? string.Empty;
        input = input.Trim();
        return string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(defaultValue) ? defaultValue : input;
    }

    public static bool Confirm(string question, bool defaultChoice = true)
    {
        var hint = defaultChoice ? "[Y/n]" : "[y/N]";
        Markup($"{question} [dim]{hint}[/] ");
        var res = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(res)) return defaultChoice;
        return res == "y" || res == "yes";
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
        if (string.IsNullOrWhiteSpace(token)) return "[red]Not configured[/]";
        if (token.Length <= 8) return "********";
        return token.Substring(0, 4) + "..." + token.Substring(token.Length - 4);
    }

    public static string FormatDateTime(DateTime? dt)
    {
        if (!dt.HasValue || dt.Value == DateTime.MinValue || dt.Value.Year <= 1)
            return "-";
        return dt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }

    public static bool TryParseSelectionIndices(string input, int maxCount, out List<int> indices)
    {
        indices = new List<int>();
        if (string.IsNullOrWhiteSpace(input)) return false;

        // Only accept space-separated format, no commas or hyphens allowed
        if (input.Contains(',') || input.Contains('-')) return false;

        var tokens = Regex.Split(input.Trim(), @"\s+");
        var result = new HashSet<int>();

        foreach (var token in tokens)
        {
            if (int.TryParse(token, out int num))
            {
                if (num < 1 || num > maxCount) return false;
                result.Add(num);
            }
            else
            {
                return false;
            }
        }

        if (result.Count == 0) return false;

        indices = result.OrderBy(x => x).ToList();
        return true;
    }
}

public static class AnsiConsole
{
    public static void WriteLine(string text = "") => ConsoleHelper.WriteLine(text);
    public static void Write(string text) => ConsoleHelper.Write(text);
    public static void MarkupLine(string text) => ConsoleHelper.MarkupLine(text);
    public static void Markup(string text) => ConsoleHelper.Markup(text);
    public static StatusRunner Status() => new StatusRunner();
    public static string Prompt(string prompt, string? defaultValue = null, bool secret = false) => ConsoleHelper.Prompt(prompt, defaultValue, secret);
    public static bool Confirm(string prompt, bool defaultVal = true) => ConsoleHelper.Confirm(prompt, defaultVal);
}

public class StatusRunner
{
    public StatusRunner Spinner(object? unused = null) => this;

    public async Task StartAsync(string initialStatus, Func<StatusContext, Task> action)
    {
        await ConsoleHelper.StatusAsync(initialStatus, async updateStatus =>
        {
            var ctx = new StatusContext(updateStatus);
            await action(ctx);
        });
    }
}

public class StatusContext
{
    private readonly Action<string> _update;
    public StatusContext(Action<string> update) => _update = update;
    public void Status(string text) => _update(text);
}
