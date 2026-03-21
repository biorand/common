using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public enum TestEnum
    {
        A,
        B,
        C
    }

    public class BasicModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class NullableModel
    {
        public int? Value { get; set; }
        public TestEnum? EnumValue { get; set; }
    }

    public class EnumModel
    {
        public TestEnum Value { get; set; }
    }

    public class ArrayModel
    {
        public ImmutableArray<int> Numbers { get; set; }
    }

    public class GuidModel
    {
        public Guid Id { get; set; }
    }

    // TODO: Add more intricate models
}
