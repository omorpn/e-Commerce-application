using System.IO.Compression;
using System.Text;

namespace e_Commerce_application.Services
{
    // Small generated files so seeded digital listings have something real to download.
    public static class SampleFiles
    {
        public static byte[] Zip(params (string Name, string Content)[] entries)
        {
            using var buffer = new MemoryStream();
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, content) in entries)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8);
                    writer.Write(content);
                }
            }
            return buffer.ToArray();
        }

        // A mono 16-bit PCM WAV that fades a sine tone out, like a bell.
        public static byte[] Tone(double frequency, double seconds, int sampleRate = 22050)
        {
            var samples = (int)(sampleRate * seconds);
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer);
            writer.Write("RIFF"u8);
            writer.Write(36 + samples * 2);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(samples * 2);
            for (var i = 0; i < samples; i++)
            {
                var t = i / (double)sampleRate;
                var envelope = Math.Exp(-3 * t / seconds);
                writer.Write((short)(Math.Sin(2 * Math.PI * frequency * t) * envelope * short.MaxValue * 0.6));
            }
            writer.Flush();
            return buffer.ToArray();
        }
    }
}
