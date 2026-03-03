using System;

namespace IntelOrca.Biohazard.BioRand
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class RowNumberAttribute : Attribute
    {
        public int RowNumber { get; set; }
    }
}
