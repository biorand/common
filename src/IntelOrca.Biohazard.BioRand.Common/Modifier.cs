namespace IntelOrca.Biohazard.BioRand
{
    internal abstract class Modifier
    {
        public virtual void LogState(ReeRandomizer randomizer, RandomizerLogger logger)
        {
        }

        public virtual void Apply(ReeRandomizer randomizer, RandomizerLogger logger)
        {
        }
    }
}
