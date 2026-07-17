using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Readers
{
    /// <summary>
    /// Shared synchronous enumerator logic for character and UTF-8 record readers. The element,
    /// line format, parser, and source strategies are structs, so the JIT specializes every hot-path
    /// call without allocations or interface dispatch.
    /// </summary>
    public struct RecordEnumeratorCore<T, TFormat, TModel, TParser, TSource> : IEnumerator<TModel>
        where T : unmanaged, IEquatable<T>
        where TFormat : struct, ILineFormat<T>
        where TParser : struct, IRecordLineParser<T, TModel>
        where TSource : struct, ISource<T>
    {
        private readonly TParser _strategy;
        private readonly IFormatProvider? _formatProvider;
        private readonly StringPool? _stringPool;
        private TSource _source;
        private LineBufferState<T, TFormat> _lines;
        private TModel _current;
        private bool _disposed;

        internal RecordEnumeratorCore(
            TParser strategy,
            TSource source,
            IFormatProvider? formatProvider,
            StringPool? stringPool,
            int bufferSize)
        {
            _strategy = strategy;
            _source = source;
            _formatProvider = formatProvider;
            _stringPool = stringPool;
            _lines = default;
            _lines.Rent(bufferSize);
            _current = default!;
            _disposed = false;
        }

        /// <inheritdoc />
        public readonly TModel Current => _current;
        [ExcludeFromCodeCoverage]
        readonly object IEnumerator.Current => _current!;

        /// <inheritdoc />
        public bool MoveNext()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(RecordEnumeratorCore<T, TFormat, TModel, TParser, TSource>));
            while (true)
            {
                var status = _lines.TryGetLine(out var line);
                if (status == LineStatus.Line)
                {
                    Parse(line);
                    return true;
                }
                if (status == LineStatus.End)
                {
                    return false;
                }

                Refill();
            }
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

        private void Refill()
        {
            _lines.Compact();
            _lines.GrowIfFull();
            _lines.Advance(_source.Read(_lines.Buffer.AsSpan(_lines.End, _lines.Buffer.Length - _lines.End)));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _lines.Return();
            _source.Dispose();
            _disposed = true;
        }

        [ExcludeFromCodeCoverage]
        /// <inheritdoc />
        public readonly void Reset()
        {
            throw new NotSupportedException("Reading is single-pass.");
        }
    }
}
