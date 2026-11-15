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
