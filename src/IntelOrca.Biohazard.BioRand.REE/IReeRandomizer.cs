using System.Collections.Immutable;
using System.Threading.Tasks;
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

        Task<RandomizerConfigurationDefinition> GetConfigurationDefinitionAsync(RandomizerOptions options);
        Task<IReeRandomizerGenerator> CreateGeneratorAsync(RandomizerInput input, RandomizerOptions options, IRandomizerProgress progress);
    }
}
