# Architecture

A map of how the codebase fits together, not a manual. Start here, then follow the file/type names
into the source: the code is expected to carry its own meaning through naming and flow, so read it
directly rather than looking for a commentary alongside it.

What the code deliberately does *not* carry is the evidence behind a decision — benchmark numbers,
allocation counts, approaches that were tried and rejected. That lives in [`docs/`](docs/README.md),
and [`STYLEGUIDE.md`](STYLEGUIDE.md#comments) says why.

## Two paths over one set of helpers

Every model is parsed and written by one of two paths, both behind the static `FixedWidth` class
(`src/FixedWidthParser/FixedWidth.cs`), where each method has one overload per path:

| | Layout | Generated |
|---|---|---|
| Model | any type, through a `FixedWidthLayout<T>` from `[FixedColumn]` attributes or from `FixedWidthLayout.Build` | a `partial` type implementing `IFixedWidthModel<TSelf>` |
| Call | `FixedWidth.X(…, layout, …)` | `FixedWidth.X<T>(…)`, calling the generated statics |
| Built | when the layout is built: by reflection and compiled expression trees for attributes, from delegates for `Build` | at compile time by `FixedWidthModelGenerator` |

Both paths call the same helpers in `FixedWidthRuntime` (`src/FixedWidthParser/Infrastructure/FixedWidthRuntime.cs`)
for every column: trim, decode a string, parse a number, format a value, pad and align. That class is
public only because generated code lives in the consumer's assembly. Keeping the semantics in one place
is what makes the two paths agree; `SourceGenParityTests` checks that they do.

Column positions are bytes throughout. Lines are never transcoded to UTF-16: a column is a
`ReadOnlySpan<byte>` slice of the line, and only a `string` column, or a type that parses from
characters alone, is decoded with the configured encoding.

## Settings: options and the context

Reads take a `FixedWidthReaderOptions` and writes a `FixedWidthWriterOptions`, validated on each call. Both reduce to a
`FixedWidthContext` (`src/FixedWidthParser/FixedWidthContext.cs`), a `readonly record struct` passed
by `in` to every parse and format call, generated or not, and to every `IFixedWidthConverter<T>`.

The context is the extension point. `IFixedWidthModel<TSelf>` and `IFixedWidthConverter<T>` are
implemented by users, so a minor version cannot add a member to them; a new setting arrives as a new
property on the context instead. The context also caches what the hot path would otherwise recompute
per column, such as whether the encoding is UTF-8 and the provider's decimal separator.

## The layout object

A `FixedWidthLayout<T>` (`src/FixedWidthParser/Layouts/`) holds, per column, a `ColumnParserInfo<T>`
(start, length, name and a `ColumnParser<T>` closure that parses and assigns) and an
`IColumnFormatter<T>` (`Formatters/ColumnFormatters.cs`), plus the line length and the model factory.
It is immutable, so one instance serves every read, write and worker thread. A layout that lacks the
parsers or the formatters reads or writes nothing; `EnsureCanRead`/`EnsureCanWrite` throw before the
first line.

Two sources fill it, and both reach the same parse and format closures:

- **`FromAttributes<T>()`.** `ModelColumns.ForEachColumn` (`Attributes/`) walks the `[FixedColumn]`
  properties, then fields; `ColumnLayoutValidator` rejects a negative start, a non-positive length and
  overlapping columns. `ParserBuilder` compiles a model factory and a setter per column, and
  `ColumnParserFactory` picks the parse for the member's runtime `Type` through `MakeGenericMethod`.
  `FormatterBuilder` compiles a getter per column and picks its formatter the same way. The result is
  cached once per `T` in a static constructor. The read side and the write side build independently:
  a model only one side can handle (a column type that formats but does not parse, say) still gets a
  layout, and the other side's error is rethrown on its first use. Only an error both sides hit, such
  as an overlap, throws from `FromAttributes`.
- **`Build<T>(configure)`.** `FixedWidthLayoutBuilder<T>` receives a `ColumnGetter`/`ColumnSetter`
  pair per column, already typed, so it selects the parse and the formatter by generic constraints
  (`Column<T>` for UTF-8 parsable types, `DecodedColumn<T>`, `NullableColumn<T>`, `ConvertedColumn<T>`)
  and `typeof(T) ==` checks the JIT folds. No reflection: this is the trimming- and AOT-safe source,
  which `tests/AotSmoke` publishes with NativeAOT to prove.

`ColumnParserFactory`'s resolution order — converter, date/time with a `Format`, `string`,
`double`/`float`, `IUtf8SpanParsable<T>`, then `ISpanParsable<T>` over decoded text — is shared by both
sources (`TryBuildSpecial`) and mirrored by the generator's `Classify`; the formatter choice (converter,
string, `IUtf8SpanFormattable`, `ISpanFormattable`, a nullable wrapper) is mirrored by `ClassifyWrite`.
Column options are validated by `FormatterBuilder.ResolveOptions` for both.

