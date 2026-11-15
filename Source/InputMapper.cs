using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace TurboTad
{
    internal sealed class InputSource
    {
        internal string Kind, Device;
        internal int Code;
        internal string Identity { get { return Kind + ":" + (Kind == "key" ? "" : Device) + ":" + Code; } }
        internal static InputSource Key(int code) { return new InputSource { Kind = "key", Code = code, Device = "" }; }
        internal static InputSource Joystick(string device, int code, bool analog)
        { return new InputSource { Kind = analog ? "axis" : "button", Device = device, Code = code }; }
        internal static InputSource XboxAxis(int player, int axis)
        { return new InputSource { Kind = "xaxis", Device = "xinput:" + player.ToString(CultureInfo.InvariantCulture), Code = axis }; }
        internal static InputSource XboxButton(int player, int button)
        { return new InputSource { Kind = "xbutton", Device = "xinput:" + player.ToString(CultureInfo.InvariantCulture), Code = button }; }
        internal static bool TryXboxPlayer(string device, out int player)
        {
            player = -1;
            if (device == null || device.Length != 8 || !device.StartsWith("xinput:", StringComparison.Ordinal) ||
                device[7] < '0' || device[7] > '3') return false;
            player = device[7] - '0';
            return true;
        }
        internal bool Valid(bool analog)
        {
            int slot;
            if (analog) return (Kind == "axis" && !String.IsNullOrEmpty(Device) && Code >= 0 && Code < 6) ||
                (Kind == "xaxis" && TryXboxPlayer(Device, out slot) && Code >= 0 && Code < 4);
            if (Kind == "key") return Code >= 8 && Code <= 254;
            if (Kind == "button") return !String.IsNullOrEmpty(Device) && Code >= 0 && Code < 32;
            return Kind == "xbutton" && TryXboxPlayer(Device, out slot) && Code >= 0 && Code < 14;
        }
    }

    internal sealed class AxisBinding
    {
        internal bool Digital, Invert;
        internal InputSource Analog, Negative, Positive;
        internal IEnumerable<InputSource> Sources
        {
            get
            {
                if (Digital) { if (Negative != null) yield return Negative; if (Positive != null) yield return Positive; }
                else if (Analog != null) yield return Analog;
            }
        }
    }

    internal interface IInputFrame
    {
        bool TryAxis(InputSource source, out double value);
        bool TryButton(InputSource source, out bool down);
    }

    // No device API, focus query, file I/O, rate or flight-physics logic here.
    internal static class InputMapper
    {
        internal static bool TryEvaluate(AxisBinding binding, IInputFrame frame, double deadzone,
            out double value, out string error)
        {
            value = 0; error = null;
            if (binding == null) { error = "Unassigned axis"; return false; }
            if (binding.Digital)
            {
                if (binding.Negative == null || binding.Positive == null ||
                    !binding.Negative.Valid(false) || !binding.Positive.Valid(false) ||
                    binding.Negative.Identity == binding.Positive.Identity)
                { error = "Assign two different direction controls"; return false; }
                bool negative, positive;
                if (!frame.TryButton(binding.Negative, out negative) || !frame.TryButton(binding.Positive, out positive))
                { error = "Direction control disconnected"; return false; }
                value = (positive ? 1 : 0) - (negative ? 1 : 0);
            }
            else
            {
                double raw;
                if (binding.Analog == null || !binding.Analog.Valid(true) || !frame.TryAxis(binding.Analog, out raw))
                { error = "Analog axis unassigned or disconnected"; return false; }
                if (Double.IsNaN(raw) || Double.IsInfinity(raw) || deadzone < 0 || deadzone >= 1)
                { error = "Invalid analog value"; return false; }
                raw = Math.Max(-1, Math.Min(1, raw));
                value = Math.Abs(raw) <= deadzone ? 0 : Math.Sign(raw) * (Math.Abs(raw) - deadzone) / (1 - deadzone);
            }
            if (binding.Invert) value = -value;
            return true;
        }

        internal static bool AreDigitalSourcesReleased(AxisBinding[] axes, IInputFrame frame)
        {
            foreach (AxisBinding axis in axes)
                if (axis.Digital)
                    foreach (InputSource source in axis.Sources)
                    {
                        bool down;
                        if (!frame.TryButton(source, out down) || down) return false;
                    }
            return true;
        }

        internal static bool ValidateSources(AxisBinding[] axes, InputSource boost, out string error)
        {
            string[] names = { "Forward / reverse", "Strafe right / left", "Thrust up / down", "Pitch up / down", "Yaw right / left", "Roll right / left" };
            Dictionary<string, string> used = new Dictionary<string, string>();
            for (int row = 0; row < axes.Length; row++)
                foreach (InputSource source in axes[row].Sources)
                {
                    string previous;
                    string action = row < names.Length ? names[row] : "Axis " + (row + 1);
                    if (used.TryGetValue(source.Identity, out previous))
                    { error = previous + " and " + action + " use the same control"; return false; }
                    used.Add(source.Identity, action);
                }
            if (boost != null)
            {
                string previous;
                if (!boost.Valid(false)) { error = "Invalid boost button or key"; return false; }
                if (used.TryGetValue(boost.Identity, out previous))
                { error = "Boost and " + previous + " use the same control"; return false; }
            }
            error = null; return true;
        }

        internal static XmlElement WriteSource(XmlDocument doc, string name, InputSource source)
        {
            XmlElement element = doc.CreateElement(name);
            if (source != null)
            {
                element.SetAttribute("kind", source.Kind);
                element.SetAttribute("device", source.Device ?? "");
                element.SetAttribute("code", source.Code.ToString(CultureInfo.InvariantCulture));
            }
            return element;
        }
        internal static InputSource ReadSource(XmlElement element, bool analog)
        {
            if (element == null || !element.HasAttribute("kind")) return null;
            int code;
            if (!Int32.TryParse(element.GetAttribute("code"), NumberStyles.Integer, CultureInfo.InvariantCulture, out code))
                throw new FormatException("Invalid input code");
            InputSource source = new InputSource { Kind = element.GetAttribute("kind"), Device = element.GetAttribute("device"), Code = code };
            if (source.Kind == "key") source.Device = "";
            if (!source.Valid(analog)) throw new FormatException("Invalid input source");
            return source;
        }
        internal static XmlElement WriteAxis(XmlDocument doc, int row, AxisBinding binding)
        {
            XmlElement element = doc.CreateElement("Binding");
            element.SetAttribute("row", row.ToString(CultureInfo.InvariantCulture));
            element.SetAttribute("mode", binding.Digital ? "digital" : "analog");
            element.SetAttribute("invert", binding.Invert ? "1" : "0");
            if (binding.Digital)
            {
                element.AppendChild(WriteSource(doc, "Negative", binding.Negative));
                element.AppendChild(WriteSource(doc, "Positive", binding.Positive));
            }
            else element.AppendChild(WriteSource(doc, "Analog", binding.Analog));
            return element;
        }
        internal static AxisBinding ReadAxis(XmlElement element)
        {
            string mode = element.GetAttribute("mode");
            if (mode != "analog" && mode != "digital") throw new FormatException("Unknown mapping mode");
            AxisBinding binding = new AxisBinding { Digital = mode == "digital", Invert = element.GetAttribute("invert") == "1" };
            binding.Analog = ReadSource(element["Analog"], true);
            binding.Negative = ReadSource(element["Negative"], false);
            binding.Positive = ReadSource(element["Positive"], false);
            return binding;
        }
    }

    // A digital input held across focus changes must be released before reactivation.
    internal sealed class DigitalFocusGate
    {
        private readonly Dictionary<string, bool> armed = new Dictionary<string, bool>();
        internal void Reset() { armed.Clear(); }
        internal bool Filter(string identity, bool down, bool foreground)
        {
            bool ready;
            if (!foreground) { armed[identity] = false; return false; }
            if (!armed.TryGetValue(identity, out ready)) ready = false;
            if (!down) { armed[identity] = true; return false; }
            return ready;
        }
    }
}
