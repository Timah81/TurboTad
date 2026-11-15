using System;
using System.Collections.Generic;
using System.Xml;
using TurboTad;

internal static class InputMapperSelfTest
{
    private sealed class Frame : IInputFrame
    {
        internal readonly Dictionary<string, double> Axes = new Dictionary<string, double>();
        internal readonly Dictionary<string, bool> Buttons = new Dictionary<string, bool>();
        public bool TryAxis(InputSource source, out double value) { return Axes.TryGetValue(source.Identity, out value); }
        public bool TryButton(InputSource source, out bool down) { return Buttons.TryGetValue(source.Identity, out down); }
    }
    private static int checks;
    private static void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
    private static void Value(AxisBinding binding, Frame frame, double want, string name)
    {
        double value; string error;
        Check(InputMapper.TryEvaluate(binding, frame, .04, out value, out error) && Math.Abs(value-want)<.000001, name);
    }
    private static int Main()
    {
        try
        {
            Frame frame = new Frame();
            InputSource axis = InputSource.Joystick("0:231:12:VKB", 0, true);
            InputSource left = InputSource.Key(65), right = InputSource.Key(68);
            AxisBinding analog = new AxisBinding { Analog = axis };
            frame.Axes[axis.Identity] = .52;
            Value(analog, frame, .5, "analog keeps proportional resolution after deadzone");
            analog.Invert = true; Value(analog, frame, -.5, "analog inversion");
            frame.Axes[axis.Identity] = .02; Value(analog, frame, 0, "center deadzone");
            AxisBinding pair = new AxisBinding { Digital = true, Negative = left, Positive = right };
            frame.Buttons[left.Identity] = false; frame.Buttons[right.Identity] = true;
            Value(pair, frame, 1, "positive button commands full rate");
            frame.Buttons[left.Identity] = true; Value(pair, frame, 0, "opposites cancel");
            Check(!InputMapper.AreDigitalSourcesReleased(new AxisBinding[]{pair}, frame), "Start rejects opposite buttons held together");
            frame.Buttons[right.Identity] = false; Value(pair, frame, -1, "negative button commands full rate");
            frame.Buttons[left.Identity] = false; Value(pair, frame, 0, "release is neutral");
            Check(InputMapper.AreDigitalSourcesReleased(new AxisBinding[]{pair}, frame), "Start allows released direction buttons");
            double v; string error;
            pair.Positive = left; Check(!InputMapper.TryEvaluate(pair, frame, .04, out v, out error), "same button rejected on both directions");
            pair.Positive = right; frame.Buttons.Remove(right.Identity);
            Check(!InputMapper.TryEvaluate(pair, frame, .04, out v, out error) && v == 0, "missing input fails neutral");
            frame.Axes[axis.Identity] = Double.NaN;
            Check(!InputMapper.TryEvaluate(analog, frame, .04, out v, out error), "nonfinite analog input rejected");
            AxisBinding blank = new AxisBinding();
            Check(!InputMapper.TryEvaluate(blank, frame, .04, out v, out error), "unassigned axis rejected");
            InputSource joyButton = InputSource.Joystick("1:231:12:VKB", 7, false);
            pair.Positive = joyButton; frame.Buttons[joyButton.Identity] = true;
            Value(pair, frame, 1, "mixed keyboard and joystick direction pair");
            AxisBinding[] unique = new AxisBinding[] { pair };
            Check(!InputMapper.ValidateSources(unique, joyButton, out error), "boost cannot reuse an axis button");
            Check(!InputMapper.ValidateSources(new AxisBinding[]{pair,pair}, null, out error), "duplicate axis sources rejected");
            Check(InputMapper.ValidateSources(unique, InputSource.Key(90), out error), "independent boost accepted");
            pair.Positive = right;
            string device1 = "0:231:12:Microsoft PC-joystick driver";
            string device2 = "1:231:12:Microsoft PC-joystick driver";
            AxisBinding[] screenshotAxes = new AxisBinding[] {
                new AxisBinding { Analog = InputSource.Joystick(device1, 1, true) },
                new AxisBinding { Analog = InputSource.Joystick(device2, 3, true) },
                new AxisBinding { Analog = InputSource.Joystick(device1, 3, true) },
                new AxisBinding { Analog = InputSource.Joystick(device1, 5, true) },
                new AxisBinding { Analog = InputSource.Joystick(device1, 2, true) },
                new AxisBinding { Analog = InputSource.Joystick(device2, 2, true), Invert = true }
            };
            Check(InputMapper.ValidateSources(screenshotAxes, null, out error), "six distinct analog axes from user screenshot are accepted");
            XmlDocument forged = new XmlDocument(); forged.LoadXml("<Key kind='key' code='65' device='alternate'/>");
            InputSource importedKey = InputMapper.ReadSource(forged.DocumentElement, false);
            Check(importedKey.Identity == left.Identity, "keyboard identity cannot be changed by device attribute");
            XmlDocument doc = new XmlDocument(); doc.LoadXml("<TurboTad/>");
            XmlElement stored = InputMapper.WriteAxis(doc, 2, pair); doc.DocumentElement.AppendChild(stored);
            AxisBinding restored = InputMapper.ReadAxis(stored);
            Check(restored.Digital && restored.Negative.Identity == left.Identity && restored.Positive.Identity == right.Identity, "digital binding XML roundtrip");
            Check(InputMapper.PreferLearnCandidate(true, .65, false, .95), "XInput wins mirrored WinMM axis");
            Check(!InputMapper.PreferLearnCandidate(false, 1.0, true, .65), "WinMM cannot override XInput");
            Check(InputMapper.PreferLearnCandidate(false, .85, false, .60), "WinMM-only strongest motion still learns");
            Check(!InputMapper.PreferLearnCandidate(true, .25, false, .80), "below-threshold XInput motion ignored");
            InputSource mirroredWinmmButton = InputSource.Joystick("3:1118:654:Microsoft PC-joystick driver", 4, false);
            InputSource xinputBumper = InputSource.XboxButton(0, 8); // LB
            Check(InputMapper.PreferButtonLearnCandidate(mirroredWinmmButton, null),
                "WinMM-only button can start capture");
            Check(InputMapper.PreferButtonLearnCandidate(xinputBumper, mirroredWinmmButton),
                "XInput button overrides mirrored WinMM button");
            Check(!InputMapper.PreferButtonLearnCandidate(mirroredWinmmButton, xinputBumper),
                "WinMM cannot override captured XInput button");
            Check(InputMapper.PreferButtonLearnCandidate(xinputBumper, InputSource.Key(66)),
                "XInput button also wins over keyboard-translated button");
            Check(!InputMapper.PreferButtonLearnCandidate(InputSource.Key(66), xinputBumper),
                "keyboard event cannot replace XInput button");
            Check(!InputMapper.PreferButtonLearnCandidate(null, mirroredWinmmButton),
                "missing candidate cannot replace a real button");
            InputSource xboxAxis = InputSource.XboxAxis(0, 1);
            InputSource xboxButton = InputSource.XboxButton(0, 10); // A
            Check(xboxAxis.Valid(true) && !xboxAxis.Valid(false), "Xbox stick source is analog only");
            Check(xboxButton.Valid(false) && !xboxButton.Valid(true), "Xbox button source is digital only");
            Check(!InputSource.XboxAxis(4, 0).Valid(true), "invalid Xbox slot rejected");
            Check(InputSource.XboxAxis(0, 4).Valid(true) && InputSource.XboxAxis(0, 5).Valid(true), "LT RT are analog");
            Check(InputSource.XboxAxis(0, 6).Valid(true), "optional paired triggers are an analog axis");
            Check(!InputSource.XboxAxis(0, 7).Valid(true), "invalid Xbox axis rejected");
            Check(!InputSource.XboxButton(0, 14).Valid(false), "invalid Xbox button rejected");
            Check(!new InputSource{Kind="xaxis",Device="xinput:0:forged",Code=0}.Valid(true), "forged Xbox identity rejected");
            Check(InputMapper.ReadSource(InputMapper.WriteSource(doc,"Analog",xboxAxis),true).Identity==xboxAxis.Identity,
                "Xbox axis XML roundtrip");
            Check(InputMapper.ReadSource(InputMapper.WriteSource(doc,"Positive",xboxButton),false).Identity==xboxButton.Identity,
                "Xbox button XML roundtrip");
            InputSource leftTrigger = InputSource.XboxAxis(0, 4);
            InputSource rightTrigger = InputSource.XboxAxis(0, 5);
            frame.Axes[leftTrigger.Identity] = .65;
            Value(new AxisBinding { Analog = leftTrigger }, frame, (.65-.04)/.96, "LT proportional");
            frame.Axes[rightTrigger.Identity] = .25;
            Value(new AxisBinding { Analog = rightTrigger, Invert = true }, frame, -(.25-.04)/.96, "RT invert negative-only");
            Check(InputMapper.ReadSource(InputMapper.WriteSource(doc, "Analog", leftTrigger), true).Identity == leftTrigger.Identity, "LT persistence");
            InputSource pairedTriggers = InputSource.XboxAxis(0, 6);
            frame.Axes[pairedTriggers.Identity] = .65;
            AxisBinding pairedBinding = new AxisBinding { Analog = pairedTriggers };
            Value(pairedBinding, frame, (.65-.04)/.96, "paired RT positive proportional");
            frame.Axes[pairedTriggers.Identity] = -.35;
            Value(pairedBinding, frame, -(.35-.04)/.96, "paired LT negative proportional");
            pairedBinding.Invert = true;
            Value(pairedBinding, frame, (.35-.04)/.96, "paired trigger inversion");
            frame.Axes[pairedTriggers.Identity] = 0;
            Value(pairedBinding, frame, 0, "paired triggers neutral");
            Check(InputMapper.ReadSource(InputMapper.WriteSource(doc, "Analog", pairedTriggers), true).Identity == pairedTriggers.Identity,
                "paired trigger profile roundtrip");
            Check(InputMapper.ValidateSources(new AxisBinding[]{new AxisBinding{Analog=leftTrigger},
                new AxisBinding{Analog=rightTrigger}}, null, out error),
                "LT and RT can be assigned independently to different flight axes");
            Check(!InputMapper.ValidateSources(new AxisBinding[]{new AxisBinding{Analog=pairedTriggers},
                new AxisBinding{Analog=leftTrigger}}, null, out error),
                "paired axis reserves LT from simultaneous reassignment");
            Check(!InputMapper.ValidateSources(new AxisBinding[]{new AxisBinding{Analog=rightTrigger},
                new AxisBinding{Analog=pairedTriggers}}, null, out error),
                "paired axis reserves RT regardless of assignment order");
            Check(InputMapper.ValidateSources(new AxisBinding[]{new AxisBinding{Analog=pairedTriggers},
                new AxisBinding{Analog=InputSource.XboxAxis(1, 4)}}, null, out error),
                "other Xbox controller triggers remain independent");
            frame.Axes[xboxAxis.Identity] = .75;
            Value(new AxisBinding{Analog=xboxAxis},frame,(.75-.04)/.96,"Xbox axis stays proportional");
            frame.Buttons[xboxButton.Identity]=true;
            Value(new AxisBinding{Digital=true,Negative=left,Positive=xboxButton},frame,1,"Xbox button as digital direction");
            Check(!InputMapper.ValidateSources(new AxisBinding[]{new AxisBinding{Digital=true,Negative=left,Positive=xboxButton}},xboxButton,out error),
                "Xbox button cannot also be boost");
            InputSource offline = InputSource.Joystick("3:231:12:Offline VKB", 5, true);
            AxisBinding missing = new AxisBinding { Analog = offline, Invert = true };
            restored = InputMapper.ReadAxis(InputMapper.WriteAxis(doc, 4, missing));
            Check(restored.Analog.Device == offline.Device && restored.Invert, "unavailable device identity retained");
            stored.SetAttribute("mode", "garbage");
            bool rejected = false; try { InputMapper.ReadAxis(stored); } catch (FormatException) { rejected = true; }
            Check(rejected, "unknown binding mode rejected");
            DigitalFocusGate gate = new DigitalFocusGate();
            Check(!gate.Filter(left.Identity, true, false), "background input suppressed");
            Check(!gate.Filter(left.Identity, true, true), "held key on refocus suppressed");
            Check(!gate.Filter(left.Identity, false, true), "release rearms key");
            Check(gate.Filter(left.Identity, true, true), "fresh key after refocus accepted");
            Console.WriteLine("Input mapper tests passed: " + checks); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("Input mapper test failed: " + e); return 1; }
    }
}
