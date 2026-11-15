using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TurboTad
{
    // Scans observable changes, rather than assuming every declared control was
    // physically moved. Discovery supplements the existing device enumeration.
    internal sealed class DeviceDiscoveryDialog : Form
    {
        private readonly IList<Device> joysticks;
        private readonly InputDiscoveryCatalog existing;
        private readonly InputDiscoveryCatalog detected = new InputDiscoveryCatalog();
        private readonly Dictionary<string,double> neutral = new Dictionary<string,double>();
        private readonly Dictionary<string,bool> previous = new Dictionary<string,bool>();
        private readonly Timer poll = new Timer();
        private readonly ListView results = new ListView();
        private readonly Label status = new Label();
        private readonly Button begin = new Button();
        private bool scanning;
        internal InputDiscoveryCatalog Detected { get { return detected; } }

        internal DeviceDiscoveryDialog(IList<Device> devices, InputDiscoveryCatalog prior)
        {
            joysticks = devices;
            existing = prior ?? new InputDiscoveryCatalog();
            Text = "TurboTad — Discover Controls";
            ClientSize = new Size(750, 525);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(37, 40, 44);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 10F);
            KeyPreview = true;

            Label intro = new Label {
                Text = "Release all controls, then click START SCAN.\r\nMove every stick and pedal, squeeze both triggers and press each button.\r\nDiscovered inputs will appear first in the binding menus. Unscanned inputs stay available.",
                AutoSize = false, ForeColor = Color.WhiteSmoke
            };
            intro.SetBounds(22, 17, 700, 75);
            Controls.Add(intro);

            results.View = View.Details;
            results.FullRowSelect = true;
            results.GridLines = false;
            results.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            results.BackColor = Color.FromArgb(48, 52, 57);
            results.ForeColor = Color.WhiteSmoke;
            results.Columns.Add("Detected control", 545);
            results.Columns.Add("Type", 155);
            results.SetBounds(22, 106, 705, 310);
            Controls.Add(results);

            status.Text = "Previously discovered: " + existing.Count + " | New this scan: 0";
            status.SetBounds(22, 428, 565, 24);
            Controls.Add(status);

            begin.Text = "START SCAN";
            begin.SetBounds(22, 468, 174, 37);
            begin.Click += delegate {
                scanning = false;
                CaptureBaseline();
                scanning = true;
                begin.Text = "RESTART SCAN";
                status.Text = "Scanning — move and press each control. New: " + detected.Count;
                poll.Start();
            };
            Controls.Add(begin);

            Button cancel = new Button { Text = "CANCEL", DialogResult = DialogResult.Cancel };
            cancel.SetBounds(474, 468, 116, 37);
            Controls.Add(cancel);
            CancelButton = cancel;
            Button done = new Button { Text = "USE RESULTS", DialogResult = DialogResult.OK };
            done.SetBounds(599, 468, 128, 37);
            Controls.Add(done);

            poll.Interval = 32;
            poll.Tick += delegate { if (scanning && ContainsFocus) ObserveInputs(); };
            KeyDown += delegate(object sender, KeyEventArgs e) {
                if (!scanning || e.KeyCode == Keys.Escape || e.KeyCode == Keys.Tab ||
                    e.KeyCode == Keys.Enter || e.KeyCode == Keys.Menu ||
                    e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.ControlKey) return;
                InputSource key = InputSource.Key((int)e.KeyCode);
                if (key.Valid(false)) Record(key);
            };
            FormClosed += delegate { scanning = false; poll.Stop(); poll.Dispose(); };
        }

        private void Record(InputSource source)
        {
            if (!detected.Add(source)) return;
            string label = InputNames.Describe(source);
            if (source.Kind == "axis" || source.Kind == "button")
            {
                foreach (Device d in joysticks)
                    if (d.Identity == source.Device)
                    {
                        label = source.Kind == "axis"
                            ? new AxisChoice { Device = d, Axis = source.Code }.ToString()
                            : "#" + (d.Id + 1) + " " + d.Caps.Name + " / Button " + (source.Code + 1);
                        break;
                    }
            }
            results.Items.Add(new ListViewItem(new string[] {
                label, source.Kind == "axis" || source.Kind == "xaxis" ? "Analog" : "Button / key"
            }));
            results.EnsureVisible(results.Items.Count - 1);
            status.Text = "Previously discovered: " + existing.Count + " | New this scan: " + detected.Count;

            // The existing signed LT+RT option is virtual; add it when both
            // physical triggers were observed, never infer a trigger was used.
            int player;
            if (source.Kind == "xaxis" && (source.Code == 4 || source.Code == 5) &&
                InputSource.TryXboxPlayer(source.Device, out player))
            {
                InputSource other = InputSource.XboxAxis(player, source.Code == 4 ? 5 : 4);
                if (detected.Contains(other) || existing.Contains(other))
                    Record(InputSource.XboxAxis(player, 6));
            }
        }

        private void CaptureBaseline()
        {
            neutral.Clear();
            previous.Clear();
            foreach (Device d in joysticks)
            {
                d.Poll();
                if (!d.Connected) continue;
                for (int axis = 0; axis < 6; axis++)
                    if (d.Supports(axis))
                        neutral[InputSource.Joystick(d.Identity, axis, true).Identity] = d.Values[axis];
                for (int b = 0; b < Math.Min(32,(int)d.Caps.Buttons); b++)
                    previous[InputSource.Joystick(d.Identity, b, false).Identity] =
                        (d.Buttons & (1U << b)) != 0;
            }
            for (int player = 0; player < 4; player++)
            {
                XInputProbe.State state; string issue;
                if (!XInputProbe.TryRead((uint)player, out state, out issue)) continue;
                for (int axis = 0; axis < 6; axis++)
                    neutral[InputSource.XboxAxis(player, axis).Identity] = XInputProbe.AxisValue(state, axis);
                for (int b = 0; b < XInputProbe.ButtonBits.Length; b++)
                    previous[InputSource.XboxButton(player,b).Identity] = XInputProbe.ButtonDown(state,b);
            }
        }

        private void TrackAxis(InputSource axis, double current)
        {
            double start;
            if (!neutral.TryGetValue(axis.Identity, out start))
            {
                neutral[axis.Identity] = current;
                return;
            }
            if (Math.Abs(current - start) >= .32)
                Record(axis);
        }

        private void TrackButton(InputSource button, bool down)
        {
            bool prior;
            if (!previous.TryGetValue(button.Identity, out prior))
            {
                previous[button.Identity] = down;
                return;
            }
            previous[button.Identity] = down;
            if (down && !prior) Record(button);
        }

        private void ObserveInputs()
        {
            // XInput first: on mirrored devices show the Xbox identity before
            // the legacy WinMM mirror in the observed-input list.
            for (int player=0; player<4; player++)
            {
                XInputProbe.State state; string issue;
                if (!XInputProbe.TryRead((uint)player,out state,out issue)) continue;
                for (int axis=0; axis<6; axis++)
                    TrackAxis(InputSource.XboxAxis(player,axis),XInputProbe.AxisValue(state,axis));
                for (int b=0; b<XInputProbe.ButtonBits.Length; b++)
                    TrackButton(InputSource.XboxButton(player,b),XInputProbe.ButtonDown(state,b));
            }
            foreach (Device d in joysticks)
            {
                d.Poll();
                if (!d.Connected) continue;
                for (int axis=0; axis<6; axis++)
                    if (d.Supports(axis))
                        TrackAxis(InputSource.Joystick(d.Identity,axis,true),d.Values[axis]);
                for (int b=0; b<Math.Min(32,(int)d.Caps.Buttons); b++)
                    TrackButton(InputSource.Joystick(d.Identity,b,false),(d.Buttons & (1U << b)) != 0);
            }
        }
    }
}
