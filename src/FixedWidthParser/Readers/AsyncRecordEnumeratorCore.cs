using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Shared asynchronous enumerator logic for character and UTF-8 record readers. The span scan
    /// remains synchronous; only the source refill awaits a <see cref="Memory{T}"/>.
    /// </summary>
    public sealed class AsyncRecordEnumeratorCore<T, TFormat, TModel, TParser, TSource> : IAsyncEnumerator<TModel>
        where T : unmanaged, IEquatable<T>
        where TFormat : struct, ILineFormat<T>
        where TParser : struct, IRecordLineParser<T, TModel>
        where TSource : struct, ISource<T>
    {
        private readonly TParser _strategy;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private readonly CancellationToken _cancellationToken;
        private TSource _source;
        private LineBufferState<T, TFormat> _lines;
        private TModel _current;
        private bool _disposed;

        internal AsyncRecordEnumeratorCore(
            TParser strategy,
            TSource source,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            _strategy = strategy;
            _source = source;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _cancellationToken = cancellationToken;
            _lines = default;
            _lines.Rent(bufferSize);
            _current = default!;
            _disposed = false;
        }

        /// <inheritdoc />
        public TModel Current => _current;

        /// <inheritdoc />
        public ValueTask<bool> MoveNextAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(AsyncRecordEnumeratorCore<T, TFormat, TModel, TParser, TSource>));
            _cancellationToken.ThrowIfCancellationRequested();
            var result = TryReadFromBuffer();
            if (result == LineStatus.Line)
            {
                return new ValueTask<bool>(true);
            }
            if (result == LineStatus.End)
            {
                return new ValueTask<bool>(false);
            }

            return MoveNextSlowAsync();
        }

        private async ValueTask<bool> MoveNextSlowAsync()
        {
            while (true)
            {
                PrepareRefill();
                _lines.Advance(await _source
                    .ReadAsync(_lines.Buffer.AsMemory(_lines.End, _lines.Buffer.Length - _lines.End), _cancellationToken)
                    .ConfigureAwait(false));
                _cancellationToken.ThrowIfCancellationRequested();

                var result = TryReadFromBuffer();
                if (result == LineStatus.Line)
                {
                    return true;
                }
                if (result == LineStatus.End)
                {
                    return false;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LineStatus TryReadFromBuffer()
        {
            var status = _lines.TryGetLine(out var line);
            if (status == LineStatus.Line)
            {
                Parse(line);
            }
            return status;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Parse(ReadOnlySpan<T> line)
        {
            if (!_strategy.TryParse(line, _formatProvider, _stringPool, out _current))
            {
                throw new FormatException(
                    $"Line {_lines.LineNumber} could not be parsed into {typeof(TModel).Name}: \"{TFormat.FormatForException(line)}\".");
            }
        }

        private void PrepareRefill()
        {
            _lines.Compact();
            _lines.GrowIfFull();
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _lines.Return();
                _source.Dispose();
                _disposed = true;
            }
            return ValueTask.CompletedTask;
        }
    }
}
