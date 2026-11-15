using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TurboTad
{
    // Passive, optional XInput reader. No calls to the mapping or flight output paths.
    internal static class XInputProbe
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Gamepad
        {
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short LeftX, LeftY, RightX, RightY;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct State
        {
            public uint PacketNumber;
            public Gamepad Pad;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState", ExactSpelling = true)]
        private static extern uint GetState14(uint player, out State state);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState", ExactSpelling = true)]
        private static extern uint GetState91(uint player, out State state);

        // 0 = modern Windows XInput, 1 = legacy Windows fallback, 2 = unavailable.
        private static int provider;

        internal static bool TryRead(uint player, out State state, out string issue)
        {
            state = new State();
            issue = null;
            if (provider == 2)
            {
                issue = "XInput runtime unavailable";
                return false;
            }
            if (provider == 0)
            {
                try { return GetState14(player, out state) == 0; }
                catch (DllNotFoundException) { provider = 1; }
                catch (EntryPointNotFoundException) { provider = 1; }
                catch (BadImageFormatException) { provider = 1; }
            }
            try { return GetState91(player, out state) == 0; }
            catch (DllNotFoundException) { provider = 2; }
            catch (EntryPointNotFoundException) { provider = 2; }
            catch (BadImageFormatException) { provider = 2; }
            issue = "XInput runtime unavailable";
            return false;
        }

        internal static readonly ushort[] ButtonBits = {
            0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020,
            0x0040, 0x0080, 0x0100, 0x0200,
            0x1000, 0x2000, 0x4000, 0x8000
        };
        private static readonly string[] ButtonNames = {
            "D-Up", "D-Down", "D-Left", "D-Right", "Start", "Back",
            "L3", "R3", "LB", "RB", "A", "B", "X", "Y"
        };

        internal const int AxisCount = 6;
        internal static string AxisName(int axis)
        {
            string[] names = { "LX", "LY", "RX", "RY", "LT", "RT" };
            return axis >= 0 && axis < names.Length ? names[axis] : "?";
        }
        internal static string ButtonName(int button)
        { return button >= 0 && button < ButtonNames.Length ? ButtonNames[button] : "?"; }
        internal static double AxisValue(State state, int axis)
        {
            short v;
            switch (axis)
            {
                case 0: v = state.Pad.LeftX; break;
                case 1: v = state.Pad.LeftY; break;
                case 2: v = state.Pad.RightX; break;
                case 3: v = state.Pad.RightY; break;
                // Triggers are separate unipolar (0..1) proportional inputs.
                case 4: return state.Pad.LeftTrigger / 255.0;
                case 5: return state.Pad.RightTrigger / 255.0;
                default: throw new ArgumentOutOfRangeException("axis");
            }
            return v < 0 ? v / 32768.0 : v / 32767.0;
        }
        internal static bool ButtonDown(State state, int button)
        { return button >= 0 && button < ButtonBits.Length && (state.Pad.Buttons & ButtonBits[button]) != 0; }

        internal static string DescribeButtons(ushort buttons)
        {
            List<string> pressed = new List<string>();
            for (int i = 0; i < ButtonBits.Length; i++)
                if ((buttons & ButtonBits[i]) != 0) pressed.Add(ButtonNames[i]);
            return pressed.Count == 0 ? "none" : String.Join(" ", pressed.ToArray());
        }

        internal static string Describe(uint player, bool connected, State state, string issue)
        {
            string prefix = "Controller " + (player + 1).ToString(CultureInfo.InvariantCulture);
            if (issue != null) return prefix + ": " + issue;
            if (!connected) return prefix + ": not detected";
            Gamepad pad = state.Pad;
            return prefix + ": CONNECTED\r\n" +
                String.Format(CultureInfo.InvariantCulture,
                    "  LX:{0,6}  LY:{1,6}  RX:{2,6}  RY:{3,6}  LT:{4,3}  RT:{5,3}\r\n",
                    pad.LeftX, pad.LeftY, pad.RightX, pad.RightY, pad.LeftTrigger, pad.RightTrigger) +
                "  Buttons: " + DescribeButtons(pad.Buttons);
        }
    }

    // Isolated input diagnostic: displays readings, never binds or sends them to the game.
    internal sealed class XInputDiagnosticDialog : Form
    {
        private readonly Label[] readouts = new Label[4];
        private readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();

        internal XInputDiagnosticDialog()
        {
            Text = "TurboTad - XInput controller diagnostic (read-only)";
            ClientSize = new Size(740, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(24, 26, 29);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 10F);

            Label guidance = new Label();
            guidance.Text = "Move sticks, squeeze triggers and press buttons to check detection.\r\n" +
                "Diagnostic only: no controller bindings, flight commands or game settings change.";
            guidance.SetBounds(20, 12, 700, 53);
            Controls.Add(guidance);

            for (int i = 0; i < readouts.Length; i++)
            {
                Label label = new Label();
                label.SetBounds(20, 68 + i * 96, 700, 87);
                label.Padding = new Padding(10, 7, 6, 5);
                label.Font = new Font("Consolas", 10F);
                label.ForeColor = Color.FromArgb(255, 143, 40);
                label.BackColor = Color.FromArgb(37, 40, 44);
                label.Text = "Controller " + (i + 1) + ": checking...";
                Controls.Add(label);
                readouts[i] = label;
            }

            Button close = new Button();
            close.Text = "CLOSE";
            close.SetBounds(605, 466, 113, 37);
            close.DialogResult = DialogResult.OK;
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;

            refreshTimer.Interval = 80;
            refreshTimer.Tick += delegate { UpdateReadouts(); };
            Shown += delegate { UpdateReadouts(); refreshTimer.Start(); };
            FormClosed += delegate { refreshTimer.Stop(); refreshTimer.Dispose(); };
        }

        private void UpdateReadouts()
        {
            for (uint i = 0; i < (uint)readouts.Length; i++)
            {
                XInputProbe.State state;
                string issue;
                bool connected = XInputProbe.TryRead(i, out state, out issue);
                readouts[(int)i].Text = XInputProbe.Describe(i, connected, state, issue);
            }
        }
    }
}
