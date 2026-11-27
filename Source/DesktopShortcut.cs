using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;

namespace TurboTad
{
    internal static class DesktopShortcut
    {
        private const string FileName = "TurboTad.lnk";
        private const string OfferMarker = "desktop-shortcut-offered";

        internal static string DesktopDirectory()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        internal static bool TryBeginOffer(string localAppData, string desktop)
        {
            // Reserve the one-time offer before showing it. No, close, and a failed
            // creation all leave the manual About action available.
            try
            {
                if (String.IsNullOrEmpty(localAppData) || String.IsNullOrEmpty(desktop) ||
                    !Directory.Exists(desktop) || File.Exists(Path.Combine(desktop, FileName))) return false;
                string settings = Path.Combine(localAppData, "TurboTad");
                Directory.CreateDirectory(settings);
                using (File.Open(Path.Combine(settings, OfferMarker), FileMode.CreateNew, FileAccess.Write)) { }
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        internal static bool Create(string desktop, string executable)
        {
            return Create(desktop, executable, SaveShellLink);
        }

        internal static bool Create(string desktop, string executable, Action<string, string, string> saveLink)
        {
            if (String.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
                throw new DirectoryNotFoundException("Windows could not find your desktop folder.");
            string shortcut = Path.Combine(desktop, FileName);
            if (File.Exists(shortcut)) return false;
            string target = Path.GetFullPath(executable);
            if (!File.Exists(target)) throw new FileNotFoundException("The TurboTad executable could not be found.");

            // Save beside the final file so promotion is a same-directory move.
            // File.Move preserves any existing link, including one created concurrently.
            string pending = Path.Combine(desktop, ".TurboTad-" + Guid.NewGuid().ToString("N") + ".lnk");
            try
            {
                saveLink(pending, target, Path.GetDirectoryName(target));
                try { File.Move(pending, shortcut); }
                catch (IOException) { if (File.Exists(shortcut)) return false; throw; }
                return true;
            }
            finally
            {
                try { File.Delete(pending); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void SaveShellLink(string path, string target, string workingDirectory)
        {
            object instance = new ShellLink();
            try
            {
                IShellLink link = (IShellLink)instance;
                link.SetPath(target);
                link.SetWorkingDirectory(workingDirectory);
                link.SetIconLocation(target, 0); // Existing icon embedded in the companion.
                ((IPersistFile)instance).Save(path, true);
            }
            finally { Marshal.FinalReleaseComObject(instance); }
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        // Entries through SetPath are declared in the published IShellLinkW vtable order.
        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"),
            InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLink
        {
            void GetPath(IntPtr path, int capacity, IntPtr findData, uint flags);
            void GetIDList(out IntPtr item);
            void SetIDList(IntPtr item);
            void GetDescription(IntPtr description, int capacity);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
            void GetWorkingDirectory(IntPtr directory, int capacity);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
            void GetArguments(IntPtr arguments, int capacity);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out short hotkey);
            void SetHotkey(short hotkey);
            void GetShowCmd(out int command);
            void SetShowCmd(int command);
            void GetIconLocation(IntPtr path, int capacity, out int index);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
            void Resolve(IntPtr window, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }
    }

    public sealed partial class ControlsWindow
    {
        private void OfferDesktopShortcut()
        {
            string desktop;
            try
            {
                desktop = DesktopShortcut.DesktopDirectory();
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!DesktopShortcut.TryBeginOffer(localAppData, desktop)) return;
            }
            catch (Exception) { return; } // A folder lookup must never stop the companion.

            if (MessageBox.Show(this,
                "Create a TurboTad shortcut on your desktop? You can also do this later from About.",
                "TurboTad", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
                == DialogResult.Yes)
                CreateDesktopShortcut(this);
        }

        private static void CreateDesktopShortcut(IWin32Window owner)
        {
            try
            {
                bool created = DesktopShortcut.Create(DesktopShortcut.DesktopDirectory(), Application.ExecutablePath);
                string message = created ? "TurboTad was added to your desktop." :
                    "Your desktop already has a shortcut named TurboTad.";
                MessageBox.Show(owner, message, "TurboTad", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                MessageBox.Show(owner, "Could not create the desktop shortcut.\r\n\r\n" + error.Message,
                    "TurboTad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowAbout()
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "About TurboTad";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.ShowInTaskbar = false;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.AutoSize = true;
                dialog.AutoSizeMode = AutoSizeMode.GrowAndShrink;

                TableLayoutPanel layout = new TableLayoutPanel { AutoSize = true, Padding = new Padding(16) };
                Label details = new Label { AutoSize = true, MaximumSize = new Size(520, 0) };
                details.Text = BuildTitle + "  •  Flight Configurator\r\n\r\nSix-axis analog Tadpole controls with linked flight and boost speed.\r\n\r\n" +
                    "The rotation settings are engine-input multipliers, not calibrated degrees/second. " +
                    "Estimated mph assumes a native 1000 cm/s swim cap observed on the tested build.\r\n\r\n" +
                    "Runs locally. No network telemetry, administrator rights or controller-driver changes.\r\n\r\n" +
                    "WARNING: This mod can break your game. Breaching the water surface at high speed can launch the Tadpole out of the map. Back up your saves and slow down before surfacing.";
                layout.Controls.Add(details, 0, 0);

                FlowLayoutPanel actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
                Button shortcut = new Button { Text = "Create Desktop Shortcut", AutoSize = true };
                shortcut.Click += delegate { CreateDesktopShortcut(dialog); };
                Button close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, AutoSize = true };
                actions.Controls.Add(shortcut);
                actions.Controls.Add(close);
                layout.Controls.Add(actions, 0, 1);
                dialog.Controls.Add(layout);
                dialog.CancelButton = close;
                dialog.Shown += delegate { close.Focus(); };
                dialog.ShowDialog(this);
            }
        }
    }
}
