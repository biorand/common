namespace IntelOrca.Biohazard.BioRand
{
    public abstract class Modifier
    {
        public virtual void LogState(IReeContext context, RandomizerLogger logger)
        {
        }

        public virtual void Apply(IReeContext context, RandomizerLogger logger)
        {
        }
    }
}
