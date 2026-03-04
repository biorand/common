using System;

namespace IntelOrca.Biohazard.BioRand
{
    [AttributeUsage(AttributeTargets.Class)]
    public class OrderAttribute(int order) : Attribute
    {
        public int Order => order;
    }
}
