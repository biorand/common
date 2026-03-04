using System;
using System.Collections.Concurrent;
using System.IO;
using IntelOrca.Biohazard.REE;
using IntelOrca.Biohazard.REE.Package;

namespace IntelOrca.Biohazard.BioRand.REE
{
    internal class FileRepository : IDisposable
    {
        private readonly PatchedPakFile? _inputPakFile;
        private readonly string? _inputGamePath;
        private readonly ConcurrentDictionary<string, byte[]> _outputFiles = new(StringComparer.OrdinalIgnoreCase);

        public FileRepository(string inputGamePath)
        {
            if (inputGamePath.EndsWith(".pak", System.StringComparison.OrdinalIgnoreCase))
            {
                _inputPakFile = new PatchedPakFile(inputGamePath);
            }
            else
            {
                _inputGamePath = inputGamePath;
            }
        }

        public void Dispose()
        {
            _inputPakFile?.Dispose();
        }

        public byte[]? GetFile(string path)
        {
            if (_outputFiles.TryGetValue(path, out var data))
                return data;

            if (_inputGamePath == null)
            {
                return _inputPakFile?.GetEntryData(path);
            }
            else
            {
                var fullPath = Path.Combine(_inputGamePath, path);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllBytes(fullPath);
                }
                return null;
            }
        }

        public void SetFile(string path, byte[] data)
        {
            _outputFiles[path] = data;
        }

        public void WriteOutputPakFile(string path)
        {
            var builder = new PakFileBuilder();
            foreach (var outputFile in _outputFiles)
            {
                builder.AddEntry(outputFile.Key, outputFile.Value);
            }
            builder.Save(path, CompressionKind.Zstd);
        }

        public PakFileBuilder GetOutputPakFile()
        {
            var builder = new PakFileBuilder();
            foreach (var outputFile in _outputFiles)
            {
                builder.AddEntry(outputFile.Key, outputFile.Value);
            }
            return builder;
        }

        public void WriteOutputFolder(string path)
        {
            foreach (var outputFile in _outputFiles)
            {
                var fullPath = Path.Combine(path, outputFile.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllBytes(fullPath, outputFile.Value);
            }
        }

        public void AddFilesToModBuilder(ModBuilder modBuilder)
        {
            foreach (var kvp in _outputFiles)
            {
                modBuilder.AddFile(kvp.Key, kvp.Value);
            }
        }
    }
}
