using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.BioRand.REE.Commands
{
    internal sealed class SetupCommand : AsyncCommand<SetupCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-i|--input")]
            public string? InputPath { get; init; }

            [CommandOption("-o|--output")]
            public string? OutputPath { get; init; }
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (settings.InputPath == null)
            {
                return ValidationResult.Error($"Input path not specified");
            }
            if (settings.OutputPath == null)
            {
                return ValidationResult.Error($"Output path not specified");
            }
            return base.Validate(context, settings);
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var randomizer = (IReeRandomizer)context.Data!;
            var patternList = randomizer.PakExtractFileNamePatterns;

            var gamePath = settings.InputPath!;
            var pak = new RePakCollection(gamePath);

            var outputPath = settings.OutputPath!;
            if (outputPath.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
            {
                var newPak = new PakFileBuilder();
                HarvestFiles(pak, (path, data) =>
                {
                    newPak.AddEntry(path, data);
                });
                newPak.Save(settings.OutputPath!, CompressionKind.Zstd);
            }
            else
            {
                HarvestFiles(pak, (path, data) =>
                {
                    var targetPath = Path.Combine(outputPath, path);
                    var targetDir = Path.GetDirectoryName(targetPath)!;
                    Directory.CreateDirectory(targetDir);
                    File.WriteAllBytes(targetPath, data);
                });
            }
            return Task.FromResult(0);

            void HarvestFiles(IPakFile pak, Action<string, byte[]> cb)
            {
                var patternListRegex = randomizer.PakExtractFileNamePatterns.Select(x => new Regex(x, RegexOptions.IgnoreCase)).ToArray();
                foreach (var path in randomizer.PakList.Entries)
                {
                    if (!patternListRegex.Any(x => x.IsMatch(path)))
                        continue;

                    var file = pak.GetEntryData(path);
                    if (file == null)
                    {
                        Console.WriteLine("X " + path);
                    }
                    else
                    {
                        cb(path, file);
                        Console.WriteLine("* " + path);
                    }
                }
            }
        }
    }
}