A layout accepts `ref struct` models (`allows ref struct`) for `TryParse`, `TryFormat`, the readers and
the aggregates; an attribute layout over a `ref struct` does not write. An enumerator cannot hold a
`ref struct` in a field, so `MoveNext` only finds the next line and `Current`
parses it on access; `foreach` reads `Current` once, so each line is still parsed once. With
`OnParseError` set, `MoveNext` parses the line too, to skip a bad one before yielding it.

## Reading

```
source -> line splitter -> IRecordLineParser<TModel> -> model
          (bytes in a        (struct strategy:
           pooled buffer)     runtime or generated)
```

Two enumerators share one shape, each specialized by a `struct` parse strategy
(`IRecordLineParser<TModel>`: `LayoutLineParser<T>` or `GeneratedLineParser<T>`, in
`Infrastructure/RecordLineParsers.cs`). Because the strategy is a `struct` type argument, the JIT compiles one
enumerator per `(TModel, TParser)` pair and the parse call devirtualizes.

| Source | Enumerator |
|---|---|
| `Stream` or file, sync | `RecordEnumerator` (a `struct`, so `foreach` allocates nothing) |
| `Stream` or file, async | `AsyncRecordEnumerator` |

Every reader, the parallel ones included, splits lines with `LineBufferState`
(`Readers/LineBufferState.cs`). It owns a pooled buffer: it finds the next `\n`, drops a preceding
`\r`, skips a UTF-8 byte order mark once, skips empty lines while counting them, and compacts or grows
the buffer when a line does not fit. Growth stops at `MaxLineLength`, which is what bounds memory on a
source with no line breaks. Keeping one splitter is deliberate: a `PipeReader` reader with its own
splitter over `ReadOnlySequence<byte>` was removed because it duplicated these rules and read slower
than `PipeReader.AsStream()` ([pipe-reader.md](docs/pipe-reader.md)).

The async enumerator keeps the line scan synchronous: `MoveNextAsync` returns a completed
`ValueTask` while the buffer still holds a line, and only `MoveNextSlowAsync` awaits a refill.

A file path is reopened on each enumeration (`FixedWidthRecordEnumerable` and its async sibling); a
stream is read once and disposed unless the caller passed `leaveOpen`.

### Parallel aggregation

`ParallelAggregator` (`Readers/ParallelAggregator.cs`) backs `AggregateFileAsync` on both paths, and the
native library's reads. It reads an `IRandomAccessSource` (a file handle, or native memory), cuts
`[dataStart, length)` into byte ranges (four per worker, 1 MB to 64 MB each), and lets workers pull ranges
from a shared counter. Each worker reads its range through its own `LineBufferState` into its own
accumulator — or, for the native library, its own `ILineSink` — and the caller merges them in range order
and throws the first failure in file order. The accumulator and sink loops are twins on purpose: folding
the accumulator into a generic sink cost one-call reads about 40% under dynamic PGO.

A line belongs to the range its first byte falls in, the byte after the previous line break, so a
byte order mark belongs to line 1. A worker starts one byte before its range, skips the line it lands
inside, and stops once the next line would start past its range. Fixed-width lines have no quoting,
so unlike ExcelReader's CSV engine there is no guessed start to confirm or re-read: every line is
parsed exactly once. A single range is the sequential read, which is how `DegreeOfParallelism = 1` and
small files run, so there is no separate sequential twin.

A worker does not know how many lines precede its range. It counts them only when a line fails —
line breaks from the start of the file up to the failing line, continued from there for later failures
in the same range — so errors carry the sequential line number and the happy path pays nothing.

A `Stream` cannot be read at an offset, so `ParallelStreamAggregator` follows ExcelReader's XLSX
pipeline instead:

```
source -> producer (1 thread) -> bounded queue -> N workers -> ordered merge
          256 KB pooled chunks,    chunk, index,     parse + Add     by chunk index
          cut after the last '\n'  lines before
```

The producer carries the bytes after the last line break into the next chunk and counts the line
breaks it hands over, so each chunk knows the number of its first line and workers report sequential
line numbers directly. The queue holds about two chunks per worker, and results are merged in chunk
order as soon as every earlier chunk is merged. Each chunk holds one of four slots per worker from the
time it is read until it is merged, so a slow early chunk stalls the producer instead of letting later
results pile up: chunks and unmerged accumulators stay bounded whatever the source's size. A line longer than a chunk grows the chunk, up to `MaxLineLength`. A failure in the
producer lies past every chunk it emitted, so a worker's failure is thrown first. With
`DegreeOfParallelism = 1` the stream is read by the ordinary async enumerator.

