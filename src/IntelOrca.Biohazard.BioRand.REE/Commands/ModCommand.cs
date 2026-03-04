using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.BioRand.REE.Commands
{
    internal sealed class ModCommand : AsyncCommand<ModCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-m|--mod")]
            public string[] Mods { get; init; } = [];

            [CommandOption("-i|--input")]
            public string? InputPath { get; init; }

            [CommandOption("-o|--output")]
            public string? OutputPath { get; init; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var randomizer = (IReeRandomizer)context.Data!;
            var generator = await randomizer.CreateGeneratorAsync(new RandomizerInput(), new RandomizerOptions()
            {
                GameInputPath = settings.InputPath ?? ""
            }, new DummyRandomizerProgress());

            var patcher = new Patcher();
            if (settings.Mods == null || settings.Mods.Length == 0)
            {
                PrintMods(patcher);
                return 2;
            }

            var mods = patcher.Mods;
            foreach (var mod in settings.Mods)
            {
                if (!mods.Any(x => x.Name.Equals(mod, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.Error.WriteLine($"{mod} not found");
                    return 1;
                }
            }

            if (string.IsNullOrEmpty(settings.InputPath))
            {
                Console.Error.WriteLine("Input not specified");
                return 1;
            }

            if (string.IsNullOrEmpty(settings.OutputPath))
            {
                Console.Error.WriteLine("Output location not specified");
                return 1;
            }

            foreach (var mod in settings.Mods)
            {
                var modAttribute = mods.First(x => x.Name.Equals(mod, StringComparison.OrdinalIgnoreCase));
                var modBuilder = patcher.ExportMod(generator, modAttribute.Name);
                modBuilder.SavePakFile(Path.Combine(settings.OutputPath, modAttribute.FileName + ".pak"));
                modBuilder.SaveFluffyZipFile(Path.Combine(settings.OutputPath, modAttribute.FileName + ".zip"));
            }
            return 0;
        }

        private void PrintMods(Patcher patcher)
        {
            Console.WriteLine("Available mods:");
            foreach (var mod in patcher.Mods)
            {
                Console.WriteLine($"  {mod.Name} {mod.Version} by {mod.Author}");
            }
        }
    }
}
