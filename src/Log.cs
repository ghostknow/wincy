using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Wincy
{
    /// <summary>Small rolling log file; never throws into the caller.</summary>
    internal static class Log
    {
        private static readonly object Gate = new object();
        private const long MaxBytes = 512 * 1024;

        public static bool Enabled = true;

        public static void Info(string message)
        {
            Append("INFO ", message);
        }

        public static void Warn(string message)
        {
            Append("WARN ", message);
        }

        public static void Error(string message, Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(message);
            if (ex != null)
            {
                sb.Append(" :: ").Append(ex.GetType().FullName).Append(": ").Append(ex.Message);
                if (!string.IsNullOrEmpty(ex.StackTrace)) sb.Append(Environment.NewLine).Append(ex.StackTrace);
                Exception inner = ex.InnerException;
                int depth = 0;
                while (inner != null && depth++ < 3)
                {
                    sb.Append(Environment.NewLine).Append("  inner: ").Append(inner.GetType().FullName)
                        .Append(": ").Append(inner.Message);
                    inner = inner.InnerException;
                }
            }
            Append("ERROR", sb.ToString());
        }

        private static void Append(string level, string message)
        {
            if (!Enabled) return;
            try
            {
                lock (Gate)
                {
                    Paths.EnsureDirectories();
                    string path = Paths.LogFile;
                    FileInfo fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > MaxBytes)
                    {
                        string old = path + ".old";
                        try
                        {
                            if (File.Exists(old)) File.Delete(old);
                            File.Move(path, old);
                        }
                        catch
                        {
                        }
                    }
                    string line = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss.fff} {1} {2}{3}",
                        DateTime.Now, level, message, Environment.NewLine);
                    File.AppendAllText(path, line, new UTF8Encoding(false));
                }
            }
            catch
            {
                // Logging must never take the app down.
            }
        }
    }
}
