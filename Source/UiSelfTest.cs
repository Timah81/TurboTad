using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Runs ONLY against a temporary staged mod directory. Never runs the game or
// reads/writes the user's actual mapping.xml, audio settings, scripts or saves.
internal static class UiSelfTest
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object Field(Type type, object instance, string name)
    {
        FieldInfo info = type.GetField(name, PrivateInstance);
        if (info == null) throw new Exception("Missing GUI binding: " + name);
        return info.GetValue(instance);
    }

    private static void AssertNoLabelContains(Control parent, string unwanted)
    {
        foreach (Control control in parent.Controls)
        {
            Label label = control as Label;
            if (label != null && label.Text.IndexOf(unwanted, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new Exception("Obsolete GUI copy remains: " + unwanted);
            AssertNoLabelContains(control, unwanted);
        }
    }

    private static Label FindLabel(Control parent, string exactText)
    {
        foreach (Control control in parent.Controls)
        {
            Label label = control as Label;
            if (label != null && label.Text == exactText) return label;
        }
        throw new Exception("Missing GUI heading: " + exactText);
    }

    // All visible numeric editors must permit intermediate typing without
    // validation on every live .Value read. Top-speed percent is now a
    // visible editor alongside estimated mph and must be counted too.
    private static int AssertDeferredEditors(Control parent)
    {
        int count = 0;
        foreach (Control control in parent.Controls)
        {
            if (control is NumericUpDown)
            {
                if (control.GetType().Name != "DeferredNumericUpDown")
                    throw new Exception("Numeric editor still validates during typing: " + control.Name);
                count++;
            }
            count += AssertDeferredEditors(control);
        }
        return count;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new Exception("Usage: UiSelfTest.exe AppExe TempModRoot");
            string stagedExe = Path.GetFullPath(args[0]);
            string modRoot = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(modRoot);
            Assembly assembly = Assembly.LoadFrom(stagedExe);
            Type formType = assembly.GetType("TurboTad.ControlsWindow", true);
            Type pickerType = assembly.GetType("TurboTad.BoostAudioPicker", true);
            // Inspect the standalone WAV dialog, not just the parent tab page.
            using (Form picker = (Form)Activator.CreateInstance(pickerType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { modRoot }, null))
            {
                AssertNoLabelContains(picker, "Choose, preview, then Save");
                AssertNoLabelContains(picker, "The setting is remembered");
                AssertNoLabelContains(picker, "boost timing is unchanged");
                FindLabel(picker, "Boost sound");
                FindLabel(picker, "Playback volume");
                bool hasTest = false;
                bool hasSave = false;
                foreach (Control control in picker.Controls)
                {
                    Button button = control as Button;
                    if (button == null) continue;
                    if (button.Text == "Test sound") hasTest = true;
                    if (button.Text == "Save") hasSave = true;
                }
                if (!hasTest || !hasSave)
                    throw new Exception("WAV picker audio actions missing");
            }
            using (Form form = (Form)Activator.CreateInstance(formType, new object[] { modRoot }))
            {
                TabControl tabs = (TabControl)Field(formType, form, "dashboardTabs");
                if (tabs.TabPages.Count != 3) throw new Exception("Expected 3 GUI tabs");
                if (tabs.TabPages[2].Text != "BOOST HUD") throw new Exception("Boost HUD tab label is incorrect");
                // The device/axis hint is shown in the footer directly beside ABOUT.
                // A long status line must never occupy the button's hit/paint area.
                Label footerPrompt = (Label)Field(formType, form, "prompt");
                Label footerStatus = (Label)Field(formType, form, "status");
                Control footer = footerPrompt.Parent;
                if (footer == null || footerStatus.Parent != footer)
                    throw new Exception("Footer prompt/status parent missing");
                Button aboutButton = null;
                foreach (Control child in footer.Controls)
                {
                    Button button = child as Button;
                    if (button != null && button.Text == "ABOUT") { aboutButton = button; break; }
                }
                if (aboutButton == null) throw new Exception("ABOUT button missing from footer");
                if (footerPrompt.Right > aboutButton.Left - 12)
                    throw new Exception("Device prompt overlaps the ABOUT button: right=" +
                        footerPrompt.Right + "; ABOUT left=" + aboutButton.Left);
                if (footerStatus.Right > aboutButton.Left - 12)
                    throw new Exception("Footer status overlaps the ABOUT button: right=" +
                        footerStatus.Right + "; ABOUT left=" + aboutButton.Left);
                if (!footerPrompt.AutoEllipsis)
                    throw new Exception("Long joystick status messages are not clipped to the footer");
                // Ensure the native TabControl has computed its selected page's real bounds.
                // Reading TabPage.ClientSize before handles exist can report stale/default sizes.
                IntPtr formHandle = form.Handle;
                IntPtr tabsHandle = tabs.Handle;
                IntPtr pageHandle = tabs.TabPages[0].Handle;
                form.PerformLayout();
                tabs.PerformLayout();
                tabs.TabPages[0].PerformLayout();
                NumericUpDown[] rotation = (NumericUpDown[])Field(formType, form, "rotationRates");
                NumericUpDown[] movement = (NumericUpDown[])Field(formType, form, "movementRates");
                NumericUpDown savedSpeed = (NumericUpDown)Field(formType, form, "topSpeed");
                NumericUpDown mph = (NumericUpDown)Field(formType, form, "mphInput");
                int deferredEditors = AssertDeferredEditors(form);
                if (deferredEditors != 10)
                    throw new Exception("Expected 10 visible delayed-commit editors, got " + deferredEditors);
                if (rotation[0].Minimum != 10 || movement[0].Minimum != 10 || mph.Minimum <= 0)
                    throw new Exception("A real numeric range was changed just to permit typing");
                // Cosmetic cleanup: no stale explanatory subheadings, even on other tabs.
                AssertNoLabelContains(form, "No calibrated °/s available");
                AssertNoLabelContains(form, "Estimated mph from the observed");
                AssertNoLabelContains(form, "The game still validates each boost command");
                AssertNoLabelContains(form, "Optional boost indicators only");
                AssertNoLabelContains(form, "Sliders and numbers edit the same saved settings");
                AssertNoLabelContains(form, "Rotation values are game-input multipliers");
                AssertNoLabelContains(form, "Uses the existing joystick edge trigger");
                AssertNoLabelContains(form, "Sound changes do not modify flight controls");
                AssertNoLabelContains(form, "Fine-tune the boost bars'");
                Label hudCaption = null;
                foreach (Control c in tabs.TabPages[2].Controls)
                {
                    foreach (Control child in c.Controls)
                    {
                        Label candidate = child as Label;
                        if (candidate != null && candidate.Text == "Boost bar placement") hudCaption = candidate;
                    }
                }
                if (hudCaption == null) throw new Exception("Boost bar placement caption missing");
                if (mph.Height < 20 || mph.Width < 90)
                    throw new Exception("Speed editor dimensions are unexpectedly small");
                Control speedCard = mph.Parent;
                if (savedSpeed.Parent != speedCard || savedSpeed.Increment != 1)
                    throw new Exception("Editable 1% top-speed percentage box not visible beside mph");
                if (savedSpeed.Right > mph.Left || savedSpeed.Top != mph.Top)
                    throw new Exception("Top-speed percentage overlaps mph or is not aligned");
                Control speedSlider = null;
                foreach (Control control in speedCard.Controls)
                    if (control.GetType().Name == "RateSlider") { speedSlider = control; break; }
                if (speedSlider == null) throw new Exception("Top speed slider missing");
                Label speedHeading = FindLabel(speedCard, "NORMAL TOP SPEED");
                if (speedSlider.Top < speedHeading.Bottom + 10 || mph.Top < speedHeading.Bottom + 10)
                    throw new Exception("Top speed controls overlap the section heading");
                Label mphUnit = FindLabel(speedCard, "est. mph");
                if (speedSlider.Bottom > speedCard.ClientSize.Height - 8 ||
                    mph.Bottom > speedCard.ClientSize.Height - 8 ||
                    mphUnit.Bottom > speedCard.ClientSize.Height - 8)
                    throw new Exception("Top speed controls are cut off by the card");
                if (mphUnit.Top < mph.Bottom)
                    throw new Exception("Top speed unit label overlaps the mph editor");
                if (speedSlider.Right > mph.Left - 16)
                    throw new Exception("Slider and mph editor overlap horizontally");
                int availableHeight = tabs.TabPages[0].ClientSize.Height;
                Console.WriteLine("Flight tab viewport height: " + availableHeight +
                    "; top-speed card bottom: " + speedCard.Bottom +
                    "; card height: " + speedCard.Height);
                if (availableHeight < 300)
                    throw new Exception("Flight tab viewport did not initialize; measured " + availableHeight + " pixels");
                if (speedCard.Bottom > availableHeight - 10)
                    throw new Exception("Top speed card is cut off by the tab page: bottom " +
                        speedCard.Bottom + ", viewport " + availableHeight);
                if (rotation[0].Value != 100 || movement[0].Value != 100 || savedSpeed.Value != 100)
                    throw new Exception("Incorrect defaults");
                rotation[0].Value = 367M;
                if (rotation[0].Value != 367M) throw new Exception("Typed rotation value changed unexpectedly");
                savedSpeed.Value = 400M;
                if (mph.Value < 89M || mph.Value > 90M) throw new Exception("mph display not linked to saved percent");
                mph.Value = 22.4M;
                if (savedSpeed.Value != 100M) throw new Exception("mph input not linked back to speed percent");
                form.Close();
            }
            Console.WriteLine("GUI test passed: obsolete subtitles removed, speed layout, WAV picker, numeric entry and speed synchronization.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("GUI smoke test failed: " + e);
            return 1;
        }
    }
}
