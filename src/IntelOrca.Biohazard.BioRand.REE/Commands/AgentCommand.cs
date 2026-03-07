using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.BioRand.REE.Commands
{
    internal sealed class AgentCommand : AsyncCommand<AgentCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Host")]
            [CommandArgument(0, "<host>")]
            public required string Host { get; init; }

            [Description("Seed to generate")]
            [CommandOption("-k|--key")]
            public required string ApiKey { get; init; }

            [CommandOption("-i|--input")]
            public required string InputPath { get; init; }

            [CommandOption("-b|--beta")]
            public bool Beta { get; init; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var randomizer = (IReeRandomizer)context.Data!;
            var gameId = await GetGameIdAsync(settings.Host, randomizer.GameMoniker)
                    ?? throw new Exception($"{randomizer.GameMoniker} not registered");
            var agent = new RandomizerAgent(
                settings.Host,
                settings.ApiKey,
                gameId,
                new RandomizerAgentHandler(randomizer, settings.InputPath, settings.Beta));
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
            try
            {
                await agent.RunAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
            }
            return 0;
        }

        private static async Task<int?> GetGameIdAsync(string uri, string moniker)
        {
            var client = new RandomizerClient(uri);
            var games = await client.GetGamesAsync();
            var game = games.FirstOrDefault(x => x.Moniker == moniker);
            return game?.Id;
        }

        private class RandomizerAgentHandler(IReeRandomizer randomizer, string gameInputPath, bool beta) : IRandomizerAgentHandler
        {
            public string BuildVersion => randomizer.Version;
            public RandomizerConfigurationDefinition ConfigurationDefinition => randomizer.GetConfigurationDefinitionAsync(new RandomizerOptions()
            {
                GameInputPath = gameInputPath,
                Beta = beta
            }).Result;
            public RandomizerConfiguration DefaultConfiguration => ConfigurationDefinition.GetDefault();

            public Task<bool> CanGenerateAsync(RandomizerAgent.QueueResponseItem queueItem)
            {
                return Task.FromResult(true);
            }

            public async Task<RandomizerOutput> GenerateAsync(RandomizerAgent.QueueResponseItem queueItem, RandomizerInput input)
            {
                var generator = await randomizer.CreateGeneratorAsync(input, new RandomizerOptions()
                {
                    GameInputPath = gameInputPath,
                    Beta = beta,
                    UserTags = queueItem.UserTags.ToImmutableArray()
                }, new DummyRandomizerProgress());
                return await generator.GenerateAsync();
            }

            public void LogInfo(string message) => AnsiConsole.MarkupLine($"[gray]{Timestamp} {message}[/]");
            public void LogError(Exception ex, string message) => AnsiConsole.MarkupLine($"[red]{Timestamp} {message} ({ex.Message})[/]");

            private static string Timestamp => DateTime.Now.ToString("[[yyyy-MM-dd HH:mm]]");
        }

        private class EmptyReporter : IRandomizerProgress
        {
            public void RunTask(string text, Action cb)
            {
                cb();
            }
        }
    }
}
