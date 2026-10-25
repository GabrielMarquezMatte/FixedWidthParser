# Reading a `PipeReader`

Through 2.0 development the library read a `PipeReader` with its own enumerator,
`PipeRecordEnumerator`, which split lines over `ReadOnlySequence<byte>` and parsed a line in place when
it sat in one segment. It was removed in favour of `pipeReader.AsStream()` into the `Stream` reader.

## Machine and method

AMD Ryzen 7 5700X (8C/16T), Windows 10 Pro 19045, `net10.0`, Release. A Stopwatch console app reads
a 104 MB file of 4,000,000 lines, held in memory as a `MemoryStream`, through the generated path and
sums two columns. Best of seven runs after one warm-up run.

## Result

| Read | 4 KB segments | 64 KB segments |
|---|--:|--:|
| `ReadAsync(PipeReader)`, parsed in the segment | 472.0 ms | 474.6 ms |
| `ReadAsync(pipeReader.AsStream())` | 230.4 ms | 187.3 ms |
| `ReadAsync(Stream)` over the `MemoryStream` | 174.4 ms | |

Avoiding the copy did not pay for itself. The dedicated enumerator's `MoveNextAsync` was `async` on
every line, with no synchronous path for a line already in the buffer, and built a `SequenceReader`
and several `SequencePosition`s per line. The `Stream` path copies a block at a time and then only
slices spans. Which of those costs dominated was not profiled.

The enumerator also duplicated every line rule — the byte order mark, `\r\n`, empty lines, a last
line without a break, `MaxLineLength` — that `LineBufferState` already implements, so removing it
leaves one line splitter for every reader.
