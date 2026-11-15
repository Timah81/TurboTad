using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

namespace TurboTad
{
    internal static class InputNames
    {
        internal static string Describe(InputSource source)
        {
            if (source == null) return "Unassigned";
            if (source.Kind == "key") return "Key " + ((Keys)source.Code).ToString();
            int player;
            if (InputSource.TryXboxPlayer(source.Device, out player))
                return "Xbox " + (player + 1) + " / " + (source.Kind == "xaxis" ?
                    XInputProbe.AxisName(source.Code) : XInputProbe.ButtonName(source.Code));
            string[] parts = source.Device.Split(new char[]{':'},4);
            string name = parts.Length == 4 ? "#" + (UInt32.Parse(parts[0])+1) + " " + parts[3] : source.Device;
            return name + (source.Kind == "axis" ? " / Axis " : " / Button ") + (source.Code+1);
        }
    }

    internal sealed class InputCaptureDialog : Form
    {
        private readonly IList<Device> devices;
        private readonly Dictionary<string,bool> previous = new Dictionary<string,bool>();
        private readonly Timer timer = new Timer();
        private InputSource pending;
        private readonly Stopwatch captureWait = new Stopwatch();
        internal InputSource Selected;
        internal InputCaptureDialog(IList<Device> available)
        {
            devices = available; Text = "Assign a key, joystick button or Xbox button";
            ClientSize = new Size(540,150); StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Segoe UI",10F);
            Label help = new Label { Text = "Press a keyboard key, joystick or Xbox button. Xbox buttons use XInput.\r\nRelease held controls first. Escape cancels.", AutoSize = false };
            help.SetBounds(20,20,500,65); Controls.Add(help);
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            cancel.SetBounds(400,105,115,30);Controls.Add(cancel);CancelButton=cancel;
            Snapshot(false); timer.Interval = 16; timer.Tick += delegate { if (ContainsFocus) Snapshot(true); else Snapshot(false); };
            Shown += delegate { Snapshot(false); timer.Start(); };
            FormClosed += delegate { timer.Stop(); timer.Dispose(); };
        }
        private void Observe(InputSource source, bool down, bool capture)
        {
            bool before;
            if (!previous.TryGetValue(source.Identity, out before)) before = true;
            previous[source.Identity] = down;
            // Wait briefly before committing legacy/keyboard captures. An Xbox
            // press can arrive through WinMM and XInput on different poll ticks.
            if (capture && down && !before && InputMapper.PreferButtonLearnCandidate(source, pending))
            {
                pending = source;
                captureWait.Restart();
            }
        }
        private void Snapshot(bool capture)
        {
            if (!capture) { pending = null; captureWait.Reset(); }
            for (int key = 8; key <= 254; key++)
            {
                if (key == 27 || key == 16 || key == 17 || key == 18) continue;
                Observe(InputSource.Key(key), (Native.GetAsyncKeyState(key) & 0x8000) != 0, capture);
            }
            foreach (Device d in devices)
            {
                d.Poll(); if (!d.Connected) continue;
                for (int b = 0; b < Math.Min(32, (int)d.Caps.Buttons); b++)
                    Observe(InputSource.Joystick(d.Identity, b, false), (d.Buttons & (1U << b)) != 0, capture);
            }
            for (int player = 0; player < 4; player++)
            {
                XInputProbe.State state; string issue;
                if (!XInputProbe.TryRead((uint)player, out state, out issue)) continue;
                for (int b = 0; b < XInputProbe.ButtonBits.Length; b++)
                    Observe(InputSource.XboxButton(player, b), XInputProbe.ButtonDown(state, b), capture);
            }
            if (capture && pending != null &&
                (pending.Kind == "xbutton" || captureWait.ElapsedMilliseconds >= 160))
            {
                Selected = pending;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }

    internal sealed class DirectionPairDialog : Form
    {
        private sealed class Choice
        {
            internal readonly InputSource Source;
            internal Choice(InputSource source) { Source = source; }
            public override string ToString()
            { return Source == null ? "Unassigned" : InputNames.Describe(Source); }
        }

        private InputSource negative, positive;
        private readonly ComboBox negativeChoice = new ComboBox(), positiveChoice = new ComboBox();
        private readonly Button negativeLearn = new Button(), positiveLearn = new Button();
        internal AxisBinding Binding;

        internal DirectionPairDialog(string axisName, string negativeName, string positiveName,
            AxisBinding current, IList<Device> devices)
            : this(axisName, negativeName, positiveName, current, devices, null) {}

        internal DirectionPairDialog(string axisName, string negativeName, string positiveName,
            AxisBinding current, IList<Device> devices, InputDiscoveryCatalog discovered)
        {
            Text = "TurboTad — " + axisName; ClientSize = new Size(710, 272);
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Segoe UI", 10F); BackColor = Color.FromArgb(37, 40, 44);
            ForeColor = Color.WhiteSmoke;
            negative = current.Negative; positive = current.Positive;

            Label help = new Label {
                Text = "Choose a button directly or click LEARN. Xbox selections use XInput. Both held = neutral.",
                AutoSize = false
            };
            help.SetBounds(18, 15, 670, 44); Controls.Add(help);
            Label n = new Label { Text = negativeName + "  (−)" };
            Label p = new Label { Text = positiveName + "  (+)" };
            n.SetBounds(18, 79, 175, 28); p.SetBounds(18, 134, 175, 28);
            Controls.Add(n); Controls.Add(p);

            Configure(negativeChoice, 200, 72);
            Configure(positiveChoice, 200, 127);
            Controls.Add(negativeChoice); Controls.Add(positiveChoice);
            Populate(negativeChoice, negative, devices, discovered);
            Populate(positiveChoice, positive, devices, discovered);
            negativeChoice.SelectedIndexChanged += delegate {
                Choice selected = negativeChoice.SelectedItem as Choice;
                negative = selected == null ? null : selected.Source;
            };
            positiveChoice.SelectedIndexChanged += delegate {
                Choice selected = positiveChoice.SelectedItem as Choice;
                positive = selected == null ? null : selected.Source;
            };

            negativeLearn.Text = "LEARN";
            negativeLearn.SetBounds(575, 72, 105, 33);
            positiveLearn.Text = "LEARN";
            positiveLearn.SetBounds(575, 127, 105, 33);
            Controls.Add(negativeLearn); Controls.Add(positiveLearn);
            negativeLearn.Click += delegate {
                using (InputCaptureDialog d = new InputCaptureDialog(devices))
                    if (d.ShowDialog(this) == DialogResult.OK) Select(negativeChoice, d.Selected);
            };
            positiveLearn.Click += delegate {
                using (InputCaptureDialog d = new InputCaptureDialog(devices))
                    if (d.ShowDialog(this) == DialogResult.OK) Select(positiveChoice, d.Selected);
            };

            Button clear = new Button { Text = "Clear pair" };
            clear.SetBounds(18, 202, 125, 34);
            clear.Click += delegate { negativeChoice.SelectedIndex = 0; positiveChoice.SelectedIndex = 0; };
            Controls.Add(clear);
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            cancel.SetBounds(427, 202, 120, 34); Controls.Add(cancel); CancelButton = cancel;
            Button save = new Button { Text = "Use pair" };
            save.SetBounds(562, 202, 125, 34); Controls.Add(save);
            save.Click += delegate {
                if (negative != null && positive != null && negative.Identity == positive.Identity)
                {
                    MessageBox.Show(this, "Choose a different control for each direction."); return;
                }
                Binding = new AxisBinding { Digital = true, Negative = negative, Positive = positive };
                DialogResult = DialogResult.OK; Close();
            };
        }

        private static void Configure(ComboBox combo, int x, int y)
        {
            combo.SetBounds(x, y, 365, 33);
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = Color.FromArgb(50, 54, 59);
            combo.ForeColor = Color.WhiteSmoke;
            combo.IntegralHeight = false;
            combo.DropDownHeight = 330;
        }

        private static void Populate(ComboBox combo, InputSource selected, IList<Device> devices,
            InputDiscoveryCatalog discovered)
        {
            combo.Items.Add(new Choice(null));
            // List all XInput slots regardless of current connection so binding
            // is possible before a wireless controller wakes up.
            for (int player = 0; player < 4; player++)
                for (int button = 0; button < XInputProbe.ButtonBits.Length; button++)
                    combo.Items.Add(new Choice(InputSource.XboxButton(player, button)));
            foreach (Device d in devices)
                for (int b = 0; b < Math.Min(32, (int)d.Caps.Buttons); b++)
                    combo.Items.Add(new Choice(InputSource.Joystick(d.Identity, b, false)));

            // The physically exercised controls come first on both direction
            // pickers, while the complete supported fallback list remains.
            int insertAt = 1;
            if (discovered != null)
                foreach (InputSource source in discovered.OfType(false))
                {
                    Choice match = null;
                    foreach (Choice candidate in combo.Items)
                        if (candidate.Source != null && candidate.Source.Identity == source.Identity)
                        { match = candidate; break; }
                    if (match == null && (source.Kind == "key" || source.Kind == "xbutton"))
                        match = new Choice(source);
                    if (match == null) continue;
                    combo.Items.Remove(match);
                    combo.Items.Insert(insertAt++, match);
                }
            Select(combo, selected); // Preserve saved keyboard and unplugged-device sources.
        }

        private static void Select(ComboBox combo, InputSource source)
        {
            string id = source == null ? null : source.Identity;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                InputSource candidate = ((Choice)combo.Items[i]).Source;
                if (candidate == null ? id == null : candidate.Identity == id)
                { combo.SelectedIndex = i; return; }
            }
            // LEARN can capture a keyboard key or a device not present at dialog
            // creation time. Keep it selectable without altering other options.
            combo.Items.Add(new Choice(source));
            combo.SelectedIndex = combo.Items.Count - 1;
        }
    }
}
