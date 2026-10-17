using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Wincy
{
    internal static class Program
    {
        private static Mutex _instanceMutex;

        private static readonly Dictionary<string, int> CommandLine = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "--show", AppCommand.Show },
            { "-s", AppCommand.Show },
            { "--toggle", AppCommand.Toggle },
            { "--settings", AppCommand.Settings },
            { "--about", AppCommand.About },
            { "--clear", AppCommand.Clear },
            { "--quit", AppCommand.Quit },
            { "--exit", AppCommand.Quit }
        };

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    int dpi = (int)Math.Round(g.DpiX);
                    if (dpi >= 48 && dpi <= 480) Dpi.Value = dpi;
                }
            }
            catch
            {
            }

            int command = ParseCommand(args);

            bool isFirstInstance;
            _instanceMutex = new Mutex(true, "Wincy.Windows.SingleInstance", out isFirstInstance);
            if (!isFirstInstance)
            {
                // Forward the request to the running copy, then leave.
                if (command != 0) MessageWindow.SendCommand(command);
                else MessageWindow.BroadcastShow();
                GC.KeepAlive(_instanceMutex);
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;

            try
            {
                using (WincyApp app = new WincyApp())
                {
                    if (command != 0) app.HandleCommand(command);
                    Application.Run(app);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Fatal startup error", ex);
                MessageBox.Show(
                    I18n.T("errors.unexpected") + Environment.NewLine + Environment.NewLine + ex.Message,
                    "Wincy", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                GC.KeepAlive(_instanceMutex);
            }
        }

        private static int ParseCommand(string[] args)
        {
            if (args == null) return 0;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrEmpty(arg)) continue;
                int command;
                if (CommandLine.TryGetValue(arg.Trim(), out command)) return command;
            }
            return 0;
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log.Error("Unhandled UI exception", e.Exception);
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error("Unhandled domain exception", e.ExceptionObject as Exception);
        }
    }
}
