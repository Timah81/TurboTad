using System;
using System.IO;
using System.Text;

namespace TurboTad
{
    internal static class AudioSelfTest
    {
        public static int Main()
        {
            try
            {
                byte[] wav = MakeWav();
                byte[] untouched = PcmWav.Process(wav, 100);
                if (!Equal(wav, untouched)) throw new InvalidDataException("100% audio changed the WAV.");
                byte[] half = PcmWav.Process(wav, 50);
                if (BitConverter.ToInt16(half, 44) != 500 ||
                    BitConverter.ToInt16(half, 46) != -500 ||
                    BitConverter.ToInt16(half, 48) != 0 ||
                    BitConverter.ToInt16(half, 50) != 1000)
                    throw new InvalidDataException("PCM 16-bit volume scaling is incorrect.");
                if (BitConverter.ToInt16(wav, 44) != 1000)
                    throw new InvalidDataException("Input WAV modified in place.");
                bool invalidRejected = false;
                try { PcmWav.Process(new byte[] { 1, 2, 3 }, 50); }
                catch (InvalidDataException) { invalidRejected = true; }
                if (!invalidRejected) throw new InvalidDataException("Malformed WAV not rejected.");
                // 24-bit, 48-kHz stereo export using WAVE_FORMAT_EXTENSIBLE:
                // the previous parser rejected this common uncompressed PCM header.
                byte[] pcm24 = MakeExtensible24();
                byte[] normalized = PcmWav.Process(pcm24, 100);
                if (normalized.Length != 52 || BitConverter.ToUInt16(normalized, 20) != 1 ||
                    BitConverter.ToUInt16(normalized, 22) != 2 ||
                    BitConverter.ToUInt32(normalized, 24) != 48000 ||
                    BitConverter.ToUInt16(normalized, 34) != 16 ||
                    BitConverter.ToInt16(normalized, 44) != 16384 ||
                    BitConverter.ToInt16(normalized, 46) != -16384 ||
                    BitConverter.ToInt16(normalized, 48) != 0 ||
                    BitConverter.ToInt16(normalized, 50) != 32767)
                    throw new InvalidDataException("Extensible PCM24 was not normalized to playable stereo PCM16.");
                byte[] quiet = PcmWav.Process(pcm24, 50);
                if (BitConverter.ToInt16(quiet, 44) != 8192 ||
                    BitConverter.ToInt16(quiet, 46) != -8192 ||
                    BitConverter.ToInt16(quiet, 50) != 16384)
                    throw new InvalidDataException("Extensible PCM24 volume scaling is incorrect.");
                if (pcm24[44] != 1) throw new InvalidDataException("Original 24-bit WAV was changed.");
                using (MemoryStream soundStream = new MemoryStream(normalized, false))
                using (System.Media.SoundPlayer sound = new System.Media.SoundPlayer(soundStream))
                    sound.Load();
                byte[] floatingPointSubtype = (byte[])pcm24.Clone();
                floatingPointSubtype[44] = 3; // IEEE_FLOAT subtype, not integer PCM
                bool unsafeAccepted = false;
                try { PcmWav.Process(floatingPointSubtype, 100); }
                catch (InvalidDataException) { unsafeAccepted = true; }
                if (!unsafeAccepted) throw new InvalidDataException("Non-PCM extensible subtype was accepted.");
                Console.WriteLine("TurboTad WAV parser and volume smoke test passed.");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("TurboTad WAV test failed: " + error.Message);
                return 1;
            }
        }

        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static byte[] MakeExtensible24()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(72U);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(40U); // WAVEFORMATEXTENSIBLE: 18 + 22 bytes
                writer.Write((ushort)0xFFFE); // extensible format tag
                writer.Write((ushort)2); // stereo
                writer.Write(48000U);
                writer.Write(288000U); // 48,000 frames/sec * 6 bytes/frame
                writer.Write((ushort)6);
                writer.Write((ushort)24);
                writer.Write((ushort)22); // extensible extension size
                writer.Write((ushort)24); // valid bits per sample
                writer.Write(3U); // front left and right speakers
                writer.Write(new byte[] {
                    0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00,
                    0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71
                });
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(12U);
                writer.Write(new byte[] { 0, 0, 0x40, 0, 0, 0xC0, 0, 0, 0, 0xFF, 0xFF, 0x7F });
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] MakeWav()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(44U);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16U);
                writer.Write((ushort)1);
                writer.Write((ushort)1);
                writer.Write(8000U);
                writer.Write(16000U);
                writer.Write((ushort)2);
                writer.Write((ushort)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(8U);
                writer.Write((short)1000);
                writer.Write((short)-1000);
                writer.Write((short)0);
                writer.Write((short)2000);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
