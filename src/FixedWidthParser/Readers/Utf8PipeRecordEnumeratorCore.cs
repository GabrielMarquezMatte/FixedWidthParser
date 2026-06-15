using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// <see cref="PipeReader"/>-based counterpart of
    /// <see cref="Utf8AsyncRecordEnumeratorCore{TModel, TParser}"/>: parses fixed-width records from a
    /// <see cref="PipeReader"/>, letting the pipe own buffering and read-ahead instead of the manual
    /// pooled <see cref="LineBufferState{T, TFormat}"/> the stream cores use. Lines are split out of the
    /// <see cref="ReadOnlySequence{T}"/> with a <see cref="SequenceReader{T}"/>; a line is parsed in place
    /// from its <see cref="ReadOnlySequence{T}.FirstSpan"/> when it sits in a single segment, and copied
    /// into a pooled scratch buffer only when it straddles segment boundaries (the parser needs a
    /// contiguous span). Specialized by a <see langword="struct"/> <typeparamref name="TParser"/> strategy
    /// so parsing devirtualizes, exactly like the stream cores.
    /// <para>
    /// Line semantics match the stream path: a leading UTF-8 BOM is skipped once, lines split on
    /// <c>\n</c> (a preceding <c>\r</c> is dropped), empty lines are counted but not yielded, and a final
    /// line without a trailing newline is yielded.
    /// </para>
    /// </summary>
    public sealed class Utf8PipeRecordEnumeratorCore<TModel, TParser> : IAsyncEnumerator<TModel>
        where TParser : struct, IUtf8LineParser<TModel>
    {
        private const byte Cr = (byte)'\r';
        private const byte Lf = (byte)'\n';

        private readonly TParser _strategy;
        private readonly bool _completeReader;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly CancellationToken _cancellationToken;
        private PipeReader? _reader;
        private byte[]? _scratch;
        private long _lineNumber;
        private bool _preambleChecked;
        private TModel _current;

        internal Utf8PipeRecordEnumeratorCore(
            TParser strategy,
            PipeReader reader,
            bool completeReader,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            CancellationToken cancellationToken)
        {
            _strategy = strategy;
            _reader = reader;
            _completeReader = completeReader;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _cancellationToken = cancellationToken;
            _current = default!;
        }

        public TModel Current => _current;

        public async ValueTask<bool> MoveNextAsync()
        {
            var reader = _reader ?? throw new ObjectDisposedException(nameof(Utf8PipeRecordEnumeratorCore<,>));
            while (true)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                ReadResult result = await reader.ReadAsync(_cancellationToken).ConfigureAwait(false);

                // Synchronous step: the sequence and the spans sliced from it never cross the await above.
                var status = TryReadFromResult(in result, out var consumed, out var examined);
                reader.AdvanceTo(consumed, examined);

                if (status == LineStatus.Line)
                {
                    return true;
                }
                if (status == LineStatus.End || result.IsCompleted)
                {
                    return false;
                }
                // NeedData: loop; the next ReadAsync blocks only until more data is produced.
            }
        }

        private LineStatus TryReadFromResult(in ReadResult result, out SequencePosition consumed, out SequencePosition examined)
        {
            ReadOnlySequence<byte> buffer = result.Buffer;
            bool isCompleted = result.IsCompleted;

            if (!_preambleChecked)
            {
                if (buffer.Length >= 3)
                {
                    Span<byte> head = stackalloc byte[3];
                    buffer.Slice(0, 3).CopyTo(head);
                    int skip = head is [0xEF, 0xBB, 0xBF] ? 3 : 0;
                    buffer = buffer.Slice(skip);
                    _preambleChecked = true;
                }
                else if (isCompleted)
                {
                    _preambleChecked = true; // too short to hold a BOM; no preamble
                }
                else
                {
                    consumed = buffer.Start;
                    examined = buffer.End;
                    return LineStatus.NeedData;
                }
            }

            var seqReader = new SequenceReader<byte>(buffer);
            while (seqReader.TryReadTo(out ReadOnlySequence<byte> lineSeq, Lf, advancePastDelimiter: true))
            {
                _lineNumber++;
                var line = TrimTrailingCr(in lineSeq);
                if (!line.IsEmpty)
                {
                    Parse(in line);
                    consumed = seqReader.Position;
                    examined = seqReader.Position;
                    return LineStatus.Line;
                }
                // Empty line: already consumed by the reader; keep scanning for a real one.
            }

            if (isCompleted)
            {
                // Trailing content with no final '\n' is the last line.
                ReadOnlySequence<byte> remaining = buffer.Slice(seqReader.Position);
                consumed = buffer.End;
                examined = buffer.End;
                if (remaining.Length > 0)
                {
                    _lineNumber++;
                    var line = TrimTrailingCr(in remaining);
                    if (!line.IsEmpty)
                    {
                        Parse(in line);
                        return LineStatus.Line;
                    }
                }
                return LineStatus.End;
            }

            // No newline yet: keep the complete (empty) lines consumed, but mark the whole buffer examined
            // so the pipe fetches more before calling back.
            consumed = seqReader.Position;
            examined = buffer.End;
            return LineStatus.NeedData;
        }

        private void Parse(in ReadOnlySequence<byte> line)
        {
            if (line.IsSingleSegment)
            {
                ParseSpan(line.FirstSpan);
                return;
            }

            int length = (int)line.Length;
            if (_scratch is null || _scratch.Length < length)
            {
                if (_scratch is not null)
                {
                    ArrayPool<byte>.Shared.Return(_scratch);
                }
                _scratch = ArrayPool<byte>.Shared.Rent(length);
            }
            line.CopyTo(_scratch);
            ParseSpan(_scratch.AsSpan(0, length));
        }

        private void ParseSpan(ReadOnlySpan<byte> line)
        {
            if (!_strategy.TryParse(line, _formatProvider, _stringPool, out _current))
            {
                throw new FormatException(
                    $"Line {_lineNumber} could not be parsed into {typeof(TModel).Name}: \"{Encoding.UTF8.GetString(line)}\".");
            }
        }

        private static ReadOnlySequence<byte> TrimTrailingCr(in ReadOnlySequence<byte> line)
        {
            long length = line.Length;
            if (length == 0)
            {
                return line;
            }
            // A single-element slice is always single-segment, so FirstSpan[0] is the last byte.
            byte last = line.Slice(length - 1).FirstSpan[0];
            return last == Cr ? line.Slice(0, length - 1) : line;
        }

        public async ValueTask DisposeAsync()
        {
            if (_scratch is not null)
            {
                ArrayPool<byte>.Shared.Return(_scratch);
                _scratch = null;
            }

            var reader = _reader;
            _reader = null;
            if (reader is not null && _completeReader)
            {
                await reader.CompleteAsync().ConfigureAwait(false);
            }
        }
    }
}
