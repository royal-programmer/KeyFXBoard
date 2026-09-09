namespace KeyFXBoard.App;

/// <summary>
/// App lifecycle log. Never write key names, typed text, or window titles.
/// </summary>
internal static class AppLog
{
    public static void Info(string source, string message) => Write("INFO", source, message, null);

    public static void Warn(string source, string message) => Write("WARN", source, message, null);

    public static void Error(string source, string message, Exception? ex = null) =>
        Write("ERROR", source, message, ex);

    private static void Write(string level, string source, string message, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KeyFXBoard",
                "logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "app.log");
            var body =
                $"{DateTime.Now:O} [{level}] [{source}] {Sanitize(message)}" +
                (ex is null ? "" : $"{Environment.NewLine}{Sanitize(ex.GetType().Name)}: {Sanitize(ex.Message)}") +
                Environment.NewLine;
            File.AppendAllText(path, body);
        }
        catch
        {
            // Logging must never throw.
        }
    }

    private static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        // Keep messages short and free of path user folders when possible is not required;
        // never log raw key payloads — callers must not pass them.
        return text.Length <= 500 ? text : text[..500] + "…";
    }
}
