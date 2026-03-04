using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand.REE
{
    public class RandomizerOptions
    {
        public required string GameInputPath { get; init; }
        public bool Beta { get; init; }
        public ImmutableArray<string> UserTags { get; init; } = [];
    }
}
