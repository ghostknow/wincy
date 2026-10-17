using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Wincy
{
    /// <summary>Resolves the data folder and does safe, atomic file writes.</summary>
    internal static class Paths
    {
        private static string _root;

        public static string Root
        {
            get
            {
                if (_root == null) _root = ResolveRoot();
                return _root;
            }
        }

        /// <summary>Test hook: point the data folder at a scratch directory.</summary>
        internal static void UseRoot(string path)
        {
            _root = path;
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(Path.Combine(path, "images"));
        }

        public static string SettingsFile
        {
            get { return Path.Combine(Root, "settings.json"); }
        }

        public static string HistoryFile
        {
            get { return Path.Combine(Root, "history.json"); }
        }

        public static string ImageDir
        {
            get { return Path.Combine(Root, "images"); }
        }

        public static string LogFile
        {
            get { return Path.Combine(Root, "wincy.log"); }
        }

        public static string ExecutablePath
        {
            get { return Assembly.GetEntryAssembly().Location; }
        }

        public static string ExecutableName
        {
            get { return Path.GetFileNameWithoutExtension(ExecutablePath); }
        }

        private static string ResolveRoot()
        {
            // Portable mode: drop an empty "portable.txt" next to Wincy.exe.
            try
            {
                string exeDir = Path.GetDirectoryName(ExecutablePath);
                if (!string.IsNullOrEmpty(exeDir) && File.Exists(Path.Combine(exeDir, "portable.txt")))
                {
                    string portable = Path.Combine(exeDir, "WincyData");
                    Directory.CreateDirectory(portable);
                    return portable;
                }
            }
            catch
            {
                // fall through to %APPDATA%
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData)) appData = Path.GetTempPath();
            return Path.Combine(appData, "Wincy");
        }

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(ImageDir);
        }

        public static string ReadAllText(string path)
        {
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path, Encoding.UTF8);
        }

        /// <summary>Write via a temp file + replace so a crash never leaves a half file.</summary>
        public static void WriteAllTextAtomic(string path, string content)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string temp = path + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, null, true);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error("File.Replace failed for " + path, ex);
                }
            }
            File.Copy(temp, path, true);
            try
            {
                File.Delete(temp);
            }
            catch
            {
            }
        }


        /// <summary>Full path of an item's PNG blob, or null when it has none.</summary>
        public static string ImageFullPathFor(ClipboardItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Blob)) return null;
            return Path.Combine(Root, item.Blob.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
