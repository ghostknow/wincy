using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Wincy.Tests
{
    /// <summary>
    /// Tiny editable target used by the end-to-end paste test: whatever lands in the
    /// text box is mirrored to a file so the test can assert on it.
    /// </summary>
    internal static class PasteTarget
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "wincy-paste-target.txt");
            Application.EnableVisualStyles();

            Form form = new Form();
            form.Text = "Wincy Paste Target";
            form.Width = 640;
            form.Height = 320;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(120, 120);

            TextBox box = new TextBox();
            box.Multiline = true;
            box.Dock = DockStyle.Fill;
            box.Font = new System.Drawing.Font("Consolas", 12f);
            box.ScrollBars = ScrollBars.Vertical;
            box.AcceptsReturn = true;
            box.TextChanged += delegate
            {
                try
                {
                    File.WriteAllText(output, box.Text, new UTF8Encoding(false));
                }
                catch
                {
                }
            };
            form.Controls.Add(box);

            try
            {
                File.WriteAllText(output, string.Empty, new UTF8Encoding(false));
            }
            catch
            {
            }

            form.Shown += delegate { box.Focus(); };
            Application.Run(form);
        }
    }
}
