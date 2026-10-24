# Reader performance

## Machine and method

AMD Ryzen 7 5700X (8C/16T), Windows 10 Pro 19045, .NET SDK 11.0.100-rc.2, `net10.0`, Release. A
Stopwatch console app reads 2,000,000 lines of a generated model (a 10-byte string, a 5-byte `int`, a
10-byte `double`) with `ReadFile`, summing every column. Each figure is the best of five runs after one
warm-up run, file in the OS cache. Before and after were measured in the same position of the same
program: the first configuration of each kind a run measures comes out slower while the JIT is still
tiering up, and comparing across positions misled the file-engine experiment below.

## `double` columns in a comma culture

csFastFloat parsed only when the decimal separator was `.`; any other culture decoded the column and
ran the BCL parser. It now runs with any one-ASCII-character separator when the negative sign is `-`
(`FixedWidthContext.DecimalSeparator`, which the writer already used), still falling back unless every
byte was consumed, so thousands separators keep going through the BCL. `CultureTests` checks the
fast path against `double.TryParse` in de-DE, pt-BR and fr-FR.

| Read | Before | After |
|---|--:|--:|
| `.` separator, invariant | 57.1 ns/line | 58.5 |
| `,` separator, pt-BR | 98.4 ns/line | 58.5 |

One value: 42.3 ns decoded and BCL-parsed, 7.7 ns through csFastFloat.

## `StringPool`

CommunityToolkit's `StringPool.GetOrAdd(bytes, encoding)` costs 87 ns per 6-byte value against 19 ns
for `Encoding.UTF8.GetString` (76 ns even decoding on the stack first): hashing, a lock and its
priority map. A per-thread, 4-way set-associative cache of UTF-8 columns now sits in front of it, as
ExcelReader's `Utf8StringCache` does; a miss still goes to the pool.

| Read | No pool | Pool, before | Pool, after |
|---|--:|--:|--:|
| 997 distinct names | 57.1 ns/line | 137.8 | 69.8 |
| 50 distinct names | 58.6 ns/line | — | 69.7 |

A 256-set cache (ExcelReader's size) left the 997-name column at 119.5 ns/line: four ways overflowed
in many sets and the cyclic order missed every time. 1,024 sets (48 KB per thread that reads with a
pool) holds it. A pool still costs ~11 ns/line over allocating: short-lived strings are cheap, and
the pool exists to save memory, not time.

## Not done: larger reads in `AggregateFileAsync`

The file engine trailed the stream engine by 29–35 % in the
[parallel-aggregate](parallel-aggregate.md) runs, and each file worker refills a 64 KB buffer one
`RandomAccess.Read` at a time. Measured warm and in either order, reading 256 KB or 1 MB at a time
moved dop 8 from 21.2 to 19.5 ms at best, within run-to-run spread, and the gap to the stream engine
was ~10 % (19–21 ms against 17.4–18.3 ms), not 30 %. Not changed.
