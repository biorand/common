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
            var csv = "Id,Name\n1,Alice\n2,Bob";

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
            var csv = "Value\nA\nB";

            var result = Csv.Deserialize<EnumModel>(Utf8(csv));

            Assert.Equal(TestEnum.A, result[0].Value);
            Assert.Equal(TestEnum.B, result[1].Value);
        }

        [Fact]
        public void Deserialize_NullableEnum_Works()
        {
            var csv = "EnumValue\nA\n\nC";

            var result = Csv.Deserialize<NullableModel>(Utf8(csv));

            Assert.Equal(TestEnum.A, result[0].EnumValue);
            Assert.Null(result[1].EnumValue);
            Assert.Equal(TestEnum.C, result[2].EnumValue);
        }

        [Fact]
        public void Deserialize_NullableInt_Works()
        {
            var csv = "Value\n1\n\n3";

            var result = Csv.Deserialize<NullableModel>(Utf8(csv));

            Assert.Equal(1, result[0].Value);
            Assert.Null(result[1].Value);
            Assert.Equal(3, result[2].Value);
        }

        [Fact]
        public void Deserialize_ImmutableArray_Works()
        {
            var csv = "Numbers\n1 2 3\n4 5";

            var result = Csv.Deserialize<ArrayModel>(Utf8(csv));

            Assert.Equal(new[] { 1, 2, 3 }, result[0].Numbers.ToArray());
            Assert.Equal(new[] { 4, 5 }, result[1].Numbers.ToArray());
        }

        [Fact]
        public void Deserialize_Guid_Works()
        {
            var guid = Guid.NewGuid();
            var csv = $"Id\n{guid}";

            var result = Csv.Deserialize<GuidModel>(Utf8(csv));

            Assert.Equal(guid, result[0].Id);
        }

        [Fact]
        public void Deserialize_QuotedFields_WithComma_Works()
        {
            var csv = "Name\n\"Alice, Smith\"\nBob";

            var result = Csv.Deserialize<BasicModel>(Utf8("Id,Name\n1,\"Alice, Smith\"\n2,Bob"));

            Assert.Equal("Alice, Smith", result[0].Name);
            Assert.Equal("Bob", result[1].Name);
        }

        [Fact]
        public void Deserialize_QuotedFields_WithEscapedQuotes_Works()
        {
            var csv = "Name\n\"Alice \"\"The Great\"\"\"\nBob";

            var result = Csv.Deserialize<BasicModel>(Utf8("Id,Name\n1,\"Alice \"\"The Great\"\"\"\n2,Bob"));

            Assert.Equal("Alice \"The Great\"", result[0].Name);
        }

        [Fact]
        public void Deserialize_MissingColumn_Ignored()
        {
            var csv = "Id,Unknown\n1,X";

            var result = Csv.Deserialize<BasicModel>(Utf8(csv));

            Assert.Equal(1, result[0].Id);
            Assert.Null(result[0].Name);
        }

        [Fact]
        public void Deserialize_ExtraColumns_Ignored()
        {
            var csv = "Id,Name,Extra\n1,Alice,X";

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
    }
}