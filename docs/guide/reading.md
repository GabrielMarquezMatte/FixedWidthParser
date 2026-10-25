# Reading

```csharp
using FixedWidthParser;

var layout = FixedWidthLayout.FromAttributes<Person>();

foreach (var person in FixedWidth.ReadFile("people.txt", layout))
{
    // ...
}

foreach (var person in FixedWidth.ReadFile<GeneratedPerson>("people.txt"))
{
    // generated TryParse for each line
}
```

`Read(Stream, …, leaveOpen)` reads a stream once; `ReadFile` reopens the file on each enumeration. Reading is lazy and reuses a pooled buffer; each line is parsed in place, without a string per line. A layout is immutable: build it once and share it across reads and threads.

Empty lines (including a trailing newline at end of file) are skipped: they are counted toward the line number but not yielded. Lines end at `\n`, with a preceding `\r` dropped, and a leading UTF-8 byte order mark is skipped.

## Async

```csharp
await foreach (var person in FixedWidth.ReadFileAsync("people.txt", layout))
{
    // ...
}
```

`ReadAsync(Stream, leaveOpen)` mirrors the synchronous overload, and cancellation is honored via `WithCancellation`. A [`PipeReader`](https://learn.microsoft.com/dotnet/standard/io/pipelines) — a Kestrel request body, a socket — is read through `pipeReader.AsStream()`.

## Parallel aggregation

To fold a large file into one result — totals, counts, groups — read it on several threads:

```csharp
public sealed class Payroll : IRecordAccumulator<Payroll, Person>
{
    public double Total { get; private set; }

    public void Add(in Person person)
    {
        Total += person.Salary;
    }

    public void Merge(Payroll following)
    {
        Total += following.Total;
    }
}

var payroll = await FixedWidth.AggregateFileAsync<Person, Payroll>("people.txt", layout);
// GeneratedPayroll: the same accumulator over GeneratedPerson.
var generated = await FixedWidth.AggregateFileAsync<GeneratedPerson, GeneratedPayroll>("people.txt");
```

The file is cut into byte ranges, each folded into its own accumulator on a worker thread, and the
accumulators are merged left to right in file order. `Add` must touch only its own instance; `Merge`
runs on one thread. Every line is parsed exactly once, and a failing line reports the same line number
as a sequential read. `FixedWidthReaderOptions.DegreeOfParallelism` caps the threads (`0`, the
default, means one per processor; `1` reads the file in one pass); other reads ignore it. `OnParseError`, when set, runs on
the worker threads.

`AggregateAsync` does the same over a `Stream`, which is read once: one thread reads it into chunks
and the others parse.

## Options

Every read takes an optional `FixedWidthReaderOptions`:

```csharp
var options = new FixedWidthReaderOptions
{
    FormatProvider = CultureInfo.GetCultureInfo("pt-BR"),
    Encoding = Encoding.Latin1,
    StringPool = new StringPool(),
    BufferSize = 64 * 1024,
    MaxLineLength = 4096,
    OnParseError = ex => { log.Warning(ex.Message); return true; }, // skip the line
};
var people = FixedWidth.ReadFile("people.txt", layout, options);
```

| Option | Default | Meaning |
|---|---|---|
| `FormatProvider` | invariant culture | Numbers and dates. |
| `Encoding` | UTF-8 | Text columns. Must be ASCII-compatible. |
| `StringPool` | none | Interns repeated string values. |
| `BufferSize` | 65,536 | Bytes read from the source at a time. |
| `MaxLineLength` | 1,048,576 | Longest line accepted; a longer one throws `InvalidDataException`. |
| `OnParseError` | throw | Return `true` to skip a line that fails to parse. |
| `DegreeOfParallelism` | processor count | Threads for `AggregateFileAsync`/`AggregateAsync`; `1` reads in one pass. |

## Errors

A line that fails to parse throws `FixedWidthParseException`, a `FormatException` that says where:

```text
Line 2 could not be parsed into Person: column 'Age' (start 10, length 5) holds "XX   ".
```

`LineNumber`, `ColumnName`, `ColumnStart`, `ColumnLength` and `RawValue` carry the same information. A line shorter than the layout reports how long it is and how long the layout needs it to be. The parser pays nothing for this on lines that parse: a failed line is parsed a second time to find the column.

## Untrusted input

A reader buffers a line until it finds its line break. `MaxLineLength` bounds that buffer, so a request body or file without line breaks throws `InvalidDataException` instead of growing the buffer until memory runs out. Set it close to your layout's line length when the source is untrusted.

## Single Lines

```csharp
FixedWidth.TryParse("John Doe  30   60000.00  "u8, layout, default, out Person person);

FixedWidth.TryParse<GeneratedPerson>(lineBytes, new FixedWidthContext { FormatProvider = culture }, out var generated);
```

`FixedWidthContext` carries the format provider, the encoding and the string pool for one line; `default` means invariant culture, UTF-8 and no pool.
