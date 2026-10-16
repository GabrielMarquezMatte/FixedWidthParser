# Architecture

A map of how the codebase fits together, not a manual. Start here, then follow the file/type names
into the source: the code is expected to carry its own meaning through naming and flow, so read it
directly rather than looking for a commentary alongside it.

What the code deliberately does *not* carry is the evidence behind a decision — benchmark numbers,
allocation counts, approaches that were tried and rejected. That lives in [`docs/`](docs/README.md),
and [`STYLEGUIDE.md`](STYLEGUIDE.md#comments) says why.

## Two paths over one set of helpers

Every model is parsed and written by one of two paths:

| | Runtime (reflection-built) | Generated |
|---|---|---|
| Model | any class or struct with `[FixedColumn]` members | a `partial` type implementing `IFixedWidthModel<TSelf>` |
| One line | `FixedWidthParser<T>.TryParse`, `FixedWidthWriter<T>.TryFormat` | `FixedWidth.TryParse`/`TryFormat`, calling the generated statics |
| Many lines | `FixedWidthReader<T>`, `FixedWidthWriter<T>` | `FixedWidth.Read*`; writing goes through `TryFormat` |
| Built | once per type, in a static constructor, from compiled expression trees | at compile time by `FixedWidthModelGenerator` |

Both paths call the same helpers in `FixedWidthRuntime` (`src/FixedWidthParser/FixedWidthRuntime.cs`)
for every column: trim, decode a string, parse a number, format a value, pad and align. That class is
public only because generated code lives in the consumer's assembly. Keeping the semantics in one place
is what makes the two paths agree; `SourceGenParityTests` checks that they do.

Column positions are bytes throughout. Lines are never transcoded to UTF-16: a column is a
`ReadOnlySpan<byte>` slice of the line, and only a `string` column, or a type that parses from
characters alone, is decoded with the configured encoding.

## Settings: options and the context

Readers take a `FixedWidthReaderOptions` and the writer a `FixedWidthWriterOptions`. Both reduce to a
`FixedWidthContext` (`src/FixedWidthParser/FixedWidthContext.cs`), a `readonly record struct` passed
by `in` to every parse and format call, generated or not, and to every `IFixedWidthConverter<T>`.

The context is the extension point. `IFixedWidthModel<TSelf>` and `IFixedWidthConverter<T>` are
implemented by users, so a minor version cannot add a member to them; a new setting arrives as a new
property on the context instead. The context also caches what the hot path would otherwise recompute
per column, such as whether the encoding is UTF-8 and the provider's decimal separator.

## Building a runtime layout

`ModelColumns.ForEachColumn` (`src/FixedWidthParser/Attributes/`) walks the `[FixedColumn]` properties,
then fields, and hands each to a visitor; `ColumnLayoutValidator` then rejects a negative start, a
non-positive length and overlapping columns. The parser and the writer share this walk.

- **Parser.** `ParserBuilder` compiles a model factory and, per column, a setter delegate.
  `ColumnParserFactory` wraps each setter with the parse it needs into one `ColumnParser<TModel>`
  closure. Its resolution order — converter, date/time with a `Format`, `string`, `double`/`float`,
  `IUtf8SpanParsable<T>`, then `ISpanParsable<T>` over decoded text — is the order the generator's
  `Classify` mirrors.
- **Writer.** `FixedWidthWriter<T>` compiles a getter per column and wraps it in an
  `IColumnFormatter<TModel>` (`Formatters/ColumnFormatters.cs`): string, `IUtf8SpanFormattable`,
  `ISpanFormattable`, converter, and a nullable wrapper over any of them. `ClassifyWrite` in the
  generator mirrors that choice.

The build runs once per closed generic type in a static constructor. A layout error is captured there
and rethrown from the instance constructor, so the caller sees an `InvalidOperationException` rather
than a `TypeInitializationException` on first use.

`FixedWidthParser<T>` accepts `ref struct` models (`allows ref struct`); the readers do not, since an
enumerator has to hold its current model in a field.

## Reading

```
source -> line splitter -> IRecordLineParser<TModel> -> model
          (bytes in a        (struct strategy:
           pooled buffer)     runtime or generated)
```

Three enumerators share one shape, each specialized by a `struct` parse strategy
(`IRecordLineParser<TModel>`: `ReflectionLineParser<T>` or `GeneratedLineParser<T>`, in
`Readers/ILineParser.cs`). Because the strategy is a `struct` type argument, the JIT compiles one
enumerator per `(TModel, TParser)` pair and the parse call devirtualizes.

| Source | Enumerator | Line splitting |
|---|---|---|
| `Stream`, sync | `RecordEnumerator` (a `struct`, so `foreach` allocates nothing) | `LineBufferState` |
| `Stream`, async | `AsyncRecordEnumerator` | `LineBufferState` |
| `PipeReader` | `PipeRecordEnumerator` | its own scan over `ReadOnlySequence<byte>` |

`LineBufferState` (`Readers/LineBufferState.cs`) owns a pooled buffer: it finds the next `\n`, drops a
preceding `\r`, skips a UTF-8 byte order mark once, skips empty lines while counting them, and
compacts or grows the buffer when a line does not fit. Growth stops at `MaxLineLength`, which is what
bounds memory on a source with no line breaks. The pipe enumerator implements the same rules over
pipe segments, parsing a line in place when it sits in one segment and copying it to a pooled scratch
buffer only when it straddles two. The three must stay identical in behavior; see
[STYLEGUIDE.md § Twins](STYLEGUIDE.md#twins).

The async enumerator keeps the line scan synchronous: `MoveNextAsync` returns a completed
`ValueTask` while the buffer still holds a line, and only `MoveNextSlowAsync` awaits a refill.

A file path is reopened on each enumeration (`FixedWidthRecordEnumerable` and its async sibling); a
stream is read once and disposed unless the caller passed `leaveOpen`.

### Errors

A line that fails to parse costs nothing extra on the lines that succeed. On failure, `ParseErrors`
(`Readers/ParseFailure.cs`) parses the line a second time with a `ParseFailure` sink in the context;
the parser reports the rejecting column, or the short line, through
`FixedWidthContext.ReportFailedColumn`/`ReportShortLine`, which do nothing outside this diagnosis. The
result is a `FixedWidthParseException` naming the line, column, position and raw text, handed to
`OnParseError` to skip the line or thrown.

## Writing

`FixedWidthWriter<T>` formats each record into a fixed-length slot: the line, then the encoded
`NewLine`. `WriteMany` and `WriteManyAsync` fill one pooled 64 KB batch with whole records and write it
to the stream when the next record would not fit, so a write allocates nothing per record. Each line is
filled with spaces first, then every column formats into its own slice.

Per column, `FixedWidthRuntime` picks the cheapest route that keeps the bytes the encoding would
produce:

- `IUtf8SpanFormattable` values (every BCL number and date) format straight into the column's slice
  and are aligned in place. The UTF-8 bytes are kept when the encoding is UTF-8 or they are ASCII;
  otherwise the value is formatted again as characters and encoded.
- A `double` with no `Format` is written as a scaled integer when it has at most four decimals, the
  same bytes as the BCL's shortest round-trip form. Measurements are in
  [writer-performance.md](docs/writer-performance.md).
- A left-aligned `string` under UTF-8 is transcoded straight into the slice; truncation stops at a whole
  character. Other strings are encoded and placed by `ColumnFormatOptions.WriteInto`.
- Everything else formats as characters, is encoded, and is placed by `WriteInto`, which applies
  alignment, padding (keeping a sign ahead of zero padding) and the overflow policy.

## The source generator

`src/FixedWidthParser.Generator/` targets `netstandard2.0` so the .NET Framework compiler host in Visual
Studio can load it. It is an incremental generator: `Extract` turns each candidate type into a
`ModelInfo` of plain, equatable values (`ColumnInfo`, `EquatableArray<T>`), so an edit that does not
change a model's columns does not regenerate it.

`Emit` validates the model — partial, top-level, a supported type for each column, layout, converter
type, overflow, ASCII padding — and reports diagnostics `FWP001`–`FWP012` instead of generating code
for an invalid model. Otherwise it writes `TryParse` and `TryFormat` as straight-line code: one
`FixedWidthRuntime` call per column, with every attribute setting emitted as a constant.

## Further reading

- [`STYLEGUIDE.md`](STYLEGUIDE.md) — code style, and the rules for untrusted input, pooled buffers and twins.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — build expectations and how to submit a change.
- [`SECURITY.md`](SECURITY.md) — supported versions and how to report a vulnerability.
