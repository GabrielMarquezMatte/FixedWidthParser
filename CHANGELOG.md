# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0]

Not released yet.

2.0 reads and writes **bytes only**: one parser, one reader, one writer and one generated
`TryParse`/`TryFormat`, where 1.x had a `char` and a UTF-8 version of each. Column positions are
bytes; text columns are decoded with an ASCII-compatible encoding, UTF-8 by default. Settings travel
in options objects and a `FixedWidthContext`, so later versions can add to them without another break.
2.0 targets `net10.0` only.

### Migrating from 1.x

| 1.x | 2.0 |
|---|---|
| `FixedWidthReader<T>` over a `TextReader`, `FixedWidthByteReader<T>` | `FixedWidthReader<T>` over a `Stream`, a file or a `PipeReader` |
| `FixedWidthUtf8.Read*`, `FixedWidth.Read*` over a `TextReader` | `FixedWidth.Read*` over a `Stream`, a file or a `PipeReader` |
| `new FixedWidthReader<T>(provider, pool, bufferSize)` | `new FixedWidthReader<T>(new FixedWidthReaderOptions { FormatProvider = …, StringPool = …, BufferSize = … })` |
| `FixedWidth.Read<T>(stream, encoding, leaveOpen, provider, pool, bufferSize)` | `FixedWidth.Read<T>(stream, leaveOpen, options)`; the encoding is `FixedWidthReaderOptions.Encoding` |
| `FixedWidthParser<T>`, `Utf8FixedWidthParser<T>` `.TryParse(line, provider, pool, out m)` | `FixedWidthParser<T>.TryParse(ReadOnlySpan<byte> line, in FixedWidthContext context, out m)` |
| `FixedWidth.TryParse<T>(chars, provider, pool, out m)`, `FixedWidthUtf8.TryParse<T>(bytes, …)` | `FixedWidth.TryParse<T>(bytes, in FixedWidthContext context, out m)` |
| `FixedWidth.TryFormat<T>(in m, Span<char>, provider, out n)` | `FixedWidth.TryFormat<T>(in m, Span<byte>, in FixedWidthContext context, out n)` |
| `IFixedWidthModel<T>` + `IUtf8FixedWidthModel<T>` | `IFixedWidthModel<T>`, whose generated members take bytes and a context |
| `IFixedWidthConverter<T>` + `IUtf8FixedWidthConverter<T>` with `IFormatProvider?` | `IFixedWidthConverter<T>` over bytes, with `in FixedWidthContext` |
| `FixedWidthWriter<T>.Write*(StreamWriter or Stream, …, provider)` | `new FixedWidthWriter<T>(new FixedWidthWriterOptions { FormatProvider = …, Encoding = …, NewLine = … })`, then `Write*(Stream, …)` |
| `catch (FormatException)` on a bad line | still works; the exception is now `FixedWidthParseException` |

A UTF-8 file with multi-byte text whose layout counts characters no longer lines up: positions are
bytes. Read files written in a single-byte encoding with that encoding (`Encoding.Latin1`, or
Windows-1252 after registering `CodePagesEncodingProvider`).

### Added
- Parallel aggregation: `FixedWidthReader<T>.AggregateFileAsync` and `FixedWidth.AggregateFileAsync`
  read a file in byte-range partitions on several threads and fold each into an
  `IRecordAccumulator<TSelf, TModel>`, merged in file order. Every line is parsed exactly once, and
  errors carry the same line number as a sequential read. `FixedWidthParallelOptions.DegreeOfParallelism`
  sets the thread count.
- `FixedWidthReaderOptions`: `FormatProvider`, `Encoding`, `StringPool`, `BufferSize`,
  `MaxLineLength` and `OnParseError`, taken by every read. `FixedWidthWriterOptions`: `FormatProvider`,
  `Encoding` and `NewLine`.
- `FixedWidthContext`, passed by `in` reference to generated code and converters: the format provider,
  the encoding and the string pool for one line. New settings arrive as new properties.
