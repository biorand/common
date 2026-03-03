using System;

namespace IntelOrca.Biohazard.BioRand
{
    public interface IRandomizerProgress
    {
        void RunTask(string text, Action cb);
    }
}
