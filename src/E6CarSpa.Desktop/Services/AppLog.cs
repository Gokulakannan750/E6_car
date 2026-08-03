using System.IO;
using System.Text;

namespace E6CarSpa.Desktop.Services;

/// <summary>
/// Minimal rolling file log for the desktop client.
///
/// The app previously wrote nothing anywhere, so "it closed by itself yesterday" from the shop
/// was unanswerable — there was no artefact to look at. This gives every crash and handled
/// failure a timestamped line on disk.
///
/// Deliberately hand-rolled rather than pulling in a logging framework: the desktop publishes as
/// a self-contained single file, and one small append-only writer avoids adding megabytes and a
/// configuration surface for what is a few lines a day.
///
/// Logs live under %LOCALAPPDATA% because the install directory (Program Files) is not writable
/// by the signed-in user.
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private const int KeepDays = 14;

    /// <summary>Folder holding the log files. Created on first write.</summary>
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "E6CarSpa", "logs");

    private static string TodayFile =>
        Path.Combine(Folder, $"desktop-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info(string message) => Write("INFO ", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    /// <summary>
    /// Append one entry. Never throws: a logger that can break the app is worse than no logger,
    /// and this is called from crash handlers where a secondary failure would mask the original.
    /// </summary>
    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);

                var sb = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append("  ").Append(level).Append("  ").Append(message);

                if (ex is not null)
                {
                    // Type, message and stack are what diagnosis needs. Request/response bodies
                    // are deliberately NOT logged — they carry customer data and bearer tokens.
                    sb.AppendLine()
                      .Append("    ").Append(ex.GetType().FullName).Append(": ").AppendLine(ex.Message)
                      .Append(ex.StackTrace);

                    var inner = ex.InnerException;
                    while (inner is not null)
                    {
                        sb.AppendLine()
                          .Append("    caused by ").Append(inner.GetType().FullName)
                          .Append(": ").Append(inner.Message);
                        inner = inner.InnerException;
                    }
                }

                File.AppendAllText(TodayFile, sb.AppendLine().ToString());
            }
        }
        catch
        {
            // Disk full, permissions, whatever — losing a log line must never take the app down.
        }
    }

    /// <summary>Delete logs older than the retention window. Best-effort, called at startup.</summary>
    public static void Prune()
    {
        try
        {
            if (!Directory.Exists(Folder)) return;
            var cutoff = DateTime.Now.AddDays(-KeepDays);
            foreach (var file in Directory.GetFiles(Folder, "desktop-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff) File.Delete(file);
            }
        }
        catch { /* best effort */ }
    }
}
