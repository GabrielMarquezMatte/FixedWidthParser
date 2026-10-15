using System.Buffers;
using System.IO.Pipelines;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// <see cref="PipeReader"/>-based counterpart of <see cref="AsyncRecordEnumerator{TModel, TParser}"/>:
    /// lets the pipe own buffering and read-ahead. A line is parsed in place when it sits in a single
    /// segment, and copied into a pooled scratch buffer only when it straddles segments. Line semantics
    /// match the stream path: a UTF-8 byte order mark is skipped once, lines split on <c>\n</c> (a
    /// preceding <c>\r</c> is dropped), empty lines are counted but not yielded, and a final line without
    /// a line break is yielded.
    /// </summary>
    public sealed class PipeRecordEnumerator<TModel, TParser> : IAsyncEnumerator<TModel>
        where TParser : struct, IRecordLineParser<TModel>
    {
        private const byte Cr = (byte)'\r';
        private const byte Lf = (byte)'\n';

        private readonly TParser _parser;
        private readonly bool _completeReader;
        private readonly FixedWidthContext _context;
        private readonly Func<FixedWidthParseException, bool>? _onParseError;
        private readonly int _maxLineLength;
        private readonly CancellationToken _cancellationToken;
        private PipeReader? _reader;
        private byte[]? _scratch;
        private long _lineNumber;
        private bool _preambleChecked;
        private TModel _current;

        private ReadResult _currentResult;
        private bool _hasResult;
        private SequencePosition _consumed;
        private SequencePosition _examined;
        private long _scannedBytes;

        internal PipeRecordEnumerator(TParser parser, PipeReader reader, bool completeReader, FixedWidthReaderOptions options, CancellationToken cancellationToken)
        {
            _parser = parser;
            _reader = reader;
            _completeReader = completeReader;
            _context = options.Context;
            _onParseError = options.OnParseError;
            _maxLineLength = options.MaxLineLength;
            _preambleChecked = !options.SkipsByteOrderMark;
            _cancellationToken = cancellationToken;
            _current = default!;
        }

        /// <inheritdoc />
        public TModel Current => _current;

        /// <inheritdoc />
        public async ValueTask<bool> MoveNextAsync()
        {
            ObjectDisposedException.ThrowIf(_reader is null, nameof(PipeRecordEnumerator<,>));
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
                        _consumed = _currentResult.Buffer.Start;
                        _examined = _currentResult.Buffer.Start;
                    }

                    var status = TryReadFromResult(out _consumed, out _examined);
                    if (status == LineStatus.Line)
                    {
                        return true;
                    }

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
                    if (head is [0xEF, 0xBB, 0xBF])
                    {
                        _consumed = buffer.GetPosition(3);
                    }
                    _preambleChecked = true;
                }
                else if (isCompleted)
                {
                    _preambleChecked = true;
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
                    var line = TrimTrailingCr(remaining.Slice(0, positionOfLf));
                    var nextPosition = remaining.GetPosition(seqReader.Consumed, scanStart);
                    _consumed = nextPosition;
                    _examined = nextPosition;
                    _scannedBytes = 0;

                    if (!line.IsEmpty && Parse(in line))
                    {
                        consumed = _consumed;
                        examined = _consumed;
                        return LineStatus.Line;
                    }
                    continue;
                }

                if (!isCompleted)
                {
                    // An unterminated line longer than the limit (+1 for its '\r') can never become valid.
                    if (remaining.Length > _maxLineLength + 1L)
                    {
                        throw ParseErrors.LineTooLong(_lineNumber + 1, _maxLineLength);
                    }
                    _scannedBytes = remaining.Length;
                    consumed = _consumed;
                    examined = buffer.End;
                    return LineStatus.NeedData;
                }

                consumed = buffer.End;
                examined = buffer.End;
                _consumed = buffer.End;
                _examined = buffer.End;
                _scannedBytes = 0;
                if (remaining.Length <= 0)
                {
                    return LineStatus.End;
                }
                _lineNumber++;
                var last = TrimTrailingCr(in remaining);
                return !last.IsEmpty && Parse(in last) ? LineStatus.Line : LineStatus.End;
            }
        }

        // Returns false when the line failed to parse and OnParseError asked to skip it.
        private bool Parse(in ReadOnlySequence<byte> line)
        {
            if (line.Length > _maxLineLength)
            {
                throw ParseErrors.LineTooLong(_lineNumber, _maxLineLength);
            }
            if (line.IsSingleSegment)
            {
                return ParseSpan(line.FirstSpan);
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
            return ParseSpan(_scratch.AsSpan(0, length));
        }

        private bool ParseSpan(ReadOnlySpan<byte> line)
        {
            if (_parser.TryParse(line, in _context, out _current))
            {
                return true;
            }
            ParseErrors.HandleFailure<TModel, TParser>(_parser, line, _lineNumber, in _context, _onParseError);
            return false;
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

        /// <inheritdoc />
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
