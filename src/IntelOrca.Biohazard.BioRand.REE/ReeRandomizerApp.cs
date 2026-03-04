using System;
using IntelOrca.Biohazard.BioRand.REE.Commands;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.BioRand.REE
{
    public class ReeRandomizerApp
    {
        private readonly CommandApp _commandApp = new CommandApp();

        public string ApplicationName { get; set; } = "biorand";
        public string ApplicationVersion { get; set; } = "1.0";
        public string ExamplePakFilename { get; set; } = "re_chunk_000.pak.patch_006.pak";
        public string ExampleInstallPath { get; set; } = @"C:\Program Files (x86)\Steam\steamapps\common\RESIDENT EVIL 4  BIOHAZARD RE4";
        public IReeRandomizer? Randomizer { get; set; }

        public int Run(string[] args)
        {
            Configure();
            return _commandApp.Run(args);
        }

        private void Configure()
        {
            var randomizerFactory = Randomizer ?? throw new Exception("No randomizer has been configured");
            _commandApp.Configure(config =>
            {
                config.PropagateExceptions();
                config.Settings.ApplicationName = ApplicationName;
                config.Settings.ApplicationVersion = ApplicationVersion;
                config.AddCommand<AgentCommand>("agent")
                    .WithData(randomizerFactory)
                    .WithDescription("Runs a remote generator agent for generating randos")
                    .WithExample("agent", "localhost:8080", "-k", "nCF6UaetQJJ053QLwhXqUGR68U85Rcia", "-i", "input.pak");
                config.AddCommand<GenerateCommand>("generate")
                    .WithData(randomizerFactory)
                    .WithDescription("Generates a new rando")
                    .WithExample("generate", "-o", ExamplePakFilename, "--seed", "35825", "--config", "tough.json");
                config.AddCommand<SetupCommand>("setup")
                    .WithData(randomizerFactory)
                    .WithDescription("Create a mini pak containing all the required vanilla assets.")
                    .WithExample("setup", "-o", "custom.pak", "-i", ExampleInstallPath);
                config.AddCommand<UpdateCommand>("update")
                    .WithData(randomizerFactory)
                    .WithDescription("Updates csv file(s).")
                    .WithExample("update");
                config.AddCommand<ModCommand>("mod")
                    .WithData(randomizerFactory)
                    .WithDescription("Export one or more standalone mods or combine them into a super mod. Run with no arguments to display available mods.")
                    .WithExample("mod", "-m", "flamethrower", "-o", "mods", "-i", ExampleInstallPath)
                    .WithExample("mod", "-m", "flamethrower", "-o", "supermod.zip", "-i", ExampleInstallPath);
            });
        }
    }
}