- `FixedWidthParseException`, a `FormatException` carrying `LineNumber`, `ColumnName`, `ColumnStart`,
  `ColumnLength` and `RawValue`. A short line says how long it is and how long the layout needs it to
  be. A line that parses pays nothing for this: a failed line is parsed again to find the column.
- `FixedWidthReaderOptions.OnParseError`: return `true` to skip a line that fails to parse.
- `FixedWidthReaderOptions.MaxLineLength` (default 1 MiB): a longer line throws
  `InvalidDataException`. Before, a source without line breaks doubled the read buffer until memory ran
  out, which a `PipeReader` over a request body made reachable from the network.
- Encodings other than UTF-8 for text columns, dates parsed with a format and types that parse only
  from characters. A UTF-8 byte order mark is skipped only when the encoding is UTF-8.
- Columns of any `ISpanParsable<T>` type without a UTF-8 parser, decoded first (they used to be
  refused on the byte path).
- `FixedWidthWriter<T>.TryFormat(in model, Span<byte>, out int)`.
- `FixedColumnAttribute.Converter`: a per-property `IFixedWidthConverter<T>` that takes priority over the
  built-in parsing and formatting, with diagnostic `FWP008` when it targets the wrong type.
- Nullable value-type columns (`int?`, `decimal?`, `DateTime?`, …): a blank column parses to `null`
  without invoking the parser or converter, and `null` writes as a blank column.
- The source-generated writer: `IFixedWidthModel<TSelf>.TryFormat`, with the same alignment, padding,
  format, overflow, nullable and converter handling as `FixedWidthWriter<T>`, and diagnostic `FWP010`
  when a column's type cannot be written.
- `FixedColumnAttribute.TrimChar` and `TrimMode`: what to strip from a column when parsing, and from
  which side.
- `FixedColumnAttribute.Format` parses `DateTime`, `DateOnly`, `TimeOnly` and `DateTimeOffset` with
  that exact format.
- Diagnostic `FWP012`: `Padding` or `TrimChar` is not ASCII. The runtime parser and writer throw
  `InvalidOperationException` for the same models.
- Public API tracking (`PublicAPI.*.txt`) and package validation against the last release, run by CI on
  every pull request.

### Changed
- **Breaking:** bytes only; see the table above. The column positions of a model are bytes.
- **Breaking:** targets `net10.0` only. .NET 8 reaches end of support on 10 November 2026.
- **Breaking:** a `null` format provider means the invariant culture for every column type, on parse and
  write. 1.x used it for `double`/`float` only and fell back to `CurrentCulture` for the rest.
- A culture whose decimal separator is not one byte (U+066B, for example) now parses, where 1.1 threw
  `NotSupportedException` on the byte path.
- File-backed readers open the `FileStream` with `FileOptions.SequentialScan` and no internal buffer of
  their own, since the reader already buffers. The default buffer is 65,536 bytes.
- The writer batches records into one pooled buffer and writes bytes straight to the stream, instead of
  going through a `StreamWriter`.
- The writer formats `IUtf8SpanFormattable` columns (every BCL number and date) straight into the line,
  and transcodes left-aligned string columns straight into it when the encoding is UTF-8. A formatted
  value that is not ASCII under another encoding still goes through that encoding.
- A `double` column with no `Format`, at most four decimals and a magnitude under 1e9 is written as a
  scaled integer, about 5x faster, with the same bytes. A line of a string, an `int` and a `double`
  writes about 2x faster.
- The generator targets `netstandard2.0`, so Visual Studio's .NET Framework compiler host can load it.
- The library and generator build with warnings as errors.

### Removed
- **Breaking:** `FixedWidthUtf8`, `FixedWidthByteReader<T>`, `Utf8FixedWidthParser<T>`,
  `IUtf8FixedWidthModel<T>`, `IUtf8FixedWidthConverter<T>`, `Utf8FixedWidthRuntime` and the `TextReader`
  overloads. Diagnostics `FWP007` and `FWP009`, which only applied to the second path, are retired.
