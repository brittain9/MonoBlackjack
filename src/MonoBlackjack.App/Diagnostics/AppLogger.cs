using System.Diagnostics;

namespace MonoBlackjack.Diagnostics;

internal static class AppLogger
{
    private static readonly object Sync = new();

    public static void LogError(string source, string message, Exception exception)
    {
        var payload = $"[{DateTime.UtcNow:O}] ERROR {source}: {message}{Environment.NewLine}{exception}{Environment.NewLine}";
        Trace.TraceError(payload);

        try
        {
            lock (Sync)
            {
                var path = ResolveLogPath();
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.AppendAllText(path, payload);
            }
        }
        catch (Exception loggingFailure)
        {
            Trace.TraceError($"[{DateTime.UtcNow:O}] ERROR AppLogger: failed to persist log entry. {loggingFailure}");
        }
    }

    private static string ResolveLogPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "MonoBlackjack", "logs", $"app-{DateTime.UtcNow:yyyyMMdd}.log");
    }
}
