using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand
{
    public class RandomizerOutput
    {
        public ImmutableArray<RandomizerOutputAsset> Assets { get; }
        public string Instructions { get; }
        public string RequiredUserTags { get; }

        public RandomizerOutput(
            ImmutableArray<RandomizerOutputAsset> assets,
            string instructions)
            : this(assets, instructions, "")
        {
        }

        public RandomizerOutput(
            ImmutableArray<RandomizerOutputAsset> assets,
            string instructions,
            string requiredUserTags)
        {
            Assets = assets;
            Instructions = instructions;
            RequiredUserTags = requiredUserTags;
        }
    }
}
