using System.Runtime.InteropServices;
using System.Text;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Native.Writing
{
    /// <summary><c>fw_write_options</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeWriteOptionsRaw
    {
        public int StructSize;
        public int Encoding;
        public byte* NewLine;
        public int NewLineLen;
        public int DegreeOfParallelism;
        public long BatchRows;
    }

    /// <summary><c>fw_buffer</c>: bytes the library allocated, released with <c>fw_free_buffer</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeBufferRaw
    {
        public byte* Data;
        public long Length;
    }

    /// <summary>A validated <c>fw_write_options</c>.</summary>
    internal sealed class WriteSettings
    {
        private const long DefaultBatchRows = 8192;

        private WriteSettings(Encoding? encoding, byte[] newLine, int degreeOfParallelism, long batchRows)
        {
            Encoding = encoding;
            NewLine = newLine;
            DegreeOfParallelism = degreeOfParallelism == 0 ? Environment.ProcessorCount : degreeOfParallelism;
            BatchRows = batchRows == 0 ? DefaultBatchRows : batchRows;
        }

        internal Encoding? Encoding { get; }
        internal byte[] NewLine { get; }
        internal int DegreeOfParallelism { get; }
        internal long BatchRows { get; }

        internal static unsafe int TryCreate(NativeWriteOptionsRaw* options, out WriteSettings? settings)
        {
            settings = null;
            int status = NativeOptions.TryRead(options, "fw_write_options", out var raw);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            if (raw.DegreeOfParallelism < 0 || raw.BatchRows < 0 || raw.NewLineLen < 0 || (raw.NewLine is null && raw.NewLineLen > 0))
            {
                return Fail("fw_write_options: degree_of_parallelism, batch_rows and newline_len must not be negative, and a newline_len needs a newline.");
            }
            if (!ReadSettings.TryEncoding(raw.Encoding, out var encoding))
            {
                return Fail($"fw_write_options: encoding {raw.Encoding} is not a code page this library knows.");
            }
            byte[] newLine = raw.NewLine is null ? [(byte)'\n'] : new ReadOnlySpan<byte>(raw.NewLine, raw.NewLineLen).ToArray();
            settings = new WriteSettings(encoding, newLine, raw.DegreeOfParallelism, raw.BatchRows);
            return NativeStatus.Ok;
        }

        private static int Fail(string message)
        {
            NativeApi.SetLastError(message);
            return NativeStatus.InvalidArgument;
        }
    }
}
