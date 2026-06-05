using System.Text;
using FixedWidthParser.Readers;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Covers the Stream overloads of <see cref="FixedWidthReader{TModel}"/> (sync and async),
    /// including custom encoding and the leaveOpen behavior.
    /// </summary>
    public class ReaderStreamTests
    {
        private const string TwoPeople =
            "John Doe  30   60000.00  \n" +
            "Jane      28   55000.00  ";

        private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

        // ----------------------------- Synchronous -----------------------------

        [Fact]
        public void Read_Stream_ParsesAll()
        {
            var reader = new FixedWidthReader<PersonModel>(Inv);
            using var stream = Utf8(TwoPeople);

            var people = reader.Read(stream).ToList();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal("Jane", people[1].Name);
            Assert.Equal(55000.00, people[1].Salary, 2);
        }

        [Fact]
        public void Read_Stream_CustomEncoding_IsHonored()
        {
            // 'Á' (0xC1) is a single byte in Latin1 but invalid as standalone UTF-8.
            var reader = new FixedWidthReader<CodeModel>();
            using var stream = new MemoryStream(Encoding.Latin1.GetBytes("Áb \n"));

            var codes = reader.Read(stream, Encoding.Latin1).Select(m => m.Code).ToList();

            Assert.Equal(["Áb"], codes);
        }

        [Fact]
        public void Read_Stream_LeaveOpenTrue_KeepsStreamOpen()
        {
            var reader = new FixedWidthReader<CodeModel>();
            using var stream = Utf8("ABC\nDEF\n");

            _ = reader.Read(stream, leaveOpen: true).ToList();

            Assert.True(stream.CanRead); // not disposed
        }

        [Fact]
        public void Read_Stream_LeaveOpenFalse_DisposesStream()
        {
            var reader = new FixedWidthReader<CodeModel>();
            var stream = Utf8("ABC\nDEF\n");

            _ = reader.Read(stream).ToList(); // default leaveOpen: false

            Assert.False(stream.CanRead); // disposed together with the StreamReader
        }

        [Fact]
        public void Read_NullStream_Throws()
        {
            var reader = new FixedWidthReader<CodeModel>();

            Assert.Throws<ArgumentNullException>(() => reader.Read((Stream)null!));
        }

        // ----------------------------- Asynchronous -----------------------------

        [Fact]
        public async Task ReadAsync_Stream_ParsesAll()
        {
            var reader = new FixedWidthReader<PersonModel>(Inv);
            using var stream = Utf8(TwoPeople);

            var people = await reader.ReadAsync(stream).ToListAsync();

            Assert.Equal(2, people.Count);
            Assert.Equal("John Doe", people[0].Name);
            Assert.Equal("Jane", people[1].Name);
        }

        [Fact]
        public async Task ReadAsync_Stream_CustomEncoding_IsHonored()
        {
            var reader = new FixedWidthReader<CodeModel>();
            using var stream = new MemoryStream(Encoding.Latin1.GetBytes("Áb \n"));

            var codes = await reader.ReadAsync(stream, Encoding.Latin1).Select(m => m.Code).ToListAsync();

            Assert.Equal(["Áb"], codes);
        }

        [Fact]
        public async Task ReadAsync_Stream_LeaveOpenTrue_KeepsStreamOpen()
        {
            var reader = new FixedWidthReader<CodeModel>();
            using var stream = Utf8("ABC\nDEF\n");

            _ = await reader.ReadAsync(stream, leaveOpen: true).ToListAsync();

            Assert.True(stream.CanRead);
        }

        [Fact]
        public async Task ReadAsync_Stream_LeaveOpenFalse_DisposesStream()
        {
            var reader = new FixedWidthReader<CodeModel>();
            var stream = Utf8("ABC\nDEF\n");

            _ = await reader.ReadAsync(stream).ToListAsync();

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void ReadAsync_NullStream_Throws()
        {
            var reader = new FixedWidthReader<CodeModel>();

            Assert.Throws<ArgumentNullException>(() => reader.ReadAsync((Stream)null!));
        }
    }
}
