namespace IntelOrca.Biohazard.BioRand.REE
{
    public abstract class Modifier
    {
        public virtual void LogState(IReeRandomizerContext context, RandomizerLogger logger)
        {
            LogState(logger);
        }

        public virtual void Apply(IReeRandomizerContext context, RandomizerLogger logger)
        {
            Apply(logger);
        }

        public virtual void LogState(RandomizerLogger logger)
        {
        }

        public virtual void Apply(RandomizerLogger logger)
        {
        }
    }
}
