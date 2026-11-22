using System;
using System.Windows.Forms;

namespace TurboTad
{
    public sealed partial class ControlsWindow
    {
        private InputDiscoveryCatalog discovered = new InputDiscoveryCatalog();

        private void BeginDeviceDiscovery()
        {
            StopControls();
            timer.Stop();
            try
            {
                using (DeviceDiscoveryDialog dialog = new DeviceDiscoveryDialog(devices,discovered))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    int newlyAdded = 0;
                    foreach (InputSource source in dialog.Detected.Sources)
                        if (discovered.Add(source)) newlyAdded++;
                    if (newlyAdded == 0)
                    {
                        prompt.Text = "No new controls recorded. Click START SCAN, then move and press your controls.";
                        return;
                    }
                    try { discovered.Save(root); }
                    catch (Exception e)
                    {
                        MessageBox.Show(this, "The scan was applied for this session but could not be saved:\r\n" + e.Message,
                            "Device discovery", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    SaveMapping();
                    Scan();
                    LoadMapping();
                    prompt.Text = newlyAdded + " controls discovered. They now appear first in the binding lists.";
                }
            }
            finally { timer.Start(); }
        }

        // The scanner supplies an ordered list. Promoting discovered items,
        // instead of deleting the full interface-based catalog, prevents an
        // accidental missed button from becoming unbindable. The actual mapped
        // InputSource identity never changes; this is presentation only.
        private void PromoteDiscoveredOptions()
        {
            if (discovered.Count == 0) return;

            foreach (InputSource source in discovered.OfType(true))
            {
                int player;
                if (source.Kind == "xaxis" && InputSource.TryXboxPlayer(source.Device, out player))
                    EnsureXboxAxisChoices(player);
            }

            foreach (ComboBox combo in choices)
            {
                int insertAt = 1; // index 0 is always the unassigned placeholder
                foreach (InputSource source in discovered.OfType(true))
                {
                    AxisChoice match = null;
                    foreach (AxisChoice candidate in combo.Items)
                    {
                        if (source.Kind == "xaxis")
                        {
                            if (candidate.Xbox != null && candidate.Xbox.Identity == source.Identity)
                            { match = candidate; break; }
                        }
                        else if (candidate.Device != null && candidate.Axis == source.Code &&
                                 candidate.Device.Identity == source.Device)
                        { match = candidate; break; }
                    }
                    if (match == null) continue; // hardware not currently exposed
                    combo.Items.Remove(match);
                    combo.Items.Insert(insertAt++, match);
                }
            }

            // WinForms drops ComboBox.SelectedItem if the selected object is
            // temporarily removed while prioritizing discovered buttons.
            // Preserve it across reorder so Save Profile doesn't serialize
            // an unintended unassigned Boost source.
            ButtonChoice selectedBoost = boostChoice.SelectedItem as ButtonChoice;
            int buttonInsertAt = 1;
            foreach (InputSource source in discovered.OfType(false))
            {
                ButtonChoice match = null;
                foreach (ButtonChoice candidate in boostChoice.Items)
                {
                    InputSource current = candidate.Source;
                    if (current != null && current.Identity == source.Identity)
                    { match = candidate; break; }
                }
                if (match == null && source.Kind == "key")
                    match = new ButtonChoice { Key = source.Code };
                else if (match == null && source.Kind == "xbutton")
                    match = new ButtonChoice { Xbox = source };
                if (match == null) continue; // unavailable WinMM device
                boostChoice.Items.Remove(match);
                boostChoice.Items.Insert(buttonInsertAt++, match);
            }
            if (selectedBoost != null && boostChoice.Items.Contains(selectedBoost))
                boostChoice.SelectedItem = selectedBoost;
        }
    }
}
