using System;
using System.Linq;
using System.Text;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestCsv
    {
        private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

        [Fact]
        public void Deserialize_BasicMapping_Works()
        {
            var csv = @"Id,Name
1,Alice
2,Bob";

            var result = Csv.Deserialize<BasicModel>(Utf8(csv));

            Assert.Equal(2, result.Length);

            Assert.Equal(1, result[0].Id);
            Assert.Equal("Alice", result[0].Name);

            Assert.Equal(2, result[1].Id);
            Assert.Equal("Bob", result[1].Name);
        }

        [Fact]
        public void Deserialize_Enum_Works()
        {
            var csv = @"Value
A
B";

            var result = Csv.Deserialize<EnumModel>(Utf8(csv));

            Assert.Equal(TestEnum.A, result[0].Value);
            Assert.Equal(TestEnum.B, result[1].Value);
        }

        [Fact]
        public void Deserialize_NullableEnum_Works()
        {
            var csv = @"EnumValue
A

C";

            var result = Csv.Deserialize<NullableModel>(Utf8(csv));

            Assert.Equal(TestEnum.A, result[0].EnumValue);
            Assert.Null(result[1].EnumValue);
            Assert.Equal(TestEnum.C, result[2].EnumValue);
        }

        [Fact]
        public void Deserialize_NullableInt_Works()
        {
            var csv = @"Value
1

3";

            var result = Csv.Deserialize<NullableModel>(Utf8(csv));

            Assert.Equal(1, result[0].Value);
            Assert.Null(result[1].Value);
            Assert.Equal(3, result[2].Value);
        }

        [Fact]
        public void Deserialize_ImmutableArray_Works()
        {
            var csv = @"Numbers
1 2 3
4 5";

            var result = Csv.Deserialize<ArrayModel>(Utf8(csv));

            Assert.Equal(new[] { 1, 2, 3 }, result[0].Numbers.ToArray());
            Assert.Equal(new[] { 4, 5 }, result[1].Numbers.ToArray());
        }

        [Fact]
        public void Deserialize_Guid_Works()
        {
            var guid = Guid.NewGuid();
            var csv = $@"Id
{guid}";

            var result = Csv.Deserialize<GuidModel>(Utf8(csv));

            Assert.Equal(guid, result[0].Id);
        }

        [Fact]
        public void Deserialize_QuotedFields_WithComma_Works()
        {
            var result = Csv.Deserialize<BasicModel>(Utf8(@"Id,Name
1,""Alice, Smith""
2,Bob"));

            Assert.Equal("Alice, Smith", result[0].Name);
            Assert.Equal("Bob", result[1].Name);
        }

        [Fact]
        public void Deserialize_QuotedFields_WithEscapedQuotes_Works()
        {
            var result = Csv.Deserialize<BasicModel>(Utf8(@"Id,Name
1,""Alice """"The Great""""""
2,Bob"));

            Assert.Equal("Alice \"The Great\"", result[0].Name);
        }

        [Fact]
        public void Deserialize_MissingColumn_Ignored()
        {
            var csv = @"Id,Unknown
1,X";

            var result = Csv.Deserialize<BasicModel>(Utf8(csv));

            Assert.Equal(1, result[0].Id);
            Assert.Null(result[0].Name);
        }

        [Fact]
        public void Deserialize_ExtraColumns_Ignored()
        {
            var csv = @"Id,Name,Extra
1,Alice,X";

            var result = Csv.Deserialize<BasicModel>(Utf8(csv));

            Assert.Equal(1, result[0].Id);
            Assert.Equal("Alice", result[0].Name);
        }

        [Fact]
        public void Deserialize_Handles_CRLF_And_LF()
        {
            var csv = "Id,Name\r\n1,Alice\r\n2,Bob";

            var result = Csv.Deserialize<BasicModel>(Utf8(csv));

            Assert.Equal(2, result.Length);
        }

        [Fact]
        public void Deserialize_EmptyFile_ReturnsEmpty()
        {
            var result = Csv.Deserialize<BasicModel>(Utf8(""));

            Assert.Empty(result);
        }

        [Fact]
        public void Deserialize_Array_Works()
        {
            var csv = @"Numbers
1 2 3
4 5";

            var result = Csv.Deserialize<IntArrayModel>(Utf8(csv));

            Assert.Equal(new[] { 1, 2, 3 }, result[0].Numbers);
            Assert.Equal(new[] { 4, 5 }, result[1].Numbers);
        }

        [Fact]
        public void Deserialize_List_Works()
        {
            var csv = @"Numbers
1 2 3
4 5";

            var result = Csv.Deserialize<IntListModel>(Utf8(csv));

            Assert.Equal(new[] { 1, 2, 3 }, result[0].Numbers);
            Assert.Equal(new[] { 4, 5 }, result[1].Numbers);
        }

        [Fact]
        public void Deserialize_RowNumber_Works()
        {
            var csv = @"Name
Alice
Bob
Charlie";

            var result = Csv.Deserialize<RowNumberModel>(Utf8(csv));

            Assert.Equal(2, result[0].Row);
            Assert.Equal(3, result[1].Row);
            Assert.Equal(4, result[2].Row);
        }

        [Fact]
        public void Deserialize_Float_Works()
        {
            var csv = @"FloatValue,DoubleValue
1.5,3.14
-2.25,0.001";

            var result = Csv.Deserialize<FloatModel>(Utf8(csv));

            Assert.Equal(1.5f, result[0].FloatValue);
            Assert.Equal(3.14, result[0].DoubleValue, precision: 10);
            Assert.Equal(-2.25f, result[1].FloatValue);
            Assert.Equal(0.001, result[1].DoubleValue, precision: 10);
        }

        [Fact]
        public void Deserialize_ImmutableStringArray_Works()
        {
            var csv = @"Tags
foo bar baz
hello world";

            var result = Csv.Deserialize<ImmutableStringArrayModel>(Utf8(csv));

            Assert.Equal(new[] { "foo", "bar", "baz" }, result[0].Tags.ToArray());
            Assert.Equal(new[] { "hello", "world" }, result[1].Tags.ToArray());
        }

        [Fact]
        public void Deserialize_ImmutableGuidArray_Works()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var csv = $@"Ids
{a} {b}
{c}";

            var result = Csv.Deserialize<ImmutableGuidArrayModel>(Utf8(csv));

            Assert.Equal(new[] { a, b }, result[0].Ids.ToArray());
            Assert.Equal(new[] { c }, result[1].Ids.ToArray());
        }
    }
}