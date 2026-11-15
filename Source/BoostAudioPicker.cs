using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Media;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace TurboTad
{
    // Standalone audio selection screen. The existing 0.3.15B boost-button
    // timer and audio trigger in Controls.cs remain unmodified.
    internal sealed class BoostAudioPicker : Form
    {
        private const int MaxWavBytes = 20 * 1024 * 1024;
        private readonly string root;
        private readonly Label selectedLabel;
        private readonly Label volumeLabel;
        private readonly TrackBar volume;
        private readonly Button testButton;
        private readonly Button saveButton;
        private byte[] originalWav;
        private string selectedName;
        private SoundPlayer preview;
        private MemoryStream previewStream;

        private string Assets { get { return Path.Combine(root, "Assets"); } }
        private string RawPath { get { return Path.Combine(Assets, "TurboTadCustomSource.wav"); } }
        private string PlaybackPath { get { return Path.Combine(Assets, "TurboTadBoost.wav"); } }
        private string SettingsPath { get { return Path.Combine(root, "audio-settings.xml"); } }

        internal BoostAudioPicker(string modRoot)
        {
            root = modRoot;
            Text = "TurboTad - Boost audio";
            ClientSize = new Size(550, 322);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 10);
            BackColor = Color.FromArgb(24, 26, 29);
            ForeColor = Color.WhiteSmoke;

            Label heading = Label("Boost sound", 18, 12, 510, 30);
            heading.Font = new Font("Segoe UI", 15, FontStyle.Bold);
            heading.ForeColor = Color.FromArgb(255, 143, 40);
            Label drop = Label("Drop a .WAV file here, or click Browse", 18, 49, 514, 72);
            drop.TextAlign = ContentAlignment.MiddleCenter;
            drop.BorderStyle = BorderStyle.FixedSingle;
            drop.BackColor = Color.FromArgb(49, 52, 57);
            drop.AllowDrop = true;
            drop.DragEnter += OnDragEnter;
            drop.DragDrop += OnDragDrop;
            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            selectedLabel = Label("No WAV selected", 18, 132, 379, 28);
            selectedLabel.AutoEllipsis = true;
            Button browse = ActionButton("Browse...", 412, 130, 120, 33);
            browse.Click += delegate { Browse(); };

            Label("Playback volume", 18, 181, 160, 26);
            volume = new TrackBar();
            volume.Minimum = 0;
            volume.Maximum = 100;
            volume.Value = 100;
            volume.TickFrequency = 10;
            volume.SetBounds(172, 172, 296, 46);
            volume.ValueChanged += delegate { volumeLabel.Text = volume.Value.ToString(CultureInfo.InvariantCulture) + "%"; };
            Controls.Add(volume);
            volumeLabel = Label("100%", 472, 180, 60, 26);

            testButton = ActionButton("Test sound", 18, 268, 130, 38);
            testButton.Click += delegate { Test(); };
            Button cancel = ActionButton("Cancel", 288, 268, 112, 38);
            cancel.DialogResult = DialogResult.Cancel;
            saveButton = ActionButton("Save", 416, 268, 116, 38);
            saveButton.Click += delegate { SaveAudio(); };
            CancelButton = cancel;
            AcceptButton = saveButton;

            LoadSettings();
            LoadExistingSound();
            RefreshUi();
        }

        private Label Label(string content, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = content;
            label.SetBounds(x, y, width, height);
            Controls.Add(label);
            return label;
        }

        private Button ActionButton(string content, int x, int y, int width, int height)
        {
            Button button = new Button();
            button.Text = content;
            button.SetBounds(x, y, width, height);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(255, 143, 40);
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = Color.FromArgb(49, 52, 57);
            button.ForeColor = Color.WhiteSmoke;
            Controls.Add(button);
            return button;
        }

        private void LoadSettings()
        {
            if (!File.Exists(SettingsPath)) return;
            try
            {
                XmlReaderSettings options = new XmlReaderSettings();
                options.DtdProcessing = DtdProcessing.Prohibit;
                options.XmlResolver = null;
                options.MaxCharactersInDocument = 16384;
                XmlDocument document = new XmlDocument();
                document.XmlResolver = null;
                using (XmlReader reader = XmlReader.Create(SettingsPath, options)) document.Load(reader);
                XmlElement element = document.DocumentElement;
                if (element == null || element.Name != "TurboTadAudio") return;
                int savedVolume;
                if (int.TryParse(element.GetAttribute("volume"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out savedVolume))
                    volume.Value = Math.Max(0, Math.Min(100, savedVolume));
                selectedName = Path.GetFileName(element.GetAttribute("sourceName"));
            }
            catch { /* Invalid optional audio preference never blocks the flight helper. */ }
        }

        private void LoadExistingSound()
        {
            string path = File.Exists(RawPath) ? RawPath : PlaybackPath;
            if (!File.Exists(path)) return;
            try
            {
                FileInfo file = new FileInfo(path);
                if (file.Length > MaxWavBytes) throw new InvalidDataException("WAV exceeds the 20 MB limit.");
                byte[] bytes = File.ReadAllBytes(path);
                PcmWav.Process(bytes, 100);
                originalWav = bytes;
                if (String.IsNullOrEmpty(selectedName)) selectedName = Path.GetFileName(path);
            }
            catch { originalWav = null; selectedName = null; }
        }

        private void RefreshUi()
        {
            selectedLabel.Text = originalWav == null ? "No compatible WAV selected" : selectedName;
            testButton.Enabled = originalWav != null;
            saveButton.Enabled = originalWav != null;
        }

        private void Browse()
        {
            using (OpenFileDialog picker = new OpenFileDialog())
            {
                picker.Title = "Choose a boost WAV file";
                picker.Filter = "WAV audio (*.wav)|*.wav";
                picker.CheckFileExists = true;
                picker.Multiselect = false;
                if (picker.ShowDialog(this) == DialogResult.OK) SelectFile(picker.FileName);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            e.Effect = files != null && files.Length == 1 &&
                String.Equals(Path.GetExtension(files[0]), ".wav", StringComparison.OrdinalIgnoreCase) ?
                DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length == 1) SelectFile(files[0]);
        }

        private void SelectFile(string path)
        {
            try
            {
                if (!String.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Select a .wav file.");
                FileInfo info = new FileInfo(path);
                if (info.Length == 0 || info.Length > MaxWavBytes)
                    throw new InvalidDataException("The WAV must be between 1 byte and 20 MB.");
                byte[] input = File.ReadAllBytes(path);
                PcmWav.Process(input, 100);
                // Reject unsupported WAV encodings before changing the selected file.
                using (MemoryStream checkStream = new MemoryStream(input, false))
                using (SoundPlayer checkPlayer = new SoundPlayer(checkStream)) checkPlayer.Load();
                StopPreview();
                originalWav = input;
                selectedName = Path.GetFileName(path);
                RefreshUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load that WAV.\r\n\r\n" + ex.Message,
                    "Boost audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Test()
        {
            if (originalWav == null) return;
            try
            {
                StopPreview();
                byte[] sample = PcmWav.Process(originalWav, volume.Value);
                previewStream = new MemoryStream(sample, false);
                preview = new SoundPlayer(previewStream);
                preview.Load();
                preview.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not play this WAV.\r\n\r\n" + ex.Message,
                    "Boost audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StopPreview()
        {
            if (preview != null) { preview.Stop(); preview.Dispose(); preview = null; }
            if (previewStream != null) { previewStream.Dispose(); previewStream = null; }
        }

        private void SaveAudio()
        {
            if (originalWav == null) return;
            string temporaryRoot = Path.Combine(Path.GetTempPath(), "TurboTadAudio_" + Guid.NewGuid().ToString("N"));
            string stagedRaw = Path.Combine(Assets, "TurboTadCustomSource." + Guid.NewGuid().ToString("N") + ".tmp");
            string stagedPlayback = Path.Combine(Assets, "TurboTadBoost." + Guid.NewGuid().ToString("N") + ".tmp");
            string stagedSettings = Path.Combine(root, "audio-settings." + Guid.NewGuid().ToString("N") + ".tmp");
            string[] destinations = { RawPath, PlaybackPath, SettingsPath };
            string[] staged = { stagedRaw, stagedPlayback, stagedSettings };
            bool[] committed = new bool[3];
            bool[] originallyExists = new bool[3];
            try
            {
                StopPreview();
                byte[] adjusted = PcmWav.Process(originalWav, volume.Value);
                using (MemoryStream checkStream = new MemoryStream(adjusted, false))
                using (SoundPlayer checkPlayer = new SoundPlayer(checkStream)) checkPlayer.Load();
                Directory.CreateDirectory(Assets);
                Directory.CreateDirectory(temporaryRoot);
                XmlDocument preferences = new XmlDocument();
                XmlElement audio = preferences.CreateElement("TurboTadAudio");
                audio.SetAttribute("sourceName", selectedName ?? "");
                audio.SetAttribute("volume", volume.Value.ToString(CultureInfo.InvariantCulture));
                preferences.AppendChild(audio);
                File.WriteAllBytes(stagedRaw, originalWav);
                File.WriteAllBytes(stagedPlayback, adjusted);
                preferences.Save(stagedSettings);
                for (int i = 0; i < destinations.Length; i++)
                {
                    originallyExists[i] = File.Exists(destinations[i]);
                    if (originallyExists[i])
                        File.Copy(destinations[i], Path.Combine(temporaryRoot, i.ToString(CultureInfo.InvariantCulture)), true);
                }
                for (int i = 0; i < destinations.Length; i++)
                {
                    if (File.Exists(destinations[i])) File.Replace(staged[i], destinations[i], null);
                    else File.Move(staged[i], destinations[i]);
                    committed[i] = true;
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                for (int i = destinations.Length - 1; i >= 0; i--)
                {
                    if (!committed[i]) continue;
                    try
                    {
                        if (originallyExists[i])
                            File.Copy(Path.Combine(temporaryRoot, i.ToString(CultureInfo.InvariantCulture)), destinations[i], true);
                        else if (File.Exists(destinations[i])) File.Delete(destinations[i]);
                    }
                    catch { /* Preserve the original exception, tell user to restore backup. */ }
                }
                MessageBox.Show(this, "Could not save boost audio.\r\n\r\n" + ex.Message,
                    "Boost audio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                foreach (string temp in staged)
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                try { if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); } catch { }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopPreview();
            base.OnFormClosed(e);
        }
    }

    // Edits only uncompressed integer PCM WAV payloads. SoundPlayer has no per-
    // playback volume API; scaling a private copy of the WAV is independent of
    // the Windows system volume and keeps the established trigger unchanged.
    internal static class PcmWav
    {
        internal static byte[] Process(byte[] input, int volume)
        {
            if (input == null || input.Length < 44 || input.Length > 20 * 1024 * 1024)
                throw new InvalidDataException("WAV must be an uncompressed PCM file smaller than 20 MB.");
            if (volume < 0 || volume > 100) throw new ArgumentOutOfRangeException("volume");
            if (Encoding.ASCII.GetString(input, 0, 4) != "RIFF" ||
                Encoding.ASCII.GetString(input, 8, 4) != "WAVE")
                throw new InvalidDataException("The file must be a standard RIFF/WAVE file.");
            long riffLength = (long)BitConverter.ToUInt32(input, 4) + 8;
            if (riffLength > input.Length || riffLength < 44)
                throw new InvalidDataException("The RIFF/WAVE size is invalid.");
            int format = -1, channels = 0, bits = 0, frameBytes = 0, dataStart = -1, dataLength = 0;
            int pos = 12;
            while (pos + 8 <= riffLength)
            {
                string name = Encoding.ASCII.GetString(input, pos, 4);
                long size = BitConverter.ToUInt32(input, pos + 4);
                long end = (long)pos + 8 + size;
                if (end > riffLength) throw new InvalidDataException("The WAV contains a truncated chunk.");
                if (name == "fmt " && size >= 16 && format < 0)
                {
                    format = BitConverter.ToUInt16(input, pos + 8);
                    channels = BitConverter.ToUInt16(input, pos + 10);
                    bits = BitConverter.ToUInt16(input, pos + 22);
                    frameBytes = BitConverter.ToUInt16(input, pos + 20);
                    int sampleRate = BitConverter.ToInt32(input, pos + 12);
                    if (format != 1 || channels < 1 || channels > 8 ||
                        (bits != 8 && bits != 16 && bits != 24 && bits != 32) ||
                        frameBytes != channels * (bits / 8) || sampleRate < 8000 || sampleRate > 192000)
                        throw new InvalidDataException("Use an uncompressed PCM WAV (8/16/24/32-bit, 8-192 kHz). Re-export compressed or floating-point WAV files as PCM 16-bit.");
                }
                else if (name == "data" && dataStart < 0)
                {
                    dataStart = pos + 8;
                    dataLength = (int)size;
                }
                pos = (int)(end + (size & 1L));
            }
            if (format != 1 || dataStart < 0 || dataLength == 0 || frameBytes == 0 ||
                dataLength % frameBytes != 0)
                throw new InvalidDataException("The WAV is missing valid PCM audio data.");
            byte[] output = (byte[])input.Clone();
            if (volume == 100) return output;
            double gain = volume / 100.0;
            int step = bits / 8;
            for (int offset = dataStart; offset < dataStart + dataLength; offset += step)
            {
                if (bits == 8)
                {
                    double sample = 128 + (input[offset] - 128) * gain;
                    output[offset] = (byte)Math.Max(0, Math.Min(255, Math.Round(sample)));
                }
                else if (bits == 16)
                {
                    double sample = BitConverter.ToInt16(input, offset) * gain;
                    short scaled = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(sample)));
                    byte[] b = BitConverter.GetBytes(scaled);
                    output[offset] = b[0]; output[offset + 1] = b[1];
                }
                else if (bits == 24)
                {
                    int sample = input[offset] | (input[offset + 1] << 8) | (input[offset + 2] << 16);
                    if ((sample & 0x800000) != 0) sample |= unchecked((int)0xFF000000);
                    double scaledValue = Math.Max(-8388608.0, Math.Min(8388607.0, Math.Round(sample * gain)));
                    int scaled = (int)scaledValue;
                    output[offset] = (byte)(scaled & 255);
                    output[offset + 1] = (byte)((scaled >> 8) & 255);
                    output[offset + 2] = (byte)((scaled >> 16) & 255);
                }
                else
                {
                    double sample = BitConverter.ToInt32(input, offset) * gain;
                    int scaled = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Math.Round(sample)));
                    byte[] b = BitConverter.GetBytes(scaled);
                    Buffer.BlockCopy(b, 0, output, offset, 4);
                }
            }
            return output;
        }
    }
}
