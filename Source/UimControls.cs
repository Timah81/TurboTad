using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace TurboTad
{
    public sealed partial class ControlsWindow
    {
        private AxisBinding[] CurrentBindings()
        {
            AxisBinding[] result = new AxisBinding[6];
            for(int i=0;i<6;i++)
            {
                if(inputModes[i].SelectedIndex==1) result[i]=directionPairs[i];
                else
                {
                    AxisChoice a=choices[i].SelectedItem as AxisChoice;
                    InputSource source = a == null ? null : a.Xbox != null ? a.Xbox :
                        a.Device == null ? null : InputSource.Joystick(a.Device.Identity,a.Axis,true);
                    result[i]=new AxisBinding { Analog=source, Invert=invert[i].Checked };
                }
            }
            return result;
        }
        private void RefreshInputRow(int row)
        {
            bool digital=inputModes[row].SelectedIndex==1;
            choices[row].Visible=!digital;pairButtons[row].Visible=digital;
            invert[row].Enabled=!digital;learnButtons[row].Text=digital?"SETUP":"LEARN";
            AxisBinding pair=directionPairs[row];
            pairButtons[row].Text=InputNames.Describe(pair.Negative)+" / "+InputNames.Describe(pair.Positive);
        }
        private void EditDirectionPair(int row)
        {
            StopControls(); learning=-1;
            using(DirectionPairDialog dialog=new DirectionPairDialog(labels[row],negativeNames[row],positiveNames[row],directionPairs[row],devices,discovered))
                if(dialog.ShowDialog(this)==DialogResult.OK)
                { directionPairs[row]=dialog.Binding;RefreshInputRow(row); }
        }
        private Device FindDevice(string identity)
        {
            foreach(Device d in devices) if(d.Identity==identity)return d;
            return null;
        }
        private Device OfflineDevice(string identity)
        {
            Device online=FindDevice(identity);if(online!=null)return online;
            string[] parts=identity.Split(new char[]{':'},4);uint id;ushort mid,pid;
            if(parts.Length!=4 || !UInt32.TryParse(parts[0],out id)||id>=16 ||
                !UInt16.TryParse(parts[1],out mid)||!UInt16.TryParse(parts[2],out pid))
                throw new FormatException("Invalid stored joystick identity");
            return new Device { Id=id,Caps=new Native.Caps { Mid=mid,Pid=pid,Name=parts[3] },Connected=false };
        }
        private void SelectAnalogSource(int row,InputSource source,bool inverted)
        {
            // A saved Xbox binding may load while the pad is asleep. Catalog
            // all six analog axes for that player in every flight dropdown.
            int xboxPlayer;
            if(source!=null && source.Kind=="xaxis" &&
                InputSource.TryXboxPlayer(source.Device,out xboxPlayer))
                EnsureXboxAxisChoices(xboxPlayer);
            choices[row].SelectedIndex=0;
            if(source!=null)
            {
                AxisChoice found=null;
                foreach(AxisChoice candidate in choices[row].Items)
                    if(source.Kind=="xaxis" ? candidate.Xbox!=null && candidate.Xbox.Identity==source.Identity :
                        candidate.Device!=null && candidate.Device.Identity==source.Device && candidate.Axis==source.Code)
                    {found=candidate;break;}
                if(found==null)
                {
                    found=source.Kind=="xaxis"?new AxisChoice{Xbox=source}:
                        new AxisChoice{Device=OfflineDevice(source.Device),Axis=source.Code};
                    choices[row].Items.Add(found);
                }
                choices[row].SelectedItem=found;
            }
            invert[row].Checked=inverted;
        }
        private void SelectBoostSource(InputSource source)
        {
            boostChoice.SelectedIndex=0;if(source==null)return;
            foreach(ButtonChoice b in boostChoice.Items)
                if(b.Source!=null && b.Source.Identity==source.Identity){boostChoice.SelectedItem=b;return;}
            ButtonChoice next=source.Kind=="key"?new ButtonChoice{Key=source.Code}:
                source.Kind=="xbutton"?new ButtonChoice{Xbox=source}:
                new ButtonChoice{Device=OfflineDevice(source.Device),Button=source.Code};
            boostChoice.Items.Add(next);boostChoice.SelectedItem=next;
        }
        private sealed class HelperInputFrame : IInputFrame
        {
            private readonly ControlsWindow owner;
            internal HelperInputFrame(ControlsWindow window){owner=window;}
            public bool TryAxis(InputSource source,out double value)
            {
                value=0;
                if(source.Kind=="xaxis")
                {
                    XInputProbe.State state;
                    if(!owner.TryXboxState(source.Device,out state))return false;
                    value=XInputProbe.AxisValue(state,source.Code);
                    return true;
                }
                Device d=owner.FindDevice(source.Device);
                if(d==null||!d.Connected||!d.Supports(source.Code))return false;
                value=d.Values[source.Code];return true;
            }
            public bool TryButton(InputSource source,out bool down)
            {
                down=false;
                // Game-side keyboard values are deliberately neutral in the helper.
                // This leaves only the non-keyboard contribution of a digital pair.
                if(source.Kind=="key") return GameKeyboard.Name(source.Code)!=null;
                else if(source.Kind=="xbutton")
                {
                    XInputProbe.State state;
                    if(!owner.TryXboxState(source.Device,out state))return false;
                    down=XInputProbe.ButtonDown(state,source.Code);
                }
                else
                {
                    Device d=owner.FindDevice(source.Device);if(d==null||!d.Connected || source.Code>=Math.Min(d.Caps.Buttons,32U))return false;
                    down=(d.Buttons&(1U<<source.Code))!=0;
                }
                if(owner.filterDigital)down=owner.digitalFocus.Filter(source.Identity,down,owner.inputForeground);
                return true;
            }
        }
        private XmlElement WriteUniversalBindings(XmlDocument doc)
        {
            XmlElement element=doc.CreateElement("UniversalInput");element.SetAttribute("version","1");
            AxisBinding[] bindings=CurrentBindings();
            for(int row=0;row<6;row++)element.AppendChild(InputMapper.WriteAxis(doc,row,bindings[row]));
            ButtonChoice boost=boostChoice.SelectedItem as ButtonChoice;
            element.AppendChild(InputMapper.WriteSource(doc,"Boost",boost==null?null:boost.Source));
            return element;
        }
        private void LoadUniversalBindings(XmlElement element)
        {
            ApplyUniversalBindings(element, false);
        }
        private void LoadStoredUniversalBindings(XmlElement element)
        {
            ApplyUniversalBindings(element, true);
        }
        private void ApplyUniversalBindings(XmlElement element, bool allowEditableConflicts)
        {
            if(element==null)return;
            if(element.GetAttribute("version")!="1")throw new FormatException("Unsupported input profile version");
            AxisBinding[] parsed=new AxisBinding[6];
            foreach(XmlElement item in element.SelectNodes("Binding"))
            {
                int row;
                if(!Int32.TryParse(item.GetAttribute("row"),out row)||row<0||row>5||parsed[row]!=null)
                    throw new FormatException("Invalid or duplicate axis row");
                parsed[row]=InputMapper.ReadAxis(item);
                foreach(InputSource source in parsed[row].Sources)
                    if(source.Kind=="axis"||source.Kind=="button")OfflineDevice(source.Device); // Validate before touching UI.
            }
            for(int i=0;i<6;i++)if(parsed[i]==null)throw new FormatException("Input profile must contain all six axes");
            InputSource boost=InputMapper.ReadSource(element["Boost"],false);
            if(boost!=null&&(boost.Kind=="axis"||boost.Kind=="button"))OfflineDevice(boost.Device);
            string conflict;
            bool valid = InputMapper.ValidateSources(parsed, boost, out conflict);
            if (!valid && !allowEditableConflicts) throw new FormatException(conflict);
            for(int i=0;i<6;i++)
            {
                inputModes[i].SelectedIndex=parsed[i].Digital?1:0;
                if(parsed[i].Digital)directionPairs[i]=parsed[i];
                else SelectAnalogSource(i,parsed[i].Analog,parsed[i].Invert);
                RefreshInputRow(i);
            }
            SelectBoostSource(boost);
            if (!valid) prompt.Text = "Fix mapping: " + conflict + ".";
        }
        private void LoadLegacyBindings(XmlElement top)
        {
            XmlElement boost=top["BoostButton"];
            if(boost!=null)
            {
                uint id;ushort mid,pid;int b;
                if(!UInt32.TryParse(boost.GetAttribute("id"),out id)||!UInt16.TryParse(boost.GetAttribute("mid"),out mid)||
                    !UInt16.TryParse(boost.GetAttribute("pid"),out pid)||!Int32.TryParse(boost.GetAttribute("button"),out b)||b<0||b>=32)
                    throw new FormatException("Invalid saved boost button");
                SelectBoostSource(InputSource.Joystick(id+":"+mid+":"+pid+":"+boost.GetAttribute("name"),b,false));
            }
            foreach(XmlElement item in top.SelectNodes("Axis"))
            {
                int row,axis;uint id;ushort mid,pid;
                if(!Int32.TryParse(item.GetAttribute("row"),out row)||row<0||row>5||!Int32.TryParse(item.GetAttribute("axis"),out axis)||axis<0||axis>5||
                    !UInt32.TryParse(item.GetAttribute("id"),out id)||!UInt16.TryParse(item.GetAttribute("mid"),out mid)||!UInt16.TryParse(item.GetAttribute("pid"),out pid))
                    throw new FormatException("Invalid saved analog axis");
                SelectAnalogSource(row,InputSource.Joystick(id+":"+mid+":"+pid+":"+item.GetAttribute("name"),axis,true),item.GetAttribute("invert")=="1");
            }
        }

        private void ShowActiveInputProfile()
        {
            if (activeProfileLabel == null) return;
            activeProfileLabel.Text = activeInputProfilePath == null
                ? "Active input profile: none"
                : "Active input profile: " + Path.GetFileName(activeInputProfilePath);
            activeProfileLabel.AutoEllipsis = true;
        }

        // FlightTuning is an optional extension. Legacy input-only profiles remain valid.
        private XmlElement WriteProfileFlightTuning(XmlDocument doc)
        {
            XmlElement tuning = doc.CreateElement("FlightTuning");
            tuning.SetAttribute("version", "1");
            tuning.SetAttribute("topSpeed", topSpeed.Value.ToString(CultureInfo.InvariantCulture));
            string[] rotationKeys = { "pitchRate", "yawRate", "rollRate" };
            string[] movementKeys = { "forwardRate", "strafeRate", "verticalRate" };
            for (int i = 0; i < 3; i++)
            {
                tuning.SetAttribute(rotationKeys[i], rotationRates[i].Value.ToString(CultureInfo.InvariantCulture));
                tuning.SetAttribute(movementKeys[i], movementRates[i].Value.ToString(CultureInfo.InvariantCulture));
                tuning.SetAttribute(RotationGainKeys[i], rotationGains[i].Value.ToString(CultureInfo.InvariantCulture));
            }
            return tuning;
        }

        private static decimal ReadTuningValue(XmlElement tuning, string name, decimal minimum, decimal maximum)
        {
            decimal value;
            if (!Decimal.TryParse(tuning.GetAttribute(name), NumberStyles.Number,
                CultureInfo.InvariantCulture, out value) ||
                value < minimum || value > maximum || Decimal.Truncate(value) != value)
                throw new FormatException("Invalid profile flight tuning: " + name);
            return value;
        }

        // Parse/validate every value before changing any current GUI settings.
        // Order: top speed; three angular rates; three translation gains; three rotational gains.
        private decimal[] ReadProfileFlightTuning(XmlElement tuning)
        {
            if (tuning == null) return null;
            if (tuning.GetAttribute("version") != "1")
                throw new FormatException("Unsupported flight tuning profile version");
            decimal[] values = new decimal[10];
            values[0] = ReadTuningValue(tuning, "topSpeed", topSpeed.Minimum, topSpeed.Maximum);
            string[] rotationKeys = { "pitchRate", "yawRate", "rollRate" };
            string[] movementKeys = { "forwardRate", "strafeRate", "verticalRate" };
            for (int i = 0; i < 3; i++)
            {
                values[i + 1] = ReadTuningValue(tuning, rotationKeys[i],
                    rotationRates[i].Minimum, rotationRates[i].Maximum);
                values[i + 4] = ReadTuningValue(tuning, movementKeys[i],
                    movementRates[i].Minimum, movementRates[i].Maximum);
                values[i + 7] = tuning.HasAttribute(RotationGainKeys[i])
                    ? ReadTuningValue(tuning, RotationGainKeys[i], rotationGains[i].Minimum, rotationGains[i].Maximum)
                    : DefaultRotationGain;
            }
            return values;
        }

        private void ApplyProfileFlightTuning(decimal[] values)
        {
            if (values == null) return; // Input-only profile: retain existing flight tuning.
            topSpeed.Value = values[0];
            for (int i = 0; i < 3; i++)
            {
                rotationRates[i].Value = values[i + 1];
                movementRates[i].Value = values[i + 4];
                rotationGains[i].Value = values[i + 7];
            }
        }

        // Save input bindings and, only if opted in, flight tuning to the selected profile.
        // A temp write + replace prevents a failed save from leaving a truncated profile.
        private void SaveInputProfileTo(string destination)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement top = doc.CreateElement("TurboTadInputProfile");
            doc.AppendChild(top);
            top.AppendChild(WriteUniversalBindings(doc));
            if (includeFlightTuningInProfile.Checked)
                top.AppendChild(WriteProfileFlightTuning(doc));
            string temp = Path.Combine(Path.GetDirectoryName(destination),
                "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                doc.Save(temp);
                if (File.Exists(destination)) File.Replace(temp, destination, null);
                else File.Move(temp, destination);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private void ExportInputProfile()
        {
            StopControls(); learning = -1;
            if (String.IsNullOrEmpty(activeInputProfilePath))
            {
                ExportNewInputProfile();
                return;
            }
            try
            {
                SaveInputProfileTo(activeInputProfilePath);
                prompt.Text = "Updated input profile: " + Path.GetFileName(activeInputProfilePath);
                ShowActiveInputProfile();
            }
            catch (Exception e) { prompt.Text = "Profile update failed: " + e.Message; }
        }

        private void ExportNewInputProfile()
        {
            StopControls(); learning = -1;
            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "TurboTad input profile (*.xml)|*.xml",
                FileName = "TurboTad-input-profile.xml",
                OverwritePrompt = true
            })
            {
                if (!String.IsNullOrEmpty(activeInputProfilePath))
                    dialog.InitialDirectory = Path.GetDirectoryName(activeInputProfilePath);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    SaveInputProfileTo(dialog.FileName);
                    activeInputProfilePath = dialog.FileName;
                    ShowActiveInputProfile();
                    prompt.Text = "Input profile saved: " + Path.GetFileName(dialog.FileName);
                }
                catch (Exception e) { prompt.Text = "Profile save failed: " + e.Message; }
            }
        }

        private void ImportInputProfile()
        {
            StopControls(); learning=-1;
            using(OpenFileDialog dialog=new OpenFileDialog { Filter="TurboTad input profile (*.xml)|*.xml" })
                if(dialog.ShowDialog(this)==DialogResult.OK)
                    try
                    {
                        XmlReaderSettings settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536};
                        XmlDocument doc=new XmlDocument();doc.XmlResolver=null;
                        using(XmlReader reader=XmlReader.Create(dialog.FileName,settings))doc.Load(reader);
                        if(doc.DocumentElement.Name!="TurboTadInputProfile"||doc.DocumentElement["UniversalInput"]==null)throw new FormatException("Not a TurboTad input profile");
                        // Invalid optional tuning must not change bindings or tuning.
                        decimal[] loadedFlightTuning = ReadProfileFlightTuning(doc.DocumentElement["FlightTuning"]);
                        LoadUniversalBindings(doc.DocumentElement["UniversalInput"]);
                        ApplyProfileFlightTuning(loadedFlightTuning);
                        includeFlightTuningInProfile.Checked = loadedFlightTuning != null;
                        mappingLoadError=null;SaveMapping();
                        activeInputProfilePath = dialog.FileName;
                        ShowActiveInputProfile();
                        prompt.Text="Input profile loaded: "+Path.GetFileName(dialog.FileName)+". Center controls, then Start.";
                    }
                    catch(Exception e){prompt.Text="Profile not loaded: "+e.Message;}
        }
    }
}
