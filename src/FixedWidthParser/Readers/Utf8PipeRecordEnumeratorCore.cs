using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// <see cref="PipeReader"/>-based counterpart of
    /// <see cref="AsyncRecordEnumeratorCore{T, TFormat, TModel, TParser, TSource}"/>: parses fixed-width records from a
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

        // Stateful PipeReader tracking (PERF-B)
        private ReadResult _currentResult;
        private bool _hasResult;
        private SequencePosition _consumed;
        private SequencePosition _examined;
        private long _scannedBytes;

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
            ObjectDisposedException.ThrowIf(_reader is null, nameof(Utf8PipeRecordEnumeratorCore<,>));
            var reader = _reader;

            try
            {
                while (true)
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    if (!_hasResult)
                    {
                        _currentResult = await reader.ReadAsync(_cancellationToken).ConfigureAwait(false);
                        if (_currentResult.IsCanceled)
                        {
                            throw new OperationCanceledException(_cancellationToken);
                        }
                        _hasResult = true;

                        // Reset positions to point to the start of the new buffer (which corresponds to previous _consumed)
                        _consumed = _currentResult.Buffer.Start;
                        _examined = _currentResult.Buffer.Start;
                    }

                    var status = TryReadFromResult(out _consumed, out _examined);

                    if (status == LineStatus.Line)
                    {
                        return true;
                    }

                    // Before reading more data or returning false at the end, advance the reader
                    reader.AdvanceTo(_consumed, _examined);
                    _hasResult = false;

                    if (status == LineStatus.End || _currentResult.IsCompleted)
                    {
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                if (_hasResult)
                {
                    reader.AdvanceTo(_consumed, _examined);
                    _hasResult = false;
                }
                throw;
            }
        }

        private LineStatus TryReadFromResult(out SequencePosition consumed, out SequencePosition examined)
        {
            ReadOnlySequence<byte> buffer = _currentResult.Buffer;
            bool isCompleted = _currentResult.IsCompleted;

            if (!_preambleChecked)
            {
                if (buffer.Length >= 3)
                {
                    Span<byte> head = stackalloc byte[3];
                    buffer.Slice(0, 3).CopyTo(head);
                    int skip = head is [0xEF, 0xBB, 0xBF] ? 3 : 0;
                    if (skip > 0)
                    {
                        _consumed = buffer.GetPosition(skip);
                    }
                    _preambleChecked = true;
                }
                else if (isCompleted)
                {
                    _preambleChecked = true; // too short to hold a BOM; no preamble
                }
                else
                {
                    consumed = _consumed;
                    examined = buffer.End;
                    return LineStatus.NeedData;
                }
            }

            while (true)
            {
                ReadOnlySequence<byte> remaining = buffer.Slice(_consumed);
                var scanStart = remaining.GetPosition(_scannedBytes);
                var seqReader = new SequenceReader<byte>(remaining.Slice(scanStart));

                if (seqReader.TryReadTo(out ReadOnlySpan<byte> _, Lf, advancePastDelimiter: true))
                {
                    _lineNumber++;
                    var positionOfLf = remaining.GetPosition(seqReader.Consumed - 1, scanStart);
                    var scopeLine = TrimTrailingCr(remaining.Slice(0, positionOfLf));

                    var nextPosition = remaining.GetPosition(seqReader.Consumed, scanStart);
                    _consumed = nextPosition;
                    _examined = nextPosition;
                    _scannedBytes = 0;

                    if (!scopeLine.IsEmpty)
                    {
                        Parse(in scopeLine);
                        consumed = _consumed;
                        examined = _consumed;
                        return LineStatus.Line;
                    }
                    continue;
                }

                if (!isCompleted)
                {
                    _scannedBytes = remaining.Length;
                    consumed = _consumed;
                    examined = buffer.End;
                    return LineStatus.NeedData;
                }

                consumed = buffer.End;
                examined = buffer.End;
                if (remaining.Length <= 0)
                {
                    _consumed = buffer.End;
                    _examined = buffer.End;
                    _scannedBytes = 0;
                    return LineStatus.End;
                }
                _lineNumber++;
                var line = TrimTrailingCr(in remaining);
                if (!line.IsEmpty)
                {
                    _consumed = buffer.End;
                    _examined = buffer.End;
                    _scannedBytes = 0;
                    Parse(in line);
                    return LineStatus.Line;
                }
                _consumed = buffer.End;
                _examined = buffer.End;
                _scannedBytes = 0;
                return LineStatus.End;
            }
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
                    var oldScratch = _scratch;
                    _scratch = null;
                    ArrayPool<byte>.Shared.Return(oldScratch);
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
            byte last = line.Slice(length - 1).FirstSpan[0];
            return last == Cr ? line.Slice(0, length - 1) : line;
        }

        public async ValueTask DisposeAsync()
        {
            if (_scratch is not null)
            {
                var oldScratch = _scratch;
                _scratch = null;
                ArrayPool<byte>.Shared.Return(oldScratch);
            }

            var reader = _reader;
            _reader = null;
            if (reader is null)
            {
                return;
            }
            if (_hasResult)
            {
                reader.AdvanceTo(_consumed, _examined);
                _hasResult = false;
            }

            if (_completeReader)
            {
                await reader.CompleteAsync().ConfigureAwait(false);
            }
        }
    }
}
