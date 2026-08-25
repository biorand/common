using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestZipFileBuilder
    {
        private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

        [Fact]
        public void AddEntry_NormalizesPath()
        {
            var builder = new ZipFileBuilder();
            builder.AddEntry(@"Natives\STM\Foo.Bar.21", Utf8("a"));

            using var ms = new MemoryStream(builder.Build());
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = Assert.Single(archive.Entries);
            Assert.Equal("natives/stm/foo.bar.21", entry.FullName);
        }

        [Fact]
        public void AddEntry_CaseVariants_AreMergedNotDuplicated()
        {
            var builder = new ZipFileBuilder();
            builder.AddEntry("Natives/STM/a.txt", Utf8("first"));
            builder.AddEntry("natives/stm/A.txt", Utf8("second"));

            using var ms = new MemoryStream(builder.Build());
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = Assert.Single(archive.Entries);
            Assert.Equal("natives/stm/a.txt", entry.FullName);
        }

        [Fact]
        public void AddEntry_LastWriteWinsForCaseVariants()
        {
            var builder = new ZipFileBuilder();
            builder.AddEntry("Natives/STM/a.txt", Utf8("first"));
            builder.AddEntry("natives/stm/A.txt", Utf8("second"));

            using var ms = new MemoryStream(builder.Build());
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = archive.Entries.Single();
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            Assert.Equal("second", reader.ReadToEnd());
        }

        [Fact]
        public void AddEntry_OrdinalIgnoreCase_DefaultsToCaseInsensitive()
        {
            var builder = new ZipFileBuilder();
            builder.AddEntry("Natives/STM/a.txt", Utf8("first"));
            builder.AddEntry("natives/stm/A.txt", Utf8("second"));

            using var ms = new MemoryStream(builder.Build());
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.Single(archive.Entries);
        }

        [Fact]
        public void AddEntry_OrdinalComparer_PreservesCaseAndKeepsVariants()
        {
            var builder = new ZipFileBuilder(StringComparer.Ordinal);
            builder.AddEntry(@"Natives\STM\a.txt", Utf8("first"));
            builder.AddEntry("natives/stm/A.txt", Utf8("second"));

            using var ms = new MemoryStream(builder.Build());
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.Equal(2, archive.Entries.Count);
            Assert.Contains(archive.Entries, x => x.FullName == "Natives/STM/a.txt");
            Assert.Contains(archive.Entries, x => x.FullName == "natives/stm/A.txt");
        }

        [Theory]
        [InlineData("natives/stm/foo.txt", "natives/stm/foo.txt")]
        [InlineData(@"Natives\STM\Foo.TXT", "natives/stm/foo.txt")]
        [InlineData("/Leading/Slash", "/leading/slash")]
        public void NormalizePath_ProducesCanonicalForm(string input, string expected)
        {
            Assert.Equal(expected, ZipFileBuilder.NormalizePath(input));
        }

        [Theory]
        [InlineData(@"Natives\STM\Foo.TXT", true, "natives/stm/foo.txt")]
        [InlineData(@"Natives\STM\Foo.TXT", false, "Natives/STM/Foo.TXT")]
        public void NormalizePath_UsesComparerCaseSensitivity(string input, bool ignoreCase, string expected)
        {
            IEqualityComparer<string> comparer = ignoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            Assert.Equal(expected, ZipFileBuilder.NormalizePath(input, comparer));
        }

        [Fact]
        public void AddEntry_Null_Throws()
        {
            var builder = new ZipFileBuilder();
            Assert.Throws<ArgumentNullException>(() => builder.AddEntry(null!, Utf8("a")));
        }
    }
}
