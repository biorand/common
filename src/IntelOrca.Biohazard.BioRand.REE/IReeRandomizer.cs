using System.Collections.Immutable;
using IntelOrca.Biohazard.REE.Package;

namespace IntelOrca.Biohazard.BioRand.REE
{
    public interface IReeRandomizer
    {
        string Version { get; }
        string Author { get; }
        string GameMoniker { get; }
        string? ProcessName { get; }
        PakList PakList { get; }
        ImmutableArray<string> PakExtractFileNamePatterns { get; }

        RandomizerConfigurationDefinition GetConfigurationDefinition(RandomizerOptions options);
        RandomizerOutput Generate(RandomizerInput input, RandomizerOptions options, IRandomizerProgress progress);
    }

    public class RandomizerOptions
    {
        public required string GameInputPath { get; init; }
        public bool Beta { get; init; }
        public ImmutableArray<string> UserTags { get; init; } = [];
    }
}
