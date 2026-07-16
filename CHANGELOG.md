# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `System.IO.Pipelines` integration on the UTF-8 byte path: `FixedWidthByteReader<T>.ReadAsync(PipeReader)`
  (reflection) and `FixedWidthUtf8.ReadAsync<T>(PipeReader)` (source-generated) stream records straight
  off a `PipeReader` via `await foreach`. Lines are sliced from the `ReadOnlySequence<byte>` and parsed in
  place when contiguous, copied into a pooled scratch buffer only when they span segments. Intended for
  sources that are already pipes (Kestrel request bodies, sockets, upstream pipeline stages); for plain
  files/streams the existing `Stream` overloads remain the faster default (see `PipeReaderBenchmarks`).
- `FixedColumnAttribute.Converter`: a per-property custom converter (`IFixedWidthConverter<T>` for the
  `char` path, `IUtf8FixedWidthConverter<T>` for the UTF-8 path — one type can implement both) that
  takes priority over the built-in `ISpanParsable`/`IUtf8SpanParsable` fallback, for both parsing and
  writing. Wired into reflection (parse + write, both element types) and the source generator (parse,
  both element types), with build-time diagnostics `FWP008`/`FWP009` when the converter doesn't
  implement the interface the column's type requires.
- Nullable value-type columns (`int?`, `decimal?`, `DateTime?`, …): a blank (trimmed-empty) column
  parses to `null` without invoking the underlying parser/converter, and `null` writes as a blank
  (padding-filled) column. Works across reflection and generated, char and UTF-8, and composes with
  `FixedColumnAttribute.Converter` (the converter always targets the non-nullable `T`).
- **Source-generated writer**: `IFixedWidthModel<TSelf>` gained a static-abstract `TryFormat(in TSelf,
  Span<char>, IFormatProvider?, out int)`, implemented by the generator for every model declaring the
  `char` marker — reflection-free, AOT-safe writing at parity with the existing generated `TryParse`.
  Mirrors `FixedWidthWriter<TModel>`'s semantics exactly: per-column alignment/padding/format/overflow
  (resolved at compile time into a `ColumnFormatOptions` per column), nullable columns write blank when
  `null`, and `FixedColumnAttribute.Converter` columns format through the same converter instance used
  for parsing. Returns `false` only when the destination span is shorter than the line length; a column
  that doesn't fit throws or truncates per its `Overflow`, same as reflection. New facade
  `FixedWidth.TryFormat<TModel>`. New diagnostic `FWP010` when a column's type is neither `string` nor
  `ISpanFormattable` and has no converter. (UTF-8 byte writing — reflection or generated — remains out of
  scope: no byte writer of either kind exists yet in this library.)
- `FixedColumnAttribute.TrimChar`: the character trimmed from the end of a column when **parsing**
  (previously hardcoded `' '` everywhere — `Padding` was write-only). Threaded through reflection
  (char/byte) and the source generator (char/byte), including the nullable "blank column is null" check
  and the `double`/`float` fast path. On the UTF-8 byte path a non-ASCII `TrimChar` throws
  `NotSupportedException` (reflection: at parser construction; generated: from the column's static
  `__trim{i}` field, surfacing as `TypeInitializationException` on first use) rather than silently
  trimming the wrong byte — mirrors the existing decimal-separator ASCII guard. Unconfigured columns
  (the overwhelming majority) still emit/execute the exact space-trim call they did before; the new
  argument is only added when `TrimChar` differs from the default.

### Changed
- **Breaking:** the internal column formatters `StringColumnFormatter<TModel>` and
  `SpanFormattableColumnFormatter<TModel, TProperty>` are now `internal` (they were unintentionally
  `public`). They are implementation details resolved by the writer; the public extension point is the
  `IColumnFormatter<TModel>` interface. The reader/enumerator strategy types remain `public` because
  they appear in `GetAsyncEnumerator()` return types (a deliberate allocation-free design choice).

### Removed
- **Breaking:** `ColumnParserRegistry` and `Utf8ColumnParserRegistry` (and the `ColumnValueParser<T>`/
  `Utf8ColumnValueParser<T>` delegates they used) are gone. They were process-wide mutable state with a
  documented footgun (registration only took effect if it happened before a model's first parse) and no
  write-side counterpart; `FixedColumnAttribute.Converter` replaces them with a per-column, type-checked
  alternative that also covers writing. The built-in `double`/`float` fast path (csFastFloat) no longer
  goes through a registry lookup — it's now a direct type check in the parser factories, with no
  behavior change for consumers.

### Fixed
- **`double`/`float` columns under a non-'.' decimal separator could silently return a truncated,
  wrong value instead of failing.** csFastFloat's `decimal_separator` override does not fail on
  trailing content it doesn't recognize — it just stops at the first unrecognized character and
  reports success with whatever it parsed up to that point. Under `de-DE`/`pt-BR`-style cultures
  (`.` groups thousands, `,` is the decimal separator), a field like `"1.234,50"` silently became
  `1.0` instead of `1234.50` (or failing outright). The fast path is now only trusted for the
  invariant `'.'` separator, and even then only after confirming the whole (trimmed) field was
  consumed; any other separator falls back to real `NumberFormatInfo`-aware parsing
  (`double.TryParse`/`float.TryParse` with the actual `IFormatProvider`, which validates the whole
  input and understands thousands separators correctly). Applies to reflection and generated, char
  and UTF-8 (the byte path transcodes the — always short — numeric field to a small char buffer
  when it needs the non-fast-path parse).
- The decimal-separator cache was a single-entry memo that thrashed (recomputed on every call) when
  two `IFormatProvider`s were used alternately in the same process (e.g. parsing files in different
  locales). Replaced with a `ConditionalWeakTable` keyed by provider identity — no thrash, and no
  unbounded growth if callers pass many distinct providers (entries are reclaimed with their provider).
- UTF-8 byte parser: a culture whose decimal separator is not a single ASCII character (e.g. the
  Arabic decimal separator U+066B) previously had its separator silently truncated to the wrong
  byte, mis-parsing `double`/`float` columns. Such cultures now throw a clear `NotSupportedException`
  on the byte path; use the `char`-based parser for them. ASCII separators (`.`/`,`) are unaffected.
- Writer: a value whose `ISpanFormattable.TryFormat` never succeeded could overflow the formatter's
  buffer-growth counter into a negative/huge `ArrayPool` rent. The grow loop is now bounded (1M chars)
  and throws a clear `InvalidOperationException` naming the column instead.

### Documentation
- Corrected the UTF-8 byte path docs: `string` columns **are** interned through a supplied
  `StringPool` (identical to the `char` path); the previous "no pooling" note was inaccurate.
- Documented that empty lines are skipped (counted but not yielded) while a non-empty line shorter
  than the declared layout is treated as malformed and throws.
- Reconciled the README's stated requirements with the actual multi-target: the package builds for
  `net8.0` and `net10.0`, not ".NET 10" only. Added an explicit note (in Requirements and in the
  `ref struct` Models section) that `ref struct` model support needs .NET 9+, since the `allows ref
  struct` generic constraint it depends on doesn't exist on `net8.0`.

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
  layouts and unsupported column types. Shipped inside the package as an analyzer — no extra package
  required.

[Unreleased]: https://github.com/GabrielMarquezMatte/FixedWidthParser/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/GabrielMarquezMatte/FixedWidthParser/releases/tag/v1.0.0
