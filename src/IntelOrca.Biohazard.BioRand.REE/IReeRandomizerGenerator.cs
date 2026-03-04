using System.Threading.Tasks;

namespace IntelOrca.Biohazard.BioRand.REE
{
    public interface IReeRandomizerGenerator : IReeRandomizerContext
    {
        Task<RandomizerOutput> GenerateAsync();
    }
}
