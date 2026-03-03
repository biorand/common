using System;

namespace IntelOrca.Biohazard.BioRand
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class OrderAttribute(int order) : Attribute
    {
        public int Order => order;
    }
}
