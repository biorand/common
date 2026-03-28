using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using IntelOrca.Biohazard.BioRand;

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

    public class FloatModel
    {
        public float FloatValue { get; set; }
        public double DoubleValue { get; set; }
    }

    public class IntArrayModel
    {
        public int[] Numbers { get; set; } = [];
    }

    public class IntListModel
    {
        public List<int> Numbers { get; set; } = [];
    }

    public class ImmutableStringArrayModel
    {
        public ImmutableArray<string> Tags { get; set; }
    }

    public class ImmutableGuidArrayModel
    {
        public ImmutableArray<Guid> Ids { get; set; }
    }

    public class RowNumberModel
    {
        [RowNumber]
        public int Row { get; set; }
        public string Name { get; set; }
    }
}
