namespace IntelOrca.Biohazard.BioRand.REE
{
    public abstract class Modifier
    {
        public virtual void LogState(IReeRandomizerContext context, RandomizerLogger logger)
        {
        }

        public virtual void Apply(IReeRandomizerContext context, RandomizerLogger logger)
        {
        }
    }
}
