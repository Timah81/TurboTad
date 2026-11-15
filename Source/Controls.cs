using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Reflection;
using System.Media;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace TurboTad
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct Caps
        {
            public ushort Mid, Pid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            public uint Xmin, Xmax, Ymin, Ymax, Zmin, Zmax;
            public uint Buttons, PeriodMin, PeriodMax;
            public uint Rmin, Rmax, Umin, Umax, Vmin, Vmax;
            public uint Flags, MaxAxes, NumAxes, MaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string RegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string OemVxd;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Info
        {
            public uint Size, Flags, X, Y, Z, R, U, V;
            public uint Buttons, ButtonNumber, Pov, Reserved1, Reserved2;
        }
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern uint joyGetDevCapsW(UIntPtr id, out Caps caps, uint size);
        [DllImport("winmm.dll", ExactSpelling = true)]
        internal static extern uint joyGetPosEx(uint id, ref Info info);
        [DllImport("winmm.dll", ExactSpelling = true)]
        internal static extern uint joyGetNumDevs();
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

        internal static bool IsGameForeground()
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                using (Process p = Process.GetProcessById((int)pid))
                {
                    return String.Equals(p.ProcessName, "Subnautica2-Win64-Shipping", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }
    }

    // Keep partially typed numbers intact while the live input loop reads Value.
    // NumericUpDown.Value calls ValidateEditText; its usual validation can turn
    // the intermediate "5" of "500" into the control's minimum (such as 10).
    // Only commit/clamp after Enter or when the editor loses focus. The actual
    // gameplay Minimum/Maximum values remain unchanged.
    internal sealed class DeferredNumericUpDown : NumericUpDown
    {
        private bool committing;

        protected override void ValidateEditText()
        {
            if (!committing && UserEdit && ContainsFocus) return;
            base.ValidateEditText();
        }

        internal void CommitPendingEdit()
        {
            if (committing) return;
            committing = true;
            try { base.ValidateEditText(); }
            finally { committing = false; }
        }

        protected override void OnLeave(EventArgs e)
        {
            CommitPendingEdit();
            base.OnLeave(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitPendingEdit();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(e);
        }
    }

    internal sealed class Device
    {
        internal string Identity { get { return Id + ":" + Caps.Mid + ":" + Caps.Pid + ":" + Caps.Name; } }
        internal uint Id;
        internal Native.Caps Caps;
        internal double[] Values = new double[6];
        internal bool Connected;
        internal uint Buttons;
        internal bool Supports(int axis)
        {
            if (axis < 2) return true;
            uint[] flags = { 0, 0, 1, 2, 4, 8 };
            return (Caps.Flags & flags[axis]) != 0;
        }
        internal bool Poll()
        {
            Native.Info info = new Native.Info();
            info.Size = (uint)Marshal.SizeOf(typeof(Native.Info));
            info.Flags = 0xFF;
            Connected = Native.joyGetPosEx(Id, ref info) == 0;
            if (!Connected) { Buttons = 0; return false; }
            Buttons = info.Buttons;
            uint[] raw = { info.X, info.Y, info.Z, info.R, info.U, info.V };
            uint[] min = { Caps.Xmin, Caps.Ymin, Caps.Zmin, Caps.Rmin, Caps.Umin, Caps.Vmin };
            uint[] max = { Caps.Xmax, Caps.Ymax, Caps.Zmax, Caps.Rmax, Caps.Umax, Caps.Vmax };
            for (int i = 0; i < 6; i++)
            {
                double range = (double)max[i] - min[i];
                Values[i] = range > 0 ? Math.Max(-1, Math.Min(1, 2 * ((double)raw[i] - min[i]) / range - 1)) : 0;
            }
            return true;
        }
    }

    internal sealed class AxisChoice
    {
        internal Device Device;
        internal int Axis;
        internal InputSource Xbox;
        private static readonly string[] Names = { "X", "Y", "Z", "R", "U", "V" };
        public override string ToString()
        {
            if (Xbox != null) return InputNames.Describe(Xbox);
            return Device == null ? "Choose an axis, or click Learn" :
                "#" + (Device.Id + 1) + " " + Device.Caps.Name + " / " + Names[Axis];
        }
        internal string Identity { get { return Xbox != null ? Xbox.Identity : Device.Id + ":" + Axis; } }
    }

    internal sealed class ButtonChoice
    {
        internal Device Device;
        internal int Button;
        internal int Key;
        internal InputSource Xbox;
        internal InputSource Source { get { return Xbox != null ? Xbox : Key != 0 ? InputSource.Key(Key) : Device == null ? null : InputSource.Joystick(Device.Identity, Button, false); } }
        public override string ToString()
        {
            return Xbox != null || Key != 0 ? InputNames.Describe(Source) : Device == null ? "No boost button assigned" :
                "#" + (Device.Id + 1) + " " + Device.Caps.Name + " / Button " + (Button + 1);
        }
        internal bool Down
        {
            get
            {
                if (Xbox != null)
                {
                    int player; XInputProbe.State state; string issue;
                    return InputSource.TryXboxPlayer(Xbox.Device, out player) &&
                        XInputProbe.TryRead((uint)player, out state, out issue) && XInputProbe.ButtonDown(state, Xbox.Code);
                }
                return Key != 0 ? (Native.GetAsyncKeyState(Key) & 0x8000) != 0 :
                    Device != null && Device.Connected && (Device.Buttons & (1U << Button)) != 0;
            }
        }
    }

    public sealed partial class ControlsWindow : Form
    {
        private readonly string root;
        private readonly List<Device> devices = new List<Device>();
        private readonly XInputProbe.State[] xboxStates = new XInputProbe.State[4];
        private readonly bool[] xboxConnected = new bool[4];
        private readonly ComboBox[] choices = new ComboBox[6];
        private readonly ComboBox[] inputModes = new ComboBox[6];
        private readonly Button[] pairButtons = new Button[6];
        private readonly AxisBinding[] directionPairs = new AxisBinding[6];
        private readonly DigitalFocusGate digitalFocus = new DigitalFocusGate();
        private bool filterDigital, inputForeground;
        private string mappingLoadError, mappingError;
        private readonly string[] negativeNames = { "Reverse", "Left", "Down", "Nose down", "Nose left", "Roll left" };
        private readonly string[] positiveNames = { "Forward", "Right", "Up", "Nose up", "Nose right", "Roll right" };
        private readonly CheckBox[] invert = new CheckBox[6];
        private readonly Label[] meters = new Label[6];
        private readonly Button[] learnButtons = new Button[6];
        private readonly Dictionary<string, double> baseline = new Dictionary<string, double>();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Label prompt = new Label();
        private readonly Label status = new Label();
        private readonly Button start = new Button();
        // This is deliberately session-only. Do not persist an experimental input takeover.
        private readonly CheckBox xboxFlightTest = new CheckBox();
        private readonly NumericUpDown deadzone = new DeferredNumericUpDown();
        private readonly NumericUpDown[] rotationRates = new NumericUpDown[3];
        private readonly NumericUpDown[] movementRates = new NumericUpDown[3];
        private readonly PictureBox brandMark = new PictureBox();
        private System.Drawing.Icon applicationIcon;
        private decimal hudRight = 420M, hudBottom = 48M;
        private readonly NumericUpDown boostCooldown = new DeferredNumericUpDown();
        private readonly NumericUpDown topSpeed = new NumericUpDown();
        private readonly ComboBox boostChoice = new ComboBox();
        private readonly Label boostStatus = new Label();
        private readonly Stopwatch statusPoll = Stopwatch.StartNew();
        private DateTime lastBoostStatusUtc = DateTime.MinValue;
        private SoundPlayer boostSound;
        private string boostSoundIssue;
        // Monotonic local cooldown for both accepting a boost-button edge and playing its WAV.
        // The game retains its own safety/cooldown check; audio never invokes game FMOD APIs.
        private readonly Stopwatch localBoostCooldown = new Stopwatch();
        private double cooldownAtLastPressSeconds = 10.0;
        private long pendingAudioToken;
        private bool lastBoostDown;
        private long boostToken;
        private readonly string[] labels = { "Forward / reverse", "Strafe right / left", "Thrust up / down", "Pitch up / down", "Yaw right / left", "Roll right / left" };
        private readonly string[] instructions = {
            "Move the control you want for FORWARD, then release it.",
            "Move the control you want for STRAFE RIGHT, then release it.",
            "Move the control you want for THRUST UP, then release it.",
            "Move the control you want for NOSE UP, then release it.",
            "Move the control you want for NOSE RIGHT, then release it.",
            "Move the control you want for ROLL RIGHT, then release it." };
        private int learning = -1;
        private bool running;
        private long sequence = DateTime.UtcNow.Ticks;
        private string lastIoError;
        private int ioFailures;

        public ControlsWindow(string modRoot)
        {
            root = modRoot;
            Text = "TurboTad - 0.3.15B LOCAL BOOST AUDIO";
            string soundPath = Path.Combine(root, "Assets", "TurboTadBoost.wav");
            if (File.Exists(soundPath))
            {
                try
                {
                    boostSound = new SoundPlayer(soundPath);
                    boostSound.Load();
                }
                catch (Exception e)
                {
                    // Audio is optional; never stop or delay flight controls.
                    boostSoundIssue = e.Message;
                    if (boostSound != null) { boostSound.Dispose(); boostSound = null; }
                }
            }
            else boostSoundIssue = "TurboTadBoost.wav is missing from the mod's Assets folder.";
            BuildBetaflightInterface();
            Scan();
            LoadMapping();
            timer.Interval = 16;
            timer.Tick += delegate { TickControls(); };
            timer.Start();
            FormClosing += delegate {
                timer.Stop();
                running = false;
                Publish(0, new double[6]);
                SaveMapping();
            };
            FormClosed += delegate {
                if (boostSound != null) boostSound.Dispose();
                if (brandMark.Image != null) { brandMark.Image.Dispose(); brandMark.Image = null; }
                Icon = null;
                if (applicationIcon != null) { applicationIcon.Dispose(); applicationIcon = null; }
            };
        }

        // Shared palette; UIM extends mapping.xml while preserving T6D7 and multiplier units.
        private static readonly Color Canvas = Color.FromArgb(24, 26, 29);
        private static readonly Color Surface = Color.FromArgb(37, 40, 44);
        private static readonly Color SurfaceLight = Color.FromArgb(49, 52, 57);
        private static readonly Color Orange = Color.FromArgb(255, 143, 40);
        private static readonly Color Muted = Color.FromArgb(178, 184, 191);
        private TabControl dashboardTabs;
        private NumericUpDown mphInput;
        private bool updatingMph;
        private const decimal BaseMphAtHundred = 22.36936M; // Observed 1000 cm/s swimming cap; estimated, not a live telemetry reading.

        private void BuildBetaflightInterface()
        {
            Text = "TurboTad | Universal Input Mapper - TEST 1A";
            ClientSize = new Size(1120, 800);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5F);
            BackColor = Canvas;
            ForeColor = Color.Gainsboro;

            // Keep existing application icon and use the approved logo for the large header.
            string iconPath = Path.Combine(root, "Assets", "TurboTad.ico");
            if (File.Exists(iconPath))
            {
                applicationIcon = new System.Drawing.Icon(iconPath, new Size(32, 32));
                using (System.Drawing.Icon headerIcon = new System.Drawing.Icon(iconPath, new Size(64, 64)))
                    brandMark.Image = headerIcon.ToBitmap();
            }
            else
            {
                applicationIcon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                if (applicationIcon != null) brandMark.Image = applicationIcon.ToBitmap();
            }
            string logoPath = Path.Combine(root, "Assets", "TurboTadLogo.png");
            if (File.Exists(logoPath))
            {
                try
                {
                    using (MemoryStream memory = new MemoryStream(File.ReadAllBytes(logoPath)))
                    using (Image original = Image.FromStream(memory))
                    {
                        if (brandMark.Image != null) brandMark.Image.Dispose();
                        brandMark.Image = new Bitmap(original);
                    }
                }
                catch { /* The fallback .ico remains valid. */ }
            }
            Icon = applicationIcon;
            ShowIcon = true;
            ShowInTaskbar = true;

            Panel accent = new Panel { BackColor = Orange };
            accent.SetBounds(0, 0, ClientSize.Width, 5);
            Controls.Add(accent);
            brandMark.SetBounds(20, 12, 110, 65);
            brandMark.SizeMode = PictureBoxSizeMode.Zoom;
            brandMark.BackColor = Surface;
            Controls.Add(brandMark);
            Label heading = UiLabel(this, "TURBOTAD", 147, 11, 490, 42, 23F, Color.White, true);
            UiLabel(this, "FLIGHT CONFIGURATOR  /  STABLE BRANCH", 150, 50, 600, 24, 9F, Muted);
            UiLabel(this, "6DOF  •  ANALOG CONTROL  •  DIRECTIONAL BOOST", 710, 27, 384, 30, 9.5F, Orange, true).TextAlign = ContentAlignment.MiddleRight;

            dashboardTabs = new TabControl();
            dashboardTabs.SetBounds(18, 87, 1084, 610);
            dashboardTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            dashboardTabs.SizeMode = TabSizeMode.Fixed;
            dashboardTabs.ItemSize = new Size(225, 38);
            dashboardTabs.BackColor = Canvas;
            dashboardTabs.ForeColor = Color.White;
            dashboardTabs.DrawItem += delegate(object sender, DrawItemEventArgs e) {
                Rectangle r = e.Bounds;
                bool selected = e.Index == dashboardTabs.SelectedIndex;
                using (Brush fill = new SolidBrush(selected ? SurfaceLight : Canvas)) e.Graphics.FillRectangle(fill, r);
                using (Font tabFont = new Font("Segoe UI", 9F, selected ? FontStyle.Bold : FontStyle.Regular))
                    TextRenderer.DrawText(e.Graphics, dashboardTabs.TabPages[e.Index].Text,
                        tabFont, r, selected ? Orange : Muted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (selected)
                    using (Brush stripe = new SolidBrush(Orange)) e.Graphics.FillRectangle(stripe, r.Left + 8, r.Bottom - 4, r.Width - 16, 3);
            };
            Controls.Add(dashboardTabs);
            TabPage tune = MakeTab("FLIGHT TUNING");
            TabPage input = MakeTab("INPUT MAPPING");
            TabPage boostPage = MakeTab("BOOST HUD");
            dashboardTabs.TabPages.Add(tune);
            dashboardTabs.TabPages.Add(input);
            dashboardTabs.TabPages.Add(boostPage);

            UiLabel(tune, "LIVE TUNING", 20, 12, 225, 26, 12F, Orange, true);

            Panel rotations = Section(tune, 20, 53, 505, 342, "ANGULAR RESPONSE", "Engine-relative rotation gain (100% = original).");
            Panel movements = Section(tune, 538, 53, 505, 342, "TRANSLATION GAIN", "Proportional movement sensitivity (100% = original).");
            string[] rateNames = { "Pitch", "Yaw", "Roll" };
            string[] movementNames = { "Forward / reverse", "Strafe left / right", "Vertical up / down" };
            for (int i = 0; i < 3; ++i)
            {
                rotationRates[i] = MakeNumber(rotations, 392, 82 + i * 82, 90, 28, 10M, 1000M, 100M, 10M);
                RateSlider slider = new RateSlider(10, 1000, 1, 100);
                slider.SetBounds(19, 112 + i * 82, 461, 30);
                rotations.Controls.Add(slider);
                UiLabel(rotations, rateNames[i] + " rate (%)", 20, 82 + i * 82, 365, 27, 10F, Color.WhiteSmoke);
                BindSlider(slider, rotationRates[i]);

                movementRates[i] = MakeNumber(movements, 392, 82 + i * 82, 90, 28, 10M, 1000M, 100M, 10M);
                RateSlider movement = new RateSlider(10, 1000, 1, 100);
                movement.SetBounds(19, 112 + i * 82, 461, 30);
                movements.Controls.Add(movement);
                UiLabel(movements, movementNames[i] + " gain (%)", 20, 82 + i * 82, 366, 27, 10F, Color.WhiteSmoke);
                BindSlider(movement, movementRates[i]);
            }

            // Keep the top-speed card within the flight tab at Windows text/DPI scales.
            // Only presentation coordinates changed; slider/editor binding and units remain intact.
            Panel speedPanel = Section(tune, 20, 402, 1023, 132,
                "NORMAL TOP SPEED", "", 810);
            topSpeed.Minimum = 25;
            topSpeed.Maximum = 400;
            topSpeed.Increment = 1;
            topSpeed.Value = 100;
            mphInput = MakeNumber(speedPanel, 871, 65, 103, 32,
                Math.Round(BaseMphAtHundred * .25M, 1), Math.Round(BaseMphAtHundred * 4M, 1),
                Math.Round(BaseMphAtHundred, 1), .2M);
            mphInput.DecimalPlaces = 1;
            mphInput.BringToFront(); // Keep the editable speed value above any surrounding presentation labels.
            UiLabel(speedPanel, "est. mph", 882, 99, 95, 22, 9F, Muted);
            RateSlider speedSlider = new RateSlider(25, 400, 1, 100);
            speedSlider.SetBounds(19, 66, 827, 36);
            speedPanel.Controls.Add(speedSlider);
            BindSlider(speedSlider, topSpeed);
            topSpeed.ValueChanged += delegate { SyncMphFromPercent(); };
            mphInput.ValueChanged += delegate { SyncPercentFromMph(); };
            SyncMphFromPercent();

            UiLabel(input, "INPUT ASSIGNMENTS", 20, 10, 330, 29, 12F, Orange, true);
            UiLabel(input, "Choose Analog or Buttons for each axis. Learn an axis, or assign its two directions.", 20, 38, 1020, 26, 9.5F, Muted);
            UiLabel(input, "AXIS", 24, 71, 190, 24, 9F, Muted, true);
            UiLabel(input, "TYPE / INPUT SOURCE", 227, 71, 448, 24, 9F, Muted, true);
            UiLabel(input, "INVERT", 758, 71, 70, 24, 9F, Muted, true);
            UiLabel(input, "LIVE", 956, 71, 70, 24, 9F, Muted, true);
            for (int i = 0; i < 6; i++)
            {
                int row = i;
                int y = 103 + i * 58;
                Panel stripe = new Panel { BackColor = (i % 2 == 0 ? Surface : Canvas) };
                stripe.SetBounds(14, y - 7, 1028, 51);
                input.Controls.Add(stripe);
                UiLabel(stripe, labels[i], 12, 10, 190, 27, 9.5F, Color.WhiteSmoke);
                choices[i] = new ComboBox();
                choices[i].SetBounds(310, 7, 415, 31);
                inputModes[i] = new ComboBox(); inputModes[i].SetBounds(209, 7, 94, 31);
                inputModes[i].DropDownStyle = ComboBoxStyle.DropDownList; StyleCombo(inputModes[i]);
                inputModes[i].Items.AddRange(new object[] { "Analog", "Buttons" }); inputModes[i].SelectedIndex = 0;
                stripe.Controls.Add(inputModes[i]); directionPairs[i] = new AxisBinding { Digital = true };
                pairButtons[i] = ThemeButton(stripe, "ASSIGN DIRECTIONS", 310, 7, 415, 32, false);
                pairButtons[i].Visible = false;
                pairButtons[i].Click += delegate { EditDirectionPair(row); };
                inputModes[i].SelectedIndexChanged += delegate { StopControls(); RefreshInputRow(row); };
                choices[i].DropDownStyle = ComboBoxStyle.DropDownList;
                StyleCombo(choices[i]);
                stripe.Controls.Add(choices[i]);
                invert[i] = new CheckBox { BackColor = Color.Transparent, ForeColor = Orange };
                invert[i].SetBounds(749, 12, 28, 24);
                stripe.Controls.Add(invert[i]);
                learnButtons[i] = ThemeButton(stripe, "LEARN", 798, 7, 110, 32, false);
                learnButtons[i].Click += delegate { BeginLearn(row); };
                meters[i] = UiLabel(stripe, "--", 930, 12, 84, 27, 9.5F, Orange, true);
                choices[i].SelectedIndexChanged += delegate { StopControls(); };
                invert[i].CheckedChanged += delegate { StopControls(); };
            }
            deadzone.Minimum = 0;
            deadzone.Maximum = .25M;
            deadzone.DecimalPlaces = 2;
            deadzone.Increment = .01M;
            deadzone.Value = .04M;
            deadzone.SetBounds(137, 486, 86, 31);
            StyleNumber(deadzone);
            input.Controls.Add(deadzone);
            UiLabel(input, "Deadzone", 22, 490, 112, 28, 9.5F, Color.WhiteSmoke);
            Button rescan = ThemeButton(input, "RESCAN DEVICES", 248, 483, 173, 35, false);
            rescan.Click += delegate { SaveMapping(); Scan(); LoadMapping(); };
            Button saveProfile = ThemeButton(input, "SAVE PROFILE", 435, 483, 157, 35, false);
            saveProfile.Click += delegate { ExportInputProfile(); };
            Button loadProfile = ThemeButton(input, "LOAD PROFILE", 606, 483, 157, 35, false);
            loadProfile.Click += delegate { ImportInputProfile(); };
            UiLabel(input, "Profiles store input bindings only.", 435, 525, 545, 24, 9F, Muted);
            // Temporary safety gate: changing this setting immediately stops active controls.
            // This flag must NOT be saved or restored from mapping.xml or a user profile.
            xboxFlightTest.SetBounds(22, 526, 407, 29);
            xboxFlightTest.Text = "ENABLE XBOX FLIGHT TEST (RESETS OFF)";
            xboxFlightTest.ForeColor = Orange;
            xboxFlightTest.BackColor = Color.Transparent;
            xboxFlightTest.CheckedChanged += delegate { StopControls(); };
            input.Controls.Add(xboxFlightTest);
            // Deliberately read-only: XInput detection must not change mappings or game controls.
            Button testXboxInput = ThemeButton(input, "TEST XBOX INPUT", 782, 483, 255, 35, false);
            testXboxInput.Click += delegate { using (XInputDiagnosticDialog dialog = new XInputDiagnosticDialog()) dialog.ShowDialog(this); };

            UiLabel(boostPage, "BOOST & SOUND", 20, 10, 420, 30, 12F, Orange, true);
            Panel boostCard = Section(boostPage, 20, 70, 671, 281, "DIRECTIONAL BOOST", "");
            UiLabel(boostCard, "Boost button / key", 22, 72, 150, 26, 10F, Color.WhiteSmoke);
            boostChoice.SetBounds(20, 106, 410, 32);
            boostChoice.DropDownStyle = ComboBoxStyle.DropDownList;
            StyleCombo(boostChoice);
            boostChoice.SelectedIndexChanged += delegate { StopControls(); };
            boostCard.Controls.Add(boostChoice);
            Button learnBoost = ThemeButton(boostCard, "LEARN", 443, 105, 94, 34, false);
            learnBoost.Click += delegate { BeginLearnBoost(); };
            Button clearBoost = ThemeButton(boostCard, "CLEAR", 548, 105, 100, 34, false);
            clearBoost.Click += delegate {  boostChoice.SelectedIndex = 0; };
            UiLabel(boostCard, "Cooldown (seconds)", 22, 173, 174, 30, 10F, Color.WhiteSmoke);
            boostCooldown.SetBounds(211, 171, 96, 32);
            boostCooldown.Minimum = 2;
            boostCooldown.Maximum = 60;
            boostCooldown.Value = 10;
            StyleNumber(boostCooldown);
            boostCard.Controls.Add(boostCooldown);
            boostStatus.SetBounds(25, 221, 620, 32);
            boostStatus.ForeColor = Orange;
            boostStatus.Text = "Boost: waiting for game" + (boostSound != null ? " | WAV ready" : " | WAV missing");
            boostCard.Controls.Add(boostStatus);

            Panel soundCard = Section(boostPage, 713, 70, 330, 281, "CUSTOM WAV AUDIO", "Browse, drag and drop, preview and save.");
            Button editAudio = ThemeButton(soundCard, "OPEN BOOST AUDIO SETTINGS", 18, 98, 293, 48, true);
            editAudio.Click += delegate {
                // Dispose the file lock before the existing WAV picker saves the sound.
                if (boostSound != null) { boostSound.Stop(); boostSound.Dispose(); boostSound = null; }
                try { using (BoostAudioPicker dialog = new BoostAudioPicker(root)) dialog.ShowDialog(this); }
                finally
                {
                    boostSoundIssue = null;
                    try
                    {
                        string file = Path.Combine(root, "Assets", "TurboTadBoost.wav");
                        boostSound = new SoundPlayer(file);
                        boostSound.Load();
                    }
                    catch (Exception e)
                    {
                        boostSoundIssue = e.Message;
                        if (boostSound != null) { boostSound.Dispose(); boostSound = null; }
                    }
                }
            };

            Panel hudCard = Section(boostPage, 20, 371, 1023, 184, "HUD & DISPLAY", "");
            Button hudPosition = ThemeButton(hudCard, "ADJUST HUD POSITION", 21, 76, 241, 43, false);
            hudPosition.Click += delegate { EditHudPosition(); };
            UiLabel(hudCard, "Boost bar placement", 286, 82, 689, 35, 9.5F, Muted);

            Panel footer = new Panel { BackColor = Surface };
            footer.SetBounds(0, 710, 1120, 90);
            Controls.Add(footer);
            Panel footerLine = new Panel { BackColor = Orange };
            footerLine.SetBounds(0, 0, 1120, 2);
            footer.Controls.Add(footerLine);
            // Keep the full joystick prompt within the footer, clear of the ABOUT button.
            // Long device and error messages are ellipsized instead of painting into buttons.
            prompt.SetBounds(20, 9, 780, 27);
            prompt.AutoEllipsis = true;
            prompt.ForeColor = Orange;
            footer.Controls.Add(prompt);
            status.SetBounds(20, 44, 790, 39);
            status.ForeColor = Muted;
            footer.Controls.Add(status);
            Button about = ThemeButton(footer, "ABOUT", 824, 17, 95, 48, false);
            about.Click += delegate { MessageBox.Show(this,
                "TurboTad 0.3.15B  •  Flight Configurator GUI\r\n\r\nSix-axis analog Tadpole controls with linked flight and boost speed.\r\n\r\n" +
                "The rotation settings are engine-input multipliers, not calibrated degrees/second. " +
                "Estimated mph assumes a native 1000 cm/s swim cap observed on the tested build.\r\n\r\n" +
                "Runs locally. No network telemetry, administrator rights or controller-driver changes.\r\n\r\n" +
                "WARNING: Breaching the water surface at high speed can launch the Tadpole out of the map. Back up saves and slow down before surfacing.",
                "About TurboTad", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            start.Text = "START CONTROLS";
            start.SetBounds(932, 16, 168, 49);
            StyleButton(start, true);
            start.Click += delegate { if (running) StopControls(); else StartControls(); };
            footer.Controls.Add(start);
        }

        private TabPage MakeTab(string title)
        {
            TabPage page = new TabPage(title);
            page.UseVisualStyleBackColor = false;
            page.BackColor = Canvas;
            page.ForeColor = Color.WhiteSmoke;
            return page;
        }

        private Panel Section(Control parent, int x, int y, int width, int height, string heading, string description, int descriptionWidth = -1)
        {
            Panel card = new Panel { BackColor = Surface };
            card.SetBounds(x, y, width, height);
            parent.Controls.Add(card);
            UiLabel(card, heading, 18, 14, width - 35, 28, 11.5F, Orange, true);
            if (!String.IsNullOrWhiteSpace(description))
                UiLabel(card, description, 18, 43, descriptionWidth > 0 ? descriptionWidth : width - 35, 38, 8.6F, Muted);
            return card;
        }

        private Label UiLabel(Control parent, string content, int x, int y, int width, int height, float fontSize, Color color, bool bold)
        {
            Label l = new Label();
            l.Text = content;
            l.Font = new Font("Segoe UI", fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.SetBounds(x, y, width, height);
            parent.Controls.Add(l);
            return l;
        }

        private Label UiLabel(Control parent, string content, int x, int y, int width, int height, float fontSize, Color color)
        {
            return UiLabel(parent, content, x, y, width, height, fontSize, color, false);
        }

        private void StyleNumber(NumericUpDown number)
        {
            number.BackColor = SurfaceLight;
            number.ForeColor = Color.WhiteSmoke;
            number.BorderStyle = BorderStyle.FixedSingle;
            number.TextAlign = HorizontalAlignment.Right;
            number.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        }

        private NumericUpDown MakeNumber(Control parent, int x, int y, int width, int height,
            decimal minimum, decimal maximum, decimal value, decimal increment)
        {
            NumericUpDown n = new DeferredNumericUpDown();
            n.Minimum = minimum;
            n.Maximum = maximum;
            n.Increment = increment;
            n.Value = value;
            n.SetBounds(x, y, width, height);
            StyleNumber(n);
            parent.Controls.Add(n);
            return n;
        }

        private void StyleCombo(ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = SurfaceLight;
            combo.ForeColor = Color.WhiteSmoke;
            combo.Font = new Font("Segoe UI", 9.5F);
        }

        private Button ThemeButton(Control parent, string title, int x, int y, int width, int height, bool primary)
        {
            Button button = new Button();
            button.Text = title;
            button.SetBounds(x, y, width, height);
            StyleButton(button, primary);
            parent.Controls.Add(button);
            return button;
        }

        private void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Orange;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(255, 166, 73) : SurfaceLight;
            button.BackColor = primary ? Orange : SurfaceLight;
            button.ForeColor = primary ? Canvas : Color.WhiteSmoke;
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            button.UseVisualStyleBackColor = false;
        }

        private void BindSlider(RateSlider slider, NumericUpDown numeric)
        {
            slider.ValueChanged += delegate { if (numeric.Value != (decimal)slider.Value) numeric.Value = slider.Value; };
            numeric.ValueChanged += delegate { if (slider.Value != (int)numeric.Value) slider.Value = (int)numeric.Value; };
            slider.Value = (int)numeric.Value;
        }

        private void SyncMphFromPercent()
        {
            if (updatingMph || mphInput == null) return;
            updatingMph = true;
            try
            {
                decimal mph = Math.Round(BaseMphAtHundred * topSpeed.Value / 100M, 1);
                mphInput.Value = Math.Max(mphInput.Minimum, Math.Min(mphInput.Maximum, mph));
            }
            finally { updatingMph = false; }
        }

        private void SyncPercentFromMph()
        {
            if (updatingMph || mphInput == null) return;
            updatingMph = true;
            try
            {
                decimal raw = Math.Round(mphInput.Value * 100M / BaseMphAtHundred, 0, MidpointRounding.AwayFromZero);
                topSpeed.Value = Math.Max(topSpeed.Minimum, Math.Min(topSpeed.Maximum, raw));
            }
            finally { updatingMph = false; }
        }

        private Label AddLabel(string text, int x, int y, int w, int h)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(x, y, w, h);
            Controls.Add(label);
            return label;
        }

        private void PollXbox()
        {
            for (uint player = 0; player < 4; player++)
            {
                string issue;
                XInputProbe.State state;
                xboxConnected[(int)player] = XInputProbe.TryRead(player, out state, out issue);
                xboxStates[(int)player] = state;
            }
        }

        private bool TryXboxState(string device, out XInputProbe.State state)
        {
            state = new XInputProbe.State();
            int player;
            if (!InputSource.TryXboxPlayer(device, out player) || !xboxConnected[player]) return false;
            state = xboxStates[player];
            return true;
        }

        private bool HasXboxMapping()
        {
            foreach (AxisBinding binding in CurrentBindings())
                foreach (InputSource source in binding.Sources)
                    if (source.Kind == "xaxis" || source.Kind == "xbutton") return true;
            ButtonChoice boost = boostChoice.SelectedItem as ButtonChoice;
            return boost != null && boost.Source != null && boost.Source.Kind == "xbutton";
        }

        private void Scan()
        {
            StopControls();
            learning = -1;
            
            devices.Clear();
            uint max = Math.Min(Native.joyGetNumDevs(), 16U);
            for (uint id = 0; id < max; id++)
            {
                Native.Caps caps;
                if (Native.joyGetDevCapsW(new UIntPtr(id), out caps, (uint)Marshal.SizeOf(typeof(Native.Caps))) != 0) continue;
                Device d = new Device(); d.Id = id; d.Caps = caps;
                if (d.Poll()) devices.Add(d);
            }
            PollXbox();
            for (int i = 0; i < 6; i++)
            {
                choices[i].Items.Clear();
                choices[i].Items.Add(new AxisChoice());
                foreach (Device d in devices)
                    for (int axis = 0; axis < 6; axis++)
                        if (d.Supports(axis)) choices[i].Items.Add(new AxisChoice { Device = d, Axis = axis });
                for (int player = 0; player < xboxConnected.Length; player++)
                    if (xboxConnected[player])
                        for (int axis = 0; axis < 4; axis++)
                            choices[i].Items.Add(new AxisChoice { Xbox = InputSource.XboxAxis(player, axis) });
                choices[i].SelectedIndex = 0;
            }
            boostChoice.Items.Clear();
            boostChoice.Items.Add(new ButtonChoice());
            foreach (Device d in devices)
                for (int b = 0; b < Math.Min((int)d.Caps.Buttons, 32); b++)
                    boostChoice.Items.Add(new ButtonChoice { Device = d, Button = b });
            int controllers = 0;
            for (int player = 0; player < xboxConnected.Length; player++)
                if (xboxConnected[player])
                {
                    controllers++;
                    for (int b = 0; b < XInputProbe.ButtonBits.Length; b++)
                        boostChoice.Items.Add(new ButtonChoice { Xbox = InputSource.XboxButton(player, b) });
                }
            boostChoice.SelectedIndex = 0;
            prompt.Text = devices.Count + " joystick(s), " + controllers +
                " Xbox controller(s) detected. Xbox bindings are preview-only for now.";
        }

        // XInput can become connected after the window's initial Scan() (for example,
        // when a wireless controller wakes up). The diagnostic already polls live,
        // but Learn must also discover those axes without requiring a manual Rescan.
        private void EnsureXboxLearnCandidates(int row)
        {
            for (int player = 0; player < xboxConnected.Length; player++)
            {
                if (!xboxConnected[player]) continue;
                for (int axis = 0; axis < 4; axis++)
                {
                    InputSource source = InputSource.XboxAxis(player, axis);
                    bool present = false;
                    foreach (AxisChoice existing in choices[row].Items)
                        if (existing.Xbox != null && existing.Xbox.Identity == source.Identity)
                        { present = true; break; }
                    if (present) continue;
                    AxisChoice candidate = new AxisChoice { Xbox = source };
                    choices[row].Items.Add(candidate);
                    // A late-waking stick is centered at zero; capture its first
                    // real movement rather than baselining the already-moved value.
                    if (!baseline.ContainsKey(candidate.Identity)) baseline[candidate.Identity] = 0;
                }
            }
        }

        private void BeginLearn(int row)
        {
            StopControls();
            
            if (inputModes[row].SelectedIndex == 1) { EditDirectionPair(row); return; }
            baseline.Clear();
            PollXbox();
            EnsureXboxLearnCandidates(row);
            foreach (Device d in devices) d.Poll();
            foreach (AxisChoice choice in choices[row].Items)
            {
                if (choice.Xbox != null)
                {
                    XInputProbe.State state;
                    if (TryXboxState(choice.Xbox.Device, out state))
                        baseline[choice.Identity] = XInputProbe.AxisValue(state, choice.Xbox.Code);
                }
                else if (choice.Device != null && choice.Device.Connected && choice.Device.Supports(choice.Axis))
                    baseline[choice.Identity] = choice.Device.Values[choice.Axis];
            }
            learning = row;
            prompt.Text = instructions[row];
        }

        private void BeginLearnBoost()
        {
            StopControls(); learning = -1; 
            using (InputCaptureDialog dialog = new InputCaptureDialog(devices))
                if (dialog.ShowDialog(this) == DialogResult.OK) SelectBoostSource(dialog.Selected);
        }

        private void StopControls()
        {
            running = false;
            digitalFocus.Reset();
            boostToken = 0;
            pendingAudioToken = 0;
            start.Text = "START CONTROLS";
            Publish(0, new double[6]);
        }

        private bool TryGetAxes(out double[] values)
        {
            values = new double[6];
            AxisBinding[] bindings = CurrentBindings();
            if (mappingLoadError != null) { mappingError = mappingLoadError; return false; }
            for (int i = 0; i < 6; i++)
                if (!InputMapper.TryEvaluate(bindings[i], new HelperInputFrame(this), (double)deadzone.Value,
                    out values[i], out mappingError)) return false;
            ButtonChoice boost = boostChoice.SelectedItem as ButtonChoice;
            return InputMapper.ValidateSources(bindings, boost == null ? null : boost.Source, out mappingError);
        }

        private void StartControls()
        {
            // The in-game controller already suppresses stock Tadpole look/rotation and
            // consumes pending stock movement. Test whether that covers Xbox input too.
            // Opt-in is temporary and explicitly reset on every helper launch.
            if (HasXboxMapping() && !xboxFlightTest.Checked)
            {
                prompt.Text = "Xbox bindings are preview-only. Enable XBOX FLIGHT TEST to try in-game.";
                return;
            }
            double[] values;
            if (!TryGetAxes(out values))
            {
                prompt.Text = "Cannot start: " + mappingError + ".";
                return;
            }
            if (!InputMapper.AreDigitalSourcesReleased(CurrentBindings(), new HelperInputFrame(this)))
            { prompt.Text = "Release all direction buttons before starting."; return; }
            for (int i = 0; i < 6; i++)
                if (Math.Abs(values[i]) > 0.15)
                {
                    prompt.Text = "Center analog controls and release direction buttons before starting.";
                    return;
                }
            ButtonChoice assignedBoost = boostChoice.SelectedItem as ButtonChoice;
            if (assignedBoost != null && assignedBoost.Down) { prompt.Text = "Release boost before starting."; return; }
            digitalFocus.Reset();
            SaveMapping();
            learning = -1;
            
            running = true;
            start.Text = "STOP CONTROLS";
            prompt.Text = "Ready. Return to Subnautica 2 and pilot a powered Tadpole. Closing this window stops the controls.";
        }

        private void TickControls()
        {
            foreach (Device d in devices) d.Poll();
            PollXbox();
            if (learning >= 0)
            {
                // Include controllers that connected or woke while Learn was open.
                EnsureXboxLearnCandidates(learning);
                AxisChoice winner = null;
                double best = 0.55;
                foreach (AxisChoice a in choices[learning].Items)
                {
                    double current;
                    if (a.Xbox != null)
                    {
                        XInputProbe.State state;
                        if (!TryXboxState(a.Xbox.Device, out state)) continue;
                        current = XInputProbe.AxisValue(state, a.Xbox.Code);
                    }
                    else
                    {
                        if (a.Device == null || !a.Device.Connected) continue;
                        current = a.Device.Values[a.Axis];
                    }
                    double center;
                    if (!baseline.TryGetValue(a.Identity, out center)) continue;
                    double travel = Math.Abs(current - center);
                    if (travel > best) { best = travel; winner = a; }
                }
                if (winner != null)
                {
                    int row = learning;
                    learning = -1;
                    choices[row].SelectedItem = winner;
                    // Learning identifies an axis only. Inversion is explicitly user-controlled.
                    prompt.Text = labels[row] + " assigned. Release the control, then Learn the next row.";
                }
            }
            bool foreground = Native.IsGameForeground();
            inputForeground = foreground; filterDigital = true;
            double[] values;
            bool mapped = TryGetAxes(out values);
            filterDigital = false;
            // Per-row preview permits Xbox Learn testing before all six axes are assigned.
            AxisBinding[] preview = CurrentBindings();
            HelperInputFrame previewFrame = new HelperInputFrame(this);
            for (int i = 0; i < 6; i++)
            {
                double value; string error;
                bool valid = InputMapper.TryEvaluate(preview[i], previewFrame, (double)deadzone.Value, out value, out error);
                meters[i].Text = valid ? value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) : "--";
            }
            if (running && !mapped)
            {
                StopControls();
                prompt.Text = "Controls stopped: a controller disconnected or a mapping is invalid. Reconnect and start again.";
            }
            bool active = running && mapped && foreground;
            int mode = running && mapped ? (foreground ? 1 : 2) : 0;
            ButtonChoice boostButton = boostChoice.SelectedItem as ButtonChoice;
            bool boostDown = boostButton != null && boostButton.Source != null &&
                digitalFocus.Filter(boostButton.Source.Identity, boostButton.Down, active);
            // Exactly one activation per press, gated by the same user-configured cooldown
            // sent to the game in Publish(). Holding the button or pressing during cooldown
            // cannot send another token or replay the sound.
            bool timerReady = !localBoostCooldown.IsRunning ||
                localBoostCooldown.Elapsed.TotalSeconds >= cooldownAtLastPressSeconds;
            if (active && boostDown && !lastBoostDown && timerReady)
            {
                boostToken = sequence + 1;
                pendingAudioToken = boostToken;
            }
            if (!active) { boostToken = 0; pendingAudioToken = 0; }
            lastBoostDown = boostDown;
            Publish(mode, active ? values : new double[6]);
            // Start the local cooldown and play only after the matching input frame was
            // successfully written. A failed file write must not produce a fake sound.
            if (active && pendingAudioToken != 0 && lastIoError == null)
            {
                pendingAudioToken = 0;
                cooldownAtLastPressSeconds = (double)boostCooldown.Value;
                localBoostCooldown.Restart();
                if (boostSound != null)
                {
                    try { boostSound.Play(); }
                    catch (Exception e) { boostSoundIssue = e.Message; }
                }
            }
            UpdateBoostStatus();
            status.Text = lastIoError != null ? "INPUT FILE ERROR: " + lastIoError :
                active ? "SENDING FLIGHT INPUT - Alt-Tab pauses input; Stop restores stock auto-leveling." :
                running ? "INPUT PAUSED - waiting for game focus; free-flight auto-leveling stays disabled." :
                "STOPPED - assign all six axes and click Start controls.";
        }

        private void Publish(int mode, double[] values)
        {
            string target = Path.Combine(root, "input.txt");
            string temp = Path.Combine(root, "input.tmp");
            try
            {
                long stamp = (DateTime.UtcNow.Ticks - 621355968000000000L) / 10000000L;
                StringBuilder line = new StringBuilder("T6D7 ");
                line.Append((++sequence).ToString(CultureInfo.InvariantCulture)).Append(' ');
                line.Append(stamp.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(mode.ToString(CultureInfo.InvariantCulture));
                for (int i = 0; i < 6; i++) line.Append(' ').Append(values[i].ToString("0.000000", CultureInfo.InvariantCulture));
                for (int i = 0; i < 3; i++)
                    line.Append(' ').Append((rotationRates[i].Value / 100M).ToString("0.00", CultureInfo.InvariantCulture));
                line.Append(' ').Append((mode == 1 ? boostToken : 0).ToString(CultureInfo.InvariantCulture));
                line.Append(' ').Append(boostCooldown.Value.ToString(CultureInfo.InvariantCulture));
                for (int i = 0; i < 3; i++)
                    line.Append(' ').Append((movementRates[i].Value / 100M).ToString("0.00", CultureInfo.InvariantCulture));
                line.Append(' ').Append(hudRight.ToString(CultureInfo.InvariantCulture));
                line.Append(' ').Append(hudBottom.ToString(CultureInfo.InvariantCulture));
                line.Append(' ').Append((topSpeed.Value / 100M).ToString("0.00", CultureInfo.InvariantCulture));
                line.Append('\n');
                File.WriteAllText(temp, line.ToString(), new UTF8Encoding(false));
                if (File.Exists(target)) File.Replace(temp, target, null);
                else File.Move(temp, target);
                lastIoError = null;
                ioFailures = 0;
            }
            catch (Exception e)
            {
                lastIoError = e.Message;
                ioFailures++;
                if (ioFailures >= 15) { running = false; start.Text = "START CONTROLS"; }
                // Never fall back to a partial, non-atomic overwrite.
            }
        }

        private void UpdateBoostStatus()
        {
            if (statusPoll.ElapsedMilliseconds < 65) return;
            statusPoll.Restart();
            DateTime clock = DateTime.UtcNow;
            if ((clock - lastBoostStatusUtc).TotalSeconds > 2)
                boostStatus.Text = "Boost: waiting for the game";
            try
            {
                string statePath = Path.Combine(root, "boost-status.txt");
                if (new FileInfo(statePath).Length > 1024) return;
                string[] words = File.ReadAllText(statePath).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                long stamp; int remaining, code;
                long confirmedToken = 0;
                bool audioStatus = words.Length == 5 && words[0] == "T6S2";
                bool legacyStatus = words.Length == 4 && words[0] == "T6S1";
                long now = (DateTime.UtcNow.Ticks - 621355968000000000L) / 10000000L;
                if ((!audioStatus && !legacyStatus) || !long.TryParse(words[1], out stamp) ||
                    !int.TryParse(words[2], out remaining) || !int.TryParse(words[3], out code) ||
                    Math.Abs(now - stamp) > 2 || remaining < 0 || remaining > 60 || code < 0 || code > 4 ||
                    (audioStatus && (!long.TryParse(words[4], out confirmedToken) || confirmedToken < 0))) return;
                lastBoostStatusUtc = clock;
                if (code == 4) StopControls();
                boostStatus.Text = code == 4 ? "Mod stopped - restart the game" :
                    code == 2 ? "Boost unavailable" :
                    remaining > 0 ? "Boost recharging: " + remaining + " s" :
                    code == 3 ? "Boost: pilot a powered Tadpole" : "Boost: READY";
                boostStatus.Text += boostSound != null ? " | WAV ready" : " | WAV missing";
            }
            catch { }
        }

        private void EditHudPosition()
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "TurboTad - Boost HUD position";
                dialog.ClientSize = new Size(450, 210);
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false; dialog.MinimizeBox = false;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Font = Font;
                Label help = new Label(); help.SetBounds(18, 12, 414, 46);
                help.Text = "Move the five-bar gauge. Smaller values move it right or down. Return to the game to see changes.";
                dialog.Controls.Add(help);
                Label rightLabel = new Label(); rightLabel.Text = "From right edge"; rightLabel.SetBounds(18, 70, 220, 28);
                Label bottomLabel = new Label(); bottomLabel.Text = "From bottom edge"; bottomLabel.SetBounds(18, 110, 220, 28);
                dialog.Controls.Add(rightLabel); dialog.Controls.Add(bottomLabel);
                NumericUpDown right = new DeferredNumericUpDown(); right.SetBounds(290, 66, 140, 30);
                right.Minimum = 0; right.Maximum = 2000; right.Increment = 10; right.Value = hudRight;
                NumericUpDown bottom = new DeferredNumericUpDown(); bottom.SetBounds(290, 106, 140, 30);
                bottom.Minimum = 0; bottom.Maximum = 2000; bottom.Increment = 10; bottom.Value = hudBottom;
                right.ValueChanged += delegate { hudRight = right.Value; };
                bottom.ValueChanged += delegate { hudBottom = bottom.Value; };
                dialog.Controls.Add(right); dialog.Controls.Add(bottom);
                Button reset = new Button(); reset.Text = "Reset position"; reset.SetBounds(18, 158, 155, 34);
                reset.Click += delegate { right.Value = 420; bottom.Value = 48; };
                Button done = new Button(); done.Text = "Done"; done.SetBounds(290, 158, 140, 34);
                done.DialogResult = DialogResult.OK; dialog.AcceptButton = done; dialog.CancelButton = done;
                dialog.Controls.Add(reset); dialog.Controls.Add(done);
                System.Drawing.Icon dialogIcon = applicationIcon == null ? null : (System.Drawing.Icon)applicationIcon.Clone();
                try { if (dialogIcon != null) dialog.Icon = dialogIcon; dialog.ShowDialog(this); }
                finally { dialog.Icon = null; if (dialogIcon != null) dialogIcon.Dispose(); }
            }
            SaveMapping();
        }

        private void SaveMapping()
        {
            if (mappingLoadError != null) { prompt.Text = "Mapping preserved: " + mappingLoadError; return; }
            try
            {
                XmlDocument doc = new XmlDocument();
                XmlElement top = doc.CreateElement("TurboTad"); doc.AppendChild(top);
                top.SetAttribute("deadzone", deadzone.Value.ToString(CultureInfo.InvariantCulture));
                string[] movementKeys = { "forwardRate", "strafeRate", "verticalRate" };
                for (int i = 0; i < 3; i++) top.SetAttribute(movementKeys[i], movementRates[i].Value.ToString(CultureInfo.InvariantCulture));
                string[] rateKeys = { "pitchRate", "yawRate", "rollRate" };
                for (int i = 0; i < 3; i++) top.SetAttribute(rateKeys[i], rotationRates[i].Value.ToString(CultureInfo.InvariantCulture));
                top.SetAttribute("boostCooldown", boostCooldown.Value.ToString(CultureInfo.InvariantCulture));
                top.SetAttribute("topSpeed", topSpeed.Value.ToString(CultureInfo.InvariantCulture));
                top.SetAttribute("hudRight", hudRight.ToString(CultureInfo.InvariantCulture));
                top.SetAttribute("hudBottom", hudBottom.ToString(CultureInfo.InvariantCulture));
                ButtonChoice boost = boostChoice.SelectedItem as ButtonChoice;
                if (boost != null && boost.Device != null)
                {
                    XmlElement item = doc.CreateElement("BoostButton");
                    item.SetAttribute("id", boost.Device.Id.ToString(CultureInfo.InvariantCulture));
                    item.SetAttribute("mid", boost.Device.Caps.Mid.ToString(CultureInfo.InvariantCulture));
                    item.SetAttribute("pid", boost.Device.Caps.Pid.ToString(CultureInfo.InvariantCulture));
                    item.SetAttribute("name", boost.Device.Caps.Name);
                    item.SetAttribute("button", boost.Button.ToString(CultureInfo.InvariantCulture));
                    top.AppendChild(item);
                }
                for (int i = 0; i < 6; i++)
                {
                    AxisChoice a = choices[i].SelectedItem as AxisChoice;
                    if (a == null || a.Device == null) continue;
                    XmlElement item = doc.CreateElement("Axis");
                    item.SetAttribute("row", i.ToString(CultureInfo.InvariantCulture));
                    item.SetAttribute("id", a.Device.Id.ToString(CultureInfo.InvariantCulture));
                    item.SetAttribute("mid", a.Device.Caps.Mid.ToString(CultureInfo.InvariantCulture));
                    item.SetAttribute("pid", a.Device.Caps.Pid.ToString(CultureInfo.InvariantCulture));
                    item.SetAttribute("name", a.Device.Caps.Name);
                    item.SetAttribute("axis", a.Axis.ToString(CultureInfo.InvariantCulture));
                    item.SetAttribute("invert", invert[i].Checked ? "1" : "0");
                    top.AppendChild(item);
                }
                top.AppendChild(WriteUniversalBindings(doc));
                string mapping = Path.Combine(root, "mapping.xml");
                string backup = Path.Combine(root, "mapping.before-uim.xml");
                if (File.Exists(mapping) && !File.Exists(backup)) File.Copy(mapping, backup);
                string pending = Path.Combine(root, "mapping.tmp.xml");
                doc.Save(pending);
                if (File.Exists(mapping)) File.Replace(pending, mapping, null);
                else File.Move(pending, mapping);
            }
            catch (Exception e) { prompt.Text = "Mapping save failed: " + e.Message; }
        }

        private void LoadMapping()
        {
            string path = Path.Combine(root, "mapping.xml");
            if (!File.Exists(path)) return;
            try
            {
                mappingLoadError = null;
                XmlReaderSettings xmlSettings = new XmlReaderSettings();
                xmlSettings.DtdProcessing = DtdProcessing.Prohibit;
                xmlSettings.XmlResolver = null;
                xmlSettings.MaxCharactersInDocument = 65536;
                XmlDocument doc = new XmlDocument(); doc.XmlResolver = null;
                using (XmlReader reader = XmlReader.Create(path, xmlSettings)) doc.Load(reader);
                XmlElement top = doc.DocumentElement;
                decimal dz;
                if (decimal.TryParse(top.GetAttribute("topSpeed"), NumberStyles.Number, CultureInfo.InvariantCulture, out dz))
                    topSpeed.Value = Math.Max(topSpeed.Minimum, Math.Min(topSpeed.Maximum, dz));
                else topSpeed.Value = 100;
                if (decimal.TryParse(top.GetAttribute("hudRight"), NumberStyles.Number, CultureInfo.InvariantCulture, out dz))
                    hudRight = Math.Max(0M, Math.Min(2000M, dz));
                if (decimal.TryParse(top.GetAttribute("hudBottom"), NumberStyles.Number, CultureInfo.InvariantCulture, out dz))
                    hudBottom = Math.Max(0M, Math.Min(2000M, dz));
                string[] movementKeys = { "forwardRate", "strafeRate", "verticalRate" };
                for (int i = 0; i < 3; i++)
                    if (decimal.TryParse(top.GetAttribute(movementKeys[i]), NumberStyles.Number, CultureInfo.InvariantCulture, out dz))
                        movementRates[i].Value = Math.Max(movementRates[i].Minimum, Math.Min(movementRates[i].Maximum, dz));
                if (decimal.TryParse(top.GetAttribute("deadzone"), NumberStyles.Number, CultureInfo.InvariantCulture, out dz))
                    deadzone.Value = Math.Max(deadzone.Minimum, Math.Min(deadzone.Maximum, dz));
                string[] rateKeys = { "pitchRate", "yawRate", "rollRate" };
                for (int i = 0; i < 3; i++)
                    if (decimal.TryParse(top.GetAttribute(rateKeys[i]), NumberStyles.Number, CultureInfo.InvariantCulture, out dz))
                        rotationRates[i].Value = Math.Max(rotationRates[i].Minimum, Math.Min(rotationRates[i].Maximum, dz));
                if (decimal.TryParse(top.GetAttribute("boostCooldown"), NumberStyles.Number, CultureInfo.InvariantCulture, out dz))
                    boostCooldown.Value = Math.Max(boostCooldown.Minimum, Math.Min(boostCooldown.Maximum, dz));
                LoadLegacyBindings(top);
                LoadStoredUniversalBindings(top["UniversalInput"]);
            }
            catch (Exception e) { mappingLoadError = e.Message; prompt.Text = "Saved mapping preserved; could not load: " + e.Message; }
        }

        [STAThread]
        public static void Main()
        {
            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            try { Run(root); }
            catch (Exception error)
            {
                MessageBox.Show("TurboTad could not start.\r\n\r\n" + error.Message,
                    "TurboTad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool MigrateLegacy(string root)
        {
            string mods = Directory.GetParent(root).FullName;
            // Prefer the latest Underwater Flight profile if both old versions exist.
            string[] legacyNames = { "UnderwaterFlight", "Tadpole6DOF" };
            List<string> legacyFolders = new List<string>();
            foreach (string name in legacyNames)
                if (Directory.Exists(Path.Combine(mods, name))) legacyFolders.Add(name);
            if (legacyFolders.Count == 0) return true;
            Process[] games = Process.GetProcessesByName("Subnautica2-Win64-Shipping");
            bool gameOpen = games.Length > 0;
            foreach (Process game in games) game.Dispose();
            if (gameOpen)
            {
                MessageBox.Show("Close Subnautica 2 before upgrading to TurboTad. Your current mapping will be preserved.", "TurboTad upgrade");
                return false;
            }
            // Validate every folder before moving any of them.
            foreach (string name in legacyFolders)
            {
                string oldScript = Path.Combine(mods, name, "Scripts", "main.lua");
                string source = File.Exists(oldScript) && new FileInfo(oldScript).Length <= 262144 ? File.ReadAllText(oldScript) : "";
                if (!source.Contains("[" + name + "]") || !source.Contains("BP_Tadpole_C"))
                {
                    MessageBox.Show("The existing " + name + " folder was not recognized. Move it outside ue4ss/Mods before using TurboTad.", "TurboTad upgrade");
                    return false;
                }
            }
            string win64 = Directory.GetParent(Directory.GetParent(mods).FullName).FullName;
            if (!File.Exists(Path.Combine(win64, "Subnautica2-Win64-Shipping.exe")))
                throw new InvalidOperationException("Install into Subnautica2/Binaries/Win64 before upgrading.");
            string newMapping = Path.Combine(root, "mapping.xml");
            string backupRoot = Path.Combine(win64, "TurboTad_Backups");
            Directory.CreateDirectory(backupRoot);
            foreach (string name in legacyFolders)
            {
                string legacy = Path.Combine(mods, name);
                string oldMapping = Path.Combine(legacy, "mapping.xml");
                if (File.Exists(oldMapping) && !File.Exists(newMapping)) File.Copy(oldMapping, newMapping);
                string backup = Path.Combine(backupRoot, name + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N"));
                Directory.Move(legacy, backup);
                string launcherName = name == "UnderwaterFlight" ? "UnderwaterFlight.cmd" : "Start_Tadpole6DOF.cmd";
                string oldLauncher = Path.Combine(win64, launcherName);
                string marker = name == "UnderwaterFlight" ? "ue4ss\\Mods\\UnderwaterFlight" : "ue4ss\\Mods\\Tadpole6DOF\\Controls.ps1";
                if (File.Exists(oldLauncher) && File.ReadAllText(oldLauncher).Contains(marker))
                    File.Move(oldLauncher, Path.Combine(backup, launcherName));
            }
            return true;
        }

        [STAThread]
        public static void Run(string root)
        {
            bool created;
            // Keep the original family mutex so old and renamed helpers cannot run together.
            using (Mutex mutex = new Mutex(true, "Local\\Tadpole6DOFControls", out created))
            {
                if (!created) { MessageBox.Show("A Tadpole controls window is already running. Close the old window before opening TurboTad."); return; }
                if (!MigrateLegacy(root)) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ControlsWindow(root));
            }
        }
    }
}
