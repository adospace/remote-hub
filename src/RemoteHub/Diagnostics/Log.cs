using System.IO;
using System.Text;

namespace RemoteHub.Diagnostics;

/// <summary>
/// Minimal thread-safe file logger. Writes to <c>%AppData%\RemoteHub\logs\remotehub-yyyymmdd.log</c>.
/// Logging must never throw, so all failures are swallowed.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemoteHub", "logs");

    private static string FilePath => Path.Combine(Directory, $"remotehub-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var sb = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(level).Append("] ")
                .Append(message);
            if (ex is not null)
            {
                sb.AppendLine().Append(ex);
            }

            lock (Gate)
            {
                File.AppendAllText(FilePath, sb.Append(Environment.NewLine).ToString());
            }
        }
        catch
        {
            // Never let logging crash the app.
        }
    }
}
