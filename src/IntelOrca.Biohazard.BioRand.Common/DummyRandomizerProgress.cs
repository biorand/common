using System;

namespace IntelOrca.Biohazard.BioRand
{
    public class DummyRandomizerProgress : IRandomizerProgress
    {
        public static DummyRandomizerProgress Default = new();

        public void RunTask(string text, Action cb)
        {
        }
    }
}
