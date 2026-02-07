using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand
{
    public class RandomizerOutput(
        ImmutableArray<RandomizerOutputAsset> assets,
        string instructions)
    {
        public ImmutableArray<RandomizerOutputAsset> Assets => assets;
        public string Instructions => instructions;
    }
}
