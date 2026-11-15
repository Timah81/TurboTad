using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
internal static class InputMapperUiSelfTest
{
    private static BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Call(Type t, object f, string name, params object[] args) { return t.GetMethod(name, Flags).Invoke(f, args); }
    private static void AssertSameXboxAxisOptions(ComboBox[] choices)
    {
        string expected = null;
        for (int row = 0; row < choices.Length; row++)
        {
            System.Collections.Generic.List<string> items = new System.Collections.Generic.List<string>();
            System.Collections.Generic.HashSet<string> unique = new System.Collections.Generic.HashSet<string>();
            foreach (object candidate in choices[row].Items)
            {
                string label = candidate.ToString();
                if (!label.StartsWith("Xbox ", StringComparison.Ordinal)) continue;
                if (!unique.Add(label)) throw new Exception("Duplicate Xbox axis in flight row " + row + ": " + label);
                items.Add(label);
            }
            string actual = string.Join("|", items.ToArray());
            if (expected == null) expected = actual;
            else if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception("Xbox analog options differ between flight row 0 and row " + row);
        }
    }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            string root = args[1]; Directory.CreateDirectory(root);
            string mapping = Path.Combine(root, "mapping.xml");
            string legacy = "<TurboTad deadzone='0.04' topSpeed='175'><Axis row='0' id='3' mid='231' pid='12' name='Offline VKB' axis='2' invert='1'/><BoostButton id='3' mid='231' pid='12' name='Offline VKB' button='4'/></TurboTad>";
            File.WriteAllText(mapping, legacy);
            Assembly a = Assembly.LoadFrom(args[0]); Type t = a.GetType("TurboTad.ControlsWindow", true);
            // Every digital direction must have a manual Xbox button picker,
            // even when no Xbox is currently connected or awake.
            Type pairType = a.GetType("TurboTad.DirectionPairDialog", true);
            Type axisType = a.GetType("TurboTad.AxisBinding", true);
            Type deviceType = a.GetType("TurboTad.Device", true);
            object emptyPair = Activator.CreateInstance(axisType, true);
            object emptyDevices = Activator.CreateInstance(
                typeof(System.Collections.Generic.List<>).MakeGenericType(deviceType));
            using (Form pair = (Form)Activator.CreateInstance(pairType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[]{ "Thrust up / down", "Down", "Up", emptyPair, emptyDevices }, null))
            {
                ComboBox negativeOptions = (ComboBox)pairType.GetField("negativeChoice", Flags).GetValue(pair);
                ComboBox positiveOptions = (ComboBox)pairType.GetField("positiveChoice", Flags).GetValue(pair);
                int lb = -1, rb = -1;
                for (int i = 0; i < negativeOptions.Items.Count; i++)
                {
                    if (negativeOptions.Items[i].ToString() == "Xbox 1 / LB") lb = i;
                    if (positiveOptions.Items[i].ToString() == "Xbox 1 / RB") rb = i;
                }
                if (lb < 0 || rb < 0) throw new Exception("Manual digital pair has no Xbox bumper selections");
                negativeOptions.SelectedIndex = lb; positiveOptions.SelectedIndex = rb;
                object selectedNegative = pairType.GetField("negative", Flags).GetValue(pair);
                object selectedPositive = pairType.GetField("positive", Flags).GetValue(pair);
                if ((string)selectedNegative.GetType().GetField("Device", Flags).GetValue(selectedNegative) != "xinput:0" ||
                    (int)selectedNegative.GetType().GetField("Code", Flags).GetValue(selectedNegative) != 8 ||
                    (string)selectedPositive.GetType().GetField("Device", Flags).GetValue(selectedPositive) != "xinput:0" ||
                    (int)selectedPositive.GetType().GetField("Code", Flags).GetValue(selectedPositive) != 9)
                    throw new Exception("Manual Xbox bumpers were not selected as XInput sources");
            }
            using (Form f = (Form)Activator.CreateInstance(t, new object[]{ root }))
            {
                Call(t, f, "SaveMapping");
                XmlDocument d = new XmlDocument(); d.Load(mapping);
                if (d.SelectSingleNode("/TurboTad/Axis[@name='Offline VKB']") == null) throw new Exception("Saving erased unplugged legacy axis");
                if (d.SelectSingleNode("/TurboTad/BoostButton[@button='4']") == null) throw new Exception("Saving erased unplugged boost");
                if (!File.Exists(Path.Combine(root, "mapping.before-uim.xml"))) throw new Exception("Pre-upgrade mapping backup missing");
                f.Close();
            }
            XmlDocument config = new XmlDocument(); config.LoadXml("<TurboTad><UniversalInput version='1'/></TurboTad>");
            XmlElement uim = (XmlElement)config.DocumentElement.FirstChild;
            for (int row=0; row<6; row++)
            {
                XmlElement b = config.CreateElement("Binding"); b.SetAttribute("row",row.ToString()); b.SetAttribute("mode","digital");
                foreach (string name in new string[]{"Negative","Positive"})
                {
                    XmlElement s=config.CreateElement(name);s.SetAttribute("kind","key");s.SetAttribute("code",(65+row*2+(name=="Positive"?1:0)).ToString());b.AppendChild(s);
                }
                uim.AppendChild(b);
            }
            config.Save(mapping);
            using(Form f = (Form)Activator.CreateInstance(t,new object[]{root}))
            {
                object[] evaluated = new object[]{null};
                if (!(bool)Call(t,f,"TryGetAxes",evaluated)) throw new Exception("Keyboard-only mapping requires a joystick");
                Call(t,f,"StartControls");
                if (!(bool)t.GetField("running",Flags).GetValue(f)) throw new Exception("Keyboard-only controls failed to start");
                Call(t,f,"StopControls"); Call(t,f,"SaveMapping");
                config.Load(mapping);
                if (config.SelectNodes("/TurboTad/UniversalInput/Binding[@mode='digital']").Count!=6) throw new Exception("Digital mapping roundtrip failed");
                XmlDocument duplicate = new XmlDocument(); duplicate.LoadXml(config.OuterXml);
                ((XmlElement)duplicate.SelectSingleNode("/TurboTad/UniversalInput/Binding[@row='1']/Negative")).SetAttribute("code","65");
                string previousMapping = File.ReadAllText(mapping);
                bool duplicateRejected = false;
                try { Call(t,f,"LoadUniversalBindings",duplicate.DocumentElement["UniversalInput"]); }
                catch(TargetInvocationException e) { if(e.InnerException is FormatException)duplicateRejected=true; else throw; }
                if(!duplicateRejected)throw new Exception("Conflicting profile was accepted");
                Call(t,f,"SaveMapping");
                if(File.ReadAllText(mapping)!=previousMapping)throw new Exception("Rejected profile changed current mapping");
                string[] words = File.ReadAllText(Path.Combine(root,"input.txt")).Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
                if(words.Length!=21 || words[0]!="T6D7" || words[3]!="0") throw new Exception("Existing stopped protocol changed");
                for(int i=4;i<10;i++) if(Double.Parse(words[i],System.Globalization.CultureInfo.InvariantCulture)!=0) throw new Exception("Stop sent motion");
                f.Close();
            }
            XmlDocument savedConflict = new XmlDocument(); savedConflict.LoadXml(config.OuterXml);
            ((XmlElement)savedConflict.SelectSingleNode("/TurboTad/UniversalInput/Binding[@row='1']/Negative")).SetAttribute("code","65");
            savedConflict.Save(mapping);
            using(Form f = (Form)Activator.CreateInstance(t,new object[]{root}))
            {
                if(t.GetField("mappingLoadError",Flags).GetValue(f)!=null)
                    throw new Exception("Readable saved conflict is latched as an unrecoverable load error");
                Call(t,f,"StartControls");
                if((bool)t.GetField("running",Flags).GetValue(f))throw new Exception("Conflicting saved bindings started");
                string conflict=(string)t.GetField("mappingError",Flags).GetValue(f);
                if(conflict.IndexOf("Forward",StringComparison.OrdinalIgnoreCase)<0 || conflict.IndexOf("Strafe",StringComparison.OrdinalIgnoreCase)<0)
                    throw new Exception("Conflict does not name both affected axes");
                Array pairs = (Array)t.GetField("directionPairs",Flags).GetValue(f);
                object rowOne = pairs.GetValue(1);
                object negative = rowOne.GetType().GetField("Negative",Flags).GetValue(rowOne);
                negative.GetType().GetField("Code",Flags).SetValue(negative,67);
                Call(t,f,"StartControls");
                if(!(bool)t.GetField("running",Flags).GetValue(f))throw new Exception("Corrected saved conflict still blocks Start");
                Call(t,f,"StopControls"); f.Close();
            }
            XmlDocument xbox = new XmlDocument(); xbox.LoadXml(config.OuterXml);
            XmlElement xboxRow = (XmlElement)xbox.SelectSingleNode("/TurboTad/UniversalInput/Binding[@row='0']");
            xboxRow.SetAttribute("mode", "analog"); xboxRow.SetAttribute("invert", "1");
            while(xboxRow.HasChildNodes) xboxRow.RemoveChild(xboxRow.FirstChild);
            XmlElement xboxAnalog = xbox.CreateElement("Analog");
            xboxAnalog.SetAttribute("kind", "xaxis"); xboxAnalog.SetAttribute("device", "xinput:0"); xboxAnalog.SetAttribute("code", "0");
            xboxRow.AppendChild(xboxAnalog);
            // Saving a mapping always creates one Boost node, even when unassigned.
            // Update that existing node instead of adding a duplicate that the loader ignores.
            XmlElement xboxBoost = xbox.DocumentElement["UniversalInput"]["Boost"];
            if (xboxBoost == null) throw new Exception("Xbox test fixture is missing its Boost node");
            xboxBoost.SetAttribute("kind", "xbutton"); xboxBoost.SetAttribute("device", "xinput:0"); xboxBoost.SetAttribute("code", "10");
            if (xbox.DocumentElement["UniversalInput"].SelectNodes("Boost").Count != 1)
                throw new Exception("Xbox test fixture created duplicate Boost nodes");
            xbox.Save(mapping);
            using(Form f=(Form)Activator.CreateInstance(t,new object[]{root}))
            {
                if(t.GetField("mappingLoadError",Flags).GetValue(f)!=null) throw new Exception("Xbox profile failed to load while controller unplugged");
                if(t.GetField("xboxFlightTest",Flags)!=null) throw new Exception("Obsolete Xbox opt-in checkbox remains");
                // No special opt-in. On an actual Windows gaming PC, Xbox 1
                // may be connected during this automated test. Only demand
                // disconnected rejection when it really is disconnected.
                bool[] availableXbox = (bool[])t.GetField("xboxConnected",Flags).GetValue(f);
                bool xboxOnePresent = availableXbox[0];
                Call(t,f,"StartControls");
                bool startedXbox = (bool)t.GetField("running",Flags).GetValue(f);
                if(!xboxOnePresent && startedXbox) throw new Exception("Disconnected Xbox mapping started");
                if(startedXbox) Call(t,f,"StopControls");
                Call(t,f,"SaveMapping");
                config.Load(mapping);
                XmlElement savedXbox=(XmlElement)config.SelectSingleNode("/TurboTad/UniversalInput/Binding[@row='0']/Analog");
                if(savedXbox==null || savedXbox.GetAttribute("kind")!="xaxis" || savedXbox.GetAttribute("device")!="xinput:0")
                    throw new Exception("Xbox axis did not survive SaveMapping");
                if(((XmlElement)config.SelectSingleNode("/TurboTad/UniversalInput/Binding[@row='0']")).GetAttribute("invert")!="1")
                    throw new Exception("Xbox axis manual inversion was lost");
                XmlElement savedBoost=(XmlElement)config.SelectSingleNode("/TurboTad/UniversalInput/Boost");
                if(savedBoost==null || savedBoost.GetAttribute("kind")!="xbutton")throw new Exception("Xbox boost binding did not roundtrip");
                // Saved Xbox bindings must populate all six choices for that player,
                // even on a machine where the physical pad is disconnected.
                ComboBox[] choices = (ComboBox[])t.GetField("choices",Flags).GetValue(f);
                AssertSameXboxAxisOptions(choices);
                for (int row = 0; row < choices.Length; row++)
                {
                    int count = 0;
                    foreach (object candidate in choices[row].Items)
                        if (candidate.ToString().StartsWith("Xbox 1 / ", StringComparison.Ordinal)) count++;
                    if (count != 7) throw new Exception("Saved offline Xbox axes missing in row " + row);
                    bool pairedFound = false;
                    foreach (object candidate in choices[row].Items)
                        if (candidate.ToString() == "Xbox 1 / LT + RT (paired axis)") pairedFound = true;
                    if (!pairedFound) throw new Exception("Optional paired trigger choice missing in row " + row);
                }
                // Regression: the Xbox can wake after the initial joystick/device scan.
                // Simulate that change without requiring a physical controller in CI.
                CheckBox[] invert = (CheckBox[])t.GetField("invert",Flags).GetValue(f);
                bool[] xboxConnected = (bool[])t.GetField("xboxConnected",Flags).GetValue(f);
                System.Collections.IDictionary baseline = (System.Collections.IDictionary)t.GetField("baseline",Flags).GetValue(f);
                int learnRow = 2, player = 2;
                for (int i = choices[learnRow].Items.Count - 1; i >= 0; i--)
                {
                    object candidate = choices[learnRow].Items[i];
                    if (candidate.GetType().GetField("Xbox",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(candidate)!=null)
                        choices[learnRow].Items.RemoveAt(i);
                }
                Array.Clear(xboxConnected, 0, xboxConnected.Length);
                xboxConnected[player] = true;
                invert[learnRow].Checked = true;
                Call(t,f,"EnsureXboxLearnCandidates",learnRow);
                // Discovery repairs the intentionally divergent row and updates all
                // others, regardless of which row initiated Learn.
                AssertSameXboxAxisOptions(choices);
                for (int row = 0; row < choices.Length; row++)
                {
                    int count = 0;
                    foreach (object candidate in choices[row].Items)
                        if (candidate.ToString().StartsWith("Xbox 3 / ", StringComparison.Ordinal)) count++;
                    if (count != 7) throw new Exception("Hotplug Xbox axes missing in row " + row);
                }
                Call(t,f,"EnsureXboxLearnCandidates",learnRow);
                AssertSameXboxAxisOptions(choices);
                for(int axis=0;axis<7;axis++)
                {
                    string identity="xaxis:xinput:"+player+":"+axis;
                    if(!baseline.Contains(identity) || (double)baseline[identity]!=0)
                        throw new Exception("Late Xbox axis missing its neutral Learn baseline: "+identity);
                }
                if (!invert[learnRow].Checked)throw new Exception("Xbox discovery changed manual inversion");
                f.Close();
            }
            // Opt-in paired mode survives a profile load and save while unplugged.
            // Individual LT and RT must remain selectable in the same dropdowns.
            XmlDocument pairedConfig = new XmlDocument(); pairedConfig.Load(mapping);
            ((XmlElement)pairedConfig.SelectSingleNode("/TurboTad/UniversalInput/Binding[@row='0']/Analog"))
                .SetAttribute("code", "6");
            pairedConfig.Save(mapping);
            using(Form f=(Form)Activator.CreateInstance(t,new object[]{root}))
            {
                if(t.GetField("mappingLoadError",Flags).GetValue(f)!=null)
                    throw new Exception("Paired trigger profile was rejected on load");
                ComboBox[] choices=(ComboBox[])t.GetField("choices",Flags).GetValue(f);
                AssertSameXboxAxisOptions(choices);
                if (choices[0].SelectedItem.ToString()!="Xbox 1 / LT + RT (paired axis)")
                    throw new Exception("Saved paired trigger assignment not restored");
                for (int row=0; row<choices.Length; row++)
                {
                    bool lt=false, rt=false, pairOption=false;
                    foreach(object item in choices[row].Items)
                    {
                        string label=item.ToString();
                        if(label=="Xbox 1 / LT")lt=true;
                        if(label=="Xbox 1 / RT")rt=true;
                        if(label=="Xbox 1 / LT + RT (paired axis)")pairOption=true;
                    }
                    if(!lt||!rt||!pairOption)
                        throw new Exception("Separate and paired options missing in row "+row);
                }
                Call(t,f,"SaveMapping");
                XmlDocument roundtrip=new XmlDocument();roundtrip.Load(mapping);
                XmlElement analogNode=(XmlElement)roundtrip.SelectSingleNode("/TurboTad/UniversalInput/Binding[@row='0']/Analog");
                if(analogNode==null||analogNode.GetAttribute("code")!="6"||
                    analogNode.GetAttribute("kind")!="xaxis")
                    throw new Exception("Paired trigger selection did not survive save");
                f.Close();
            }
            string invalid = "<TurboTad><UniversalInput version='99'/></TurboTad>";
            File.WriteAllText(mapping,invalid);
            using(Form f = (Form)Activator.CreateInstance(t,new object[]{root}))
            {
                Call(t,f,"SaveMapping"); f.Close();
                if(File.ReadAllText(mapping)!=invalid)throw new Exception("Unsupported mapping was overwritten");
            }
            Console.WriteLine("Mapper GUI tests passed: manual Xbox bumper pair, legacy/offline migration, backup, keyboard-only Start, digital persistence, neutral T6D7 Stop.");return 0;
        }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