Measurements are in [parallel-aggregate.md](docs/parallel-aggregate.md).

### Errors

A line that fails to parse costs nothing extra on the lines that succeed. On failure, `ParseErrors`
(`Readers/ParseFailure.cs`) parses the line a second time with a `ParseFailure` sink in the context;
the parser reports the rejecting column, or the short line, through
`FixedWidthContext.ReportFailedColumn`/`ReportShortLine`, which do nothing outside this diagnosis. The
result is a `FixedWidthParseException` naming the line, column, position and raw text, handed to
`OnParseError` to skip the line or thrown.

## Writing

`RecordWriter` (`Writers/RecordWriter.cs`) writes for both paths, specialized like the readers by a
`struct` strategy (`IRecordFormatter<T>`: `LayoutRecordFormatter<T>` or `GeneratedRecordFormatter<T>`,
in `Writers/RecordFormatters.cs`). Each record is formatted, followed by the encoded `NewLine`, into one
pooled 64 KB batch. A generated model does not declare its line length, so the batch does not reserve
a slot: a record that does not fit the rest of the batch flushes it, and one that does not fit an empty
batch grows it. A write allocates nothing per record. A layout line is filled with spaces first, then
every column formats into its own slice.

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
type, overflow, ASCII padding — and reports diagnostics `FWP001`–`FWP014` (`FWP007` and `FWP009` are
retired) instead of generating code
for an invalid model. Otherwise it writes `TryParse` and `TryFormat` as straight-line code: one
`FixedWidthRuntime` call per column. Each column's settings become two `static readonly` fields, a
`ColumnParseOptions` and a `ColumnFormatOptions`, passed to the helpers by `in`.

## The native library

`src/FixedWidthParser.Native` is published with NativeAOT as a shared library with a C ABI
(`include/fixedwidth.h`), following ExcelReader.Native: `[UnmanagedCallersOnly]` wrappers in `Exports*.cs`
that only check pointers, over a span-based `*Api` layer the tests drive; opaque handles in a
`NativeHandleTable` that never reissues an id; a per-thread last error; every optional input in a
`struct_size`-versioned options struct. It uses the core through `InternalsVisibleTo` and adds nothing to
its public API.

```
fw_column_spec[] -> NativeLayout -> ParallelAggregator -> TableSink per partition -> fw_table
                    (validated,      (byte ranges,         (a ColumnBuilder per       or Arrow
                     immutable)       header skipped)       column, native buffers)
```

There is no model type: a `NativeLayout` holds each column's `ColumnParseOptions` and
`ColumnFormatOptions`, and a `ColumnBuilder` parses its slice of each line with the same
`FixedWidthRuntime` helpers generated code calls, appending to `GrowableNativeBuffer`s (`int64`,
`double`, `int32` days or string offsets, UTF-8 bytes, a validity bitmap created on the first null). A line
rejected midway truncates the columns it already filled. A partitioned read merges by adopting the
following partition's builders; publishing concatenates each column once. The Arrow export moves those
buffers into `ArrowArray`s without a copy, except DECIMAL, widened to 128 bits.

Writing formats a caller's `fw_table` through the layout. Every line has the same length, so the output
size and each batch's offset are known up front: batches format in parallel straight into the output
buffer, or into a pooled buffer written at its offset with `RandomAccess.Write`, and the bytes equal a
serial write.

`python/` binds the C ABI with `ctypes`; `fixedwidth.hpp` wraps it for C++23, and maps structs through
`FixedWidthMapper<T>`, as ExcelReader's C++ header does: the bindings build the layout, a read converts
the columnar table to `T` row by row, and a write builds columns from the structs. The NativeSmoke C program
pins every struct's layout with static asserts.

## Public surface

User-facing types live in the root `FixedWidthParser` namespace. `FixedWidthParser.Infrastructure`
holds what is public only because generated code in the consumer's assembly calls it, or because a
`FixedWidth` method returns it so `foreach` allocates nothing; it is marked
`[EditorBrowsable(Never)]`. Compiled consumer code binds to those signatures, so they evolve only by
addition: a new per-column setting is a new constructor overload and property on `ColumnParseOptions`
or `ColumnFormatOptions`, never a change to an existing helper. Internal types keep their folder
namespaces (`Readers`, `Writers`, `Parsers`, `Processors`, `Formatters`, `Attributes`).

## Further reading

- [`STYLEGUIDE.md`](STYLEGUIDE.md) — code style, and the rules for untrusted input, pooled buffers and twins.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — build expectations and how to submit a change.
- [`SECURITY.md`](SECURITY.md) — supported versions and how to report a vulnerability.
