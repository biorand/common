using System;
using System.Threading.Tasks;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestAgent
    {
        [Fact]
        public async Task Randomize()
        {
            var host = GetEnv("BIORAND_API_URL", "http://localhost:10285");
            var apiKey = GetEnv("BIORAND_API_KEY", "");
            if (string.IsNullOrEmpty(apiKey))
            {
                Assert.Skip("API key is required to run this test.");
            }

            var game = 1;
            using var agent = new RandomizerAgent(host, apiKey, game, new Handler());
            await agent.RunAsync(TestContext.Current.CancellationToken);
        }

        private static string GetEnv(string name, string defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        private class Handler : IRandomizerAgentHandler
        {
            public RandomizerConfigurationDefinition ConfigurationDefinition => new RandomizerConfigurationDefinition();
            public RandomizerConfiguration DefaultConfiguration => new RandomizerConfiguration();
            public string BuildVersion => "1.0";

            public Task<bool> CanGenerateAsync(RandomizerAgent.QueueResponseItem queueItem) => Task.FromResult(true);
            public Task<RandomizerOutput> GenerateAsync(RandomizerAgent.QueueResponseItem queueItem, RandomizerInput input) => Task.FromResult(Randomize(input));
            public void LogError(Exception ex, string message) => Assert.Fail(message);
            public void LogInfo(string message) { }

            private RandomizerOutput Randomize(RandomizerInput input)
            {
                return new RandomizerOutput(
                    [
                        new RandomizerOutputAsset(
                            "1-patch",
                            "Patch",
                            "Simply drop this file into your RE 4 install folder.",
                            "biorand-re4r-58252-mod.zip",
                            new byte[16]),
                        new RandomizerOutputAsset(
                            "2-fluffy",
                            "Fluffy Mod",
                            "Drop this zip file into Fluffy Mod Manager's mod folder and enable it.",
                            "biorand-re4r-58252.zip",
                            new byte[16])
                    ],
                    """
                    <p class="mt-3">What should I do if my game crashes?</p>
                    <ol class="ml-8 list-decimal text-gray-300">
                      <li>Reload from last checkpoint and try again.</li>
                      <li>Alter the enemy sliders slightly or reduce the number temporarily. This will reshuffle the enemies. Reload from last checkpoint and try again.</li> <li>As a last resort, change your seed, and reload from last checkpoint.</li>
                    </ol>
                    """);
            }
        }
    }
}