- **Breaking:** `ColumnParserRegistry` and `Utf8ColumnParserRegistry` (and their value-parser delegates):
  process-wide mutable state that only took effect before a model's first parse.
  `FixedColumnAttribute.Converter` replaces them.
- **Breaking:** types that were public without a public use: `ColumnParser<T>`, `Utf8ColumnParser<T>`,
  `RefAction`, `RefGetter`, `IColumnFormatter<T>`, `ColumnLayoutValidator`, `ISource<T>`, `TextReaderSource`, `StreamSource`,
  `ILineFormat<T>` and the line-format structs. The enumerator and strategy types that stay public
  (they appear in return types, so `foreach` allocates nothing) lost their element-type and source
  type parameters.

### Fixed
- Right-aligned `double`/`float` columns could fail to parse under the invariant fast path: csFastFloat
  did not consistently count skipped leading whitespace.
- `double`/`float` columns with a thousands separator under a `'.'` culture (`"1,234.56"`) failed
  instead of parsing.
- `double`/`float` columns under a `','` culture could silently return a truncated value: `"1.234,50"`
  became `1.0`. The fast path is now trusted only for `'.'`, and only when it consumes the whole field.
- The decimal-separator cache thrashed when two format providers alternated.

## [1.1.1]

### Added
- Targets `net8.0` alongside `net10.0`. `ref struct` models need .NET 9 or later, so they are
  `net10.0`-only.

## [1.1.0]

### Added
- `System.IO.Pipelines` integration on the UTF-8 byte path: `FixedWidthByteReader<T>.ReadAsync(PipeReader)`
  and `FixedWidthUtf8.ReadAsync<T>(PipeReader)` stream records off a `PipeReader` via `await foreach`.

### Changed
- **Breaking:** the column formatters `StringColumnFormatter<TModel>` and
  `SpanFormattableColumnFormatter<TModel, TProperty>` became `internal`; they were unintentionally
  `public`.

### Fixed
- UTF-8 byte parser: a culture whose decimal separator is not a single ASCII character had its separator
  silently truncated to the wrong byte; it now throws `NotSupportedException`.
- Writer: a value whose `ISpanFormattable.TryFormat` never succeeded could overflow the buffer-growth
  counter into a negative rent; the loop is bounded and throws a clear error naming the column.

### Documentation
- `string` columns on the UTF-8 path are interned through a supplied `StringPool`, like the char path.
- Empty lines are skipped (counted but not yielded); a non-empty line shorter than the layout throws.

## [1.0.0]

Initial release.

### Added
- Attribute-driven column mapping (`[FixedColumn(start, length)]`) on properties and public fields.
- Parsing: single line plus lazy batch reading from a `TextReader`, `Stream` or file —
  synchronous (`IEnumerable<T>` with an allocation-free struct enumerator) and asynchronous
  (`IAsyncEnumerable<T>`), without allocating a string per line.
- Writing: single record and batches, synchronous and asynchronous, with `StreamWriter` reuse and
  `ReadOnlySpan<T>` overloads for zero-allocation output.
- Configurable per-column formatting: alignment, padding character, format string and an explicit
  overflow policy (no silent data loss).
- Culture-aware parsing/formatting for numeric and `ISpanParsable`/`ISpanFormattable` types
  (including fast `double`/`float` via csFastFloat).
- Layout validation at construction: rejects negative `Start`, non-positive `Length` and
  overlapping columns with a clear error.
- `ref struct` model support on the parser.
- A bundled Roslyn source generator: models implementing `IFixedWidthModel<TSelf>` get a
  reflection-free `TryParse` generated at compile time, with diagnostics (FWP001-FWP007) for invalid
  layouts and unsupported column types.

[2.0.0]: https://github.com/GabrielMarquezMatte/FixedWidthParser/compare/v1.1.1...HEAD
[1.1.1]: https://github.com/GabrielMarquezMatte/FixedWidthParser/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/GabrielMarquezMatte/FixedWidthParser/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/GabrielMarquezMatte/FixedWidthParser/releases/tag/v1.0.0
