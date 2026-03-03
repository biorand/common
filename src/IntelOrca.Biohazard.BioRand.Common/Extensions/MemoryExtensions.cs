using System;
using System.IO;
using System.IO.Compression;

namespace IntelOrca.Biohazard.BioRand.Extensions
{
    public static class MemoryExtensions
    {
        public static void WriteToFile(this string data, string path)
        {
            File.WriteAllText(path, data);
        }

        public static void WriteToFile(this byte[] data, string path)
            => data.AsSpan().WriteToFile(path);

        public static void WriteToFile(this Span<byte> data, string path)
        {
#if NET
            File.WriteAllBytes(path, data);
#else
            File.WriteAllBytes(path, data.ToArray());
#endif
        }

        public static void WriteToFile(this ReadOnlyMemory<byte> data, string path)
            => data.Span.WriteToFile(path);

        public static void WriteToFile(this ReadOnlySpan<byte> data, string path)
        {
#if NET
            File.WriteAllBytes(path, data);
#else
            File.WriteAllBytes(path, data.ToArray());
#endif
        }

        public static byte[] Ungzip(this byte[] input)
        {
            using var inputStream = new MemoryStream(input);
            using var outputStream = new MemoryStream();
            using var deflateStream = new GZipStream(inputStream, CompressionMode.Decompress);
            deflateStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
}
