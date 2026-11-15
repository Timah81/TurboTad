using System;
namespace TurboTad
{
    internal static class XInputDiagnosticSelfTest
    {
        private static void Check(bool valid, string name)
        {
            if (!valid) throw new Exception("XInput diagnostic test failed: " + name);
        }

        [STAThread]
        private static int Main()
        {
            XInputProbe.State s = new XInputProbe.State();
            Check(XInputProbe.Describe(0, false, s, null).Contains("not detected"), "absent");
            Check(XInputProbe.Describe(1, false, s, "XInput runtime unavailable").Contains("runtime unavailable"), "missing runtime");
            s.Pad.LeftX = -12345;
            s.Pad.RightY = 21000;
            s.Pad.LeftTrigger = 64;
            s.Pad.RightTrigger = 255;
            s.Pad.Buttons = 0x1100; // A + left bumper.
            string line = XInputProbe.Describe(0, true, s, null);
            Check(line.Contains("CONNECTED") && line.Contains("-12345") && line.Contains("21000"), "sticks");
            Check(line.Contains("64") && line.Contains("255"), "independent triggers");
            Check(line.Contains("Buttons: LB A"), "button masks");
            Check(XInputProbe.DescribeButtons(0) == "none", "released buttons");
            s.Pad.LeftX = -32768; s.Pad.LeftY = 32767;
            s.Pad.RightX = -16384; s.Pad.RightY = 16384;
            Check(XInputProbe.AxisValue(s,0) == -1 && XInputProbe.AxisValue(s,1) == 1, "stick endpoints");
            Check(Math.Abs(XInputProbe.AxisValue(s,2) + .5) < .0001, "negative stick proportional");
            Check(Math.Abs(XInputProbe.AxisValue(s,3) - .5) < .0001, "positive stick proportional");
            Check(XInputProbe.ButtonDown(s,10) && XInputProbe.ButtonDown(s,8), "Xbox button bits");
            Check(!XInputProbe.ButtonDown(s,13) && !XInputProbe.ButtonDown(s,20), "unpressed/invalid buttons");
            Console.WriteLine("Read-only XInput diagnostics: formatting checks passed.");
            return 0;
        }
    }
}
