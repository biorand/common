using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace IntelOrca.Biohazard.BioRand
{
    public class ZipFileBuilder
    {
        private readonly Dictionary<string, byte[]> _entries;
        private readonly IEqualityComparer<string> _pathComparer;

        public ZipFileBuilder()
            : this(StringComparer.OrdinalIgnoreCase)
        {
        }

        public ZipFileBuilder(IEqualityComparer<string> pathComparer)
        {
            _pathComparer = pathComparer ?? throw new ArgumentNullException(nameof(pathComparer));
            _entries = new(pathComparer);
        }

        public ZipFileBuilder AddEntry(string path, byte[] data)
        {
            _entries[NormalizePath(path, _pathComparer)] = data;
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

        /// <summary>
        /// Normalizes an archive entry path using <see cref="StringComparer.OrdinalIgnoreCase"/>
        /// so that extracted folders are unambiguous on case-sensitive file systems (Linux).
        /// Paths use forward slash separators and are lowercased.
        /// </summary>
        public static string NormalizePath(string path)
        {
            return NormalizePath(path, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Normalizes an archive entry path using the given path comparer so that extracted
        /// folders are unambiguous on case-sensitive file systems (Linux). Paths use forward
        /// slash separators and are lowercased when the comparer ignores case.
        /// </summary>
        public static string NormalizePath(string path, IEqualityComparer<string> pathComparer)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (pathComparer == null)
                throw new ArgumentNullException(nameof(pathComparer));

            var result = path.Replace('\\', '/');
            return pathComparer.Equals("a", "A") ? result.ToLowerInvariant() : result;
        }
    }
}
