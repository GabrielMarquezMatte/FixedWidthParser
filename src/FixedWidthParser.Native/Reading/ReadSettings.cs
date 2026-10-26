using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace FixedWidthParser.Native.Reading
{
    /// <summary>A validated <c>fw_read_options</c>, turned into the core's reader options.</summary>
    internal sealed class ReadSettings
    {
        private const int OptDefault = 0;
        private const int OptTrue = 2;

        private ReadSettings(NativeReadOptionsRaw raw, Encoding? encoding)
        {
            HeaderLines = raw.HeaderLines;
            SkipInvalid = raw.SkipInvalid != 0;
            DegreeOfParallelism = raw.DegreeOfParallelism;
            MaxLineLength = raw.MaxLineLength == 0 ? 1024 * 1024 : raw.MaxLineLength;
            ByteOrderMark = raw.SkipByteOrderMark == OptDefault ? null : raw.SkipByteOrderMark == OptTrue;
            Encoding = encoding;
        }

        internal long HeaderLines { get; }
        internal bool SkipInvalid { get; }
        internal int DegreeOfParallelism { get; }
        internal int MaxLineLength { get; }
        internal bool? ByteOrderMark { get; }
        internal Encoding? Encoding { get; }

        internal static unsafe int TryCreate(NativeReadOptionsRaw* options, out ReadSettings? settings)
        {
            settings = null;
            int status = NativeOptions.TryRead(options, "fw_read_options", out var raw);
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            if (raw.HeaderLines < 0 || raw.DegreeOfParallelism < 0 || raw.MaxLineLength < 0 || raw.SkipByteOrderMark is < 0 or > OptTrue)
            {
                return NativeApi.InvalidArgument("fw_read_options: header_lines, degree_of_parallelism and max_line_length must not be negative, and skip_byte_order_mark must be an FW_OPT_* value.");
            }
            if (!TryEncoding(raw.Encoding, out var encoding))
            {
                return NativeApi.InvalidArgument($"fw_read_options: encoding {raw.Encoding} is not a code page this library knows.");
            }
            settings = new ReadSettings(raw, encoding);
            return NativeStatus.Ok;
        }

        /// <summary>The core's options; a skipped line is counted in <paramref name="skipped"/>.</summary>
        internal FixedWidthReaderOptions ReaderOptions(StrongBox<long> skipped, int degreeOfParallelism, int chunkSize)
        {
            return FixedWidthReaderOptions.Validate(new FixedWidthReaderOptions
            {
                FormatProvider = CultureInfo.InvariantCulture,
                Encoding = Encoding,
                MaxLineLength = MaxLineLength,
                DegreeOfParallelism = degreeOfParallelism,
                ChunkSize = chunkSize,
                ByteOrderMark = ByteOrderMark,
                OnSkip = SkipInvalid ? () => Interlocked.Increment(ref skipped.Value) : null,
            });
        }

        internal static bool TryEncoding(int codePage, out Encoding? encoding)
        {
            encoding = null;
            if (codePage == 0)
            {
                return true;
            }
            try
            {
                encoding = Encoding.GetEncoding(codePage);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                return false;
            }
        }
    }
}
