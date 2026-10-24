using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FixedWidthParser.Readers;

namespace FixedWidthParser.Infrastructure
{
    /// <summary>
    /// Synchronous record enumerator over a stream. A <see langword="struct"/> specialized by
    /// <typeparamref name="TParser"/>, so <c>foreach</c> allocates nothing and parsing devirtualizes.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct RecordEnumerator<TModel, TParser> : IEnumerator<TModel>
        where TModel : allows ref struct
        where TParser : struct, IRecordLineParser<TModel>
    {
        private readonly TParser _parser;
        private readonly FixedWidthContext _context;
        private readonly Func<FixedWidthParseException, bool>? _onParseError;
        private readonly Stream _stream;
        private readonly bool _ownsStream;
        private LineBufferState _lines;
        private bool _disposed;

        internal RecordEnumerator(TParser parser, Stream stream, bool ownsStream, FixedWidthReaderOptions options)
        {
            _parser = parser;
            _context = options.Context;
            _onParseError = options.OnParseError;
            _stream = stream;
            _ownsStream = ownsStream;
            _lines = default;
            _lines.Rent(options);
            _disposed = false;
        }

        /// <summary>The record on the line the enumerator sits on, parsed on access.</summary>
        /// <remarks>
        /// Parsed here rather than by <see cref="MoveNext"/> because the enumerator cannot hold a field of a
        /// <c>ref struct</c> <typeparamref name="TModel"/>. <c>foreach</c> reads it once per line, so each
        /// line is still parsed once; reading it twice parses twice.
        /// </remarks>
        /// <exception cref="FixedWidthParseException">The line does not parse.</exception>
        // AsRef: a parser never keeps a reference to the context, so the record cannot capture this struct.
        public readonly TModel Current => ParseErrors.Parse<TModel, TParser>(_parser, _lines.CurrentLine, _lines.LineNumber, in Unsafe.AsRef(in _context));

        [ExcludeFromCodeCoverage]
        readonly object IEnumerator.Current => throw new NotSupportedException("Use the generic Current.");

        /// <inheritdoc />
        public bool MoveNext()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(RecordEnumerator<TModel, TParser>));
            while (true)
            {
                var status = _lines.TryGetLine(out var line);
                if (status == LineStatus.Line)
                {
                    if (ParseErrors.Accepts<TModel, TParser>(_parser, line, _lines.LineNumber, in _context, _onParseError))
                    {
                        return true;
                    }
                    continue;
                }
                if (status == LineStatus.End)
                {
                    return false;
                }

                _lines.Compact();
                _lines.GrowIfFull();
                _lines.Advance(_stream.Read(_lines.Buffer.AsSpan(_lines.End)));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _lines.Return();
            if (_ownsStream)
            {
#pragma warning disable IDISP007 // Disposed only when the enumerable was told it owns the stream.
                _stream.Dispose();
#pragma warning restore IDISP007
            }
            _disposed = true;
        }

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        public readonly void Reset()
        {
            throw new NotSupportedException("Reading is single-pass.");
        }
    }
}
