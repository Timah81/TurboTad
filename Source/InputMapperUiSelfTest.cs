using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
internal static class InputMapperUiSelfTest
{
    private static BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Call(Type t, object f, string name, params object[] args) { return t.GetMethod(name, Flags).Invoke(f, args); }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            string root = args[1]; Directory.CreateDirectory(root);
            string mapping = Path.Combine(root, "mapping.xml");
            string legacy = "<TurboTad deadzone='0.04' topSpeed='175'><Axis row='0' id='3' mid='231' pid='12' name='Offline VKB' axis='2' invert='1'/><BoostButton id='3' mid='231' pid='12' name='Offline VKB' button='4'/></TurboTad>";
            File.WriteAllText(mapping, legacy);
            Assembly a = Assembly.LoadFrom(args[0]); Type t = a.GetType("TurboTad.ControlsWindow", true);
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
                Call(t,f,"StartControls");
                if((bool)t.GetField("running",Flags).GetValue(f)) throw new Exception("Xbox binding was allowed to drive native game controls");
                Call(t,f,"SaveMapping");
                config.Load(mapping);
                XmlElement savedXbox=(XmlElement)config.SelectSingleNode("/TurboTad/UniversalInput/Binding[@row='0']/Analog");
                if(savedXbox==null || savedXbox.GetAttribute("kind")!="xaxis" || savedXbox.GetAttribute("device")!="xinput:0")
                    throw new Exception("Xbox axis did not survive SaveMapping");
                if(((XmlElement)config.SelectSingleNode("/TurboTad/UniversalInput/Binding[@row='0']")).GetAttribute("invert")!="1")
                    throw new Exception("Xbox axis manual inversion was lost");
                XmlElement savedBoost=(XmlElement)config.SelectSingleNode("/TurboTad/UniversalInput/Boost");
                if(savedBoost==null || savedBoost.GetAttribute("kind")!="xbutton")throw new Exception("Xbox boost binding did not roundtrip");
                f.Close();
            }
            string invalid = "<TurboTad><UniversalInput version='99'/></TurboTad>";
            File.WriteAllText(mapping,invalid);
            using(Form f = (Form)Activator.CreateInstance(t,new object[]{root}))
            {
                Call(t,f,"SaveMapping"); f.Close();
                if(File.ReadAllText(mapping)!=invalid)throw new Exception("Unsupported mapping was overwritten");
            }
            Console.WriteLine("Mapper GUI tests passed: legacy/offline migration, backup, keyboard-only Start, digital persistence, neutral T6D7 Stop.");return 0;
        }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
