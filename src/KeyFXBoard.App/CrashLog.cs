namespace KeyFXBoard.App;

internal static class CrashLog
{
    public static void Write(string source, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KeyFXBoard",
                "logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "crash.log");
            var body =
                $"{DateTime.Now:O} [{source}]{Environment.NewLine}" +
                $"{ex}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(path, body);
        }
        catch
        {
            // Last-resort logging must never throw.
        }
    }
}
