using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace IntelOrca.Biohazard.BioRand
{
    public class ZipFileBuilder
    {
        private readonly Dictionary<string, byte[]> _entries = [];

        public ZipFileBuilder AddEntry(string path, byte[] data)
        {
            _entries.Add(path, data);
            return this;
        }

        public byte[] Build()
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var kvp in _entries)
                {
                    var fileName = kvp.Key;
                    var fileBytes = kvp.Value;
                    var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    entryStream.Write(fileBytes, 0, fileBytes.Length);
                }
            }
            return ms.ToArray();
        }
    }
}
