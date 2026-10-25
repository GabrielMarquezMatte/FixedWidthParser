# Benchmarks against other libraries

## Machine and method

AMD Ryzen 7 5700X (8C/16T), Windows 10 Pro 19045, .NET SDK 11.0.100-rc.2, `net10.0` (.NET 10.0.13),
Release. BenchmarkDotNet 0.15.8, default job, `MemoryDiagnoser`. Measured on 2.0 (`feat/v2-api`, October 2026).

The model has three columns: a 10-byte string, a 5-byte `int` and a 10-byte `double`, 25 bytes a line.
The other libraries are [FileHelpers](https://www.filehelpers.net/) 3.5.2,
[FlatFiles](https://github.com/jehugaleahsa/FlatFiles) 5.0.4 and
[RecordParser](https://github.com/leandromoh/RecordParser) 2.3.0, with parallelism off.

The comparison is not like-for-like, by design:

- **Reading.** The others read text, so they are handed the already-decoded `string`; FixedWidthParser
  reads the file's bytes from a `MemoryStream`, which is what a caller holding the file would give it.
  The others are spared the UTF-8 decode.
- **Writing.** Everything targets `Stream.Null`, to measure formatting rather than I/O. The others write
  text through a `StreamWriter`; FixedWidthParser writes bytes straight to the stream.

Run them with:

```bash
dotnet run -c Release --project tests/Benchmarks/Benchmarks.csproj -- --filter "*Comparison*"
```

Reports are written to `tests/Benchmarks/BenchmarkDotNet.Artifacts/results`; the
[live results](https://gabrielmarquezmatte.github.io/FixedWidthParser/dev/bench/) track the library's own suite
across commits.

## Reading

N records, summing one column.

| Method                     | Count | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------- |------ |-----------:|----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| FixedWidthParser_Generated | 100   |   3.851 μs | 0.0131 μs | 0.0110 μs |  0.18 |    0.00 |  0.2365 |      - |   3.89 KB |        0.09 |
| FixedWidthParser_Read      | 100   |   5.268 μs | 0.1010 μs | 0.1994 μs |  0.24 |    0.01 |  0.2365 |      - |   3.89 KB |        0.09 |
| RecordParser_Read          | 100   |   7.956 μs | 0.0140 μs | 0.0117 μs |  0.37 |    0.00 |  0.2441 |      - |   4.23 KB |        0.10 |
| FileHelpers_Read           | 100   |  21.708 μs | 0.3205 μs | 0.2503 μs |  1.00 |    0.02 |  2.5330 | 0.0610 |  41.49 KB |        1.00 |
| FlatFiles_Read             | 100   |  41.812 μs | 0.8312 μs | 0.6489 μs |  1.93 |    0.04 |  6.2866 | 0.1831 | 103.13 KB |        2.49 |
|                            |       |            |           |           |       |         |         |        |           |             |
| FixedWidthParser_Generated | 1000  |  40.220 μs | 0.7687 μs | 1.4248 μs |  0.18 |    0.01 |  2.3193 |      - |  38.34 KB |        0.09 |
| FixedWidthParser_Read      | 1000  |  50.214 μs | 0.2093 μs | 0.1748 μs |  0.23 |    0.00 |  2.3193 |      - |  38.34 KB |        0.09 |
| RecordParser_Read          | 1000  |  73.700 μs | 0.1759 μs | 0.1560 μs |  0.33 |    0.00 |  2.3193 |      - |  38.69 KB |        0.10 |
| FileHelpers_Read           | 1000  | 220.773 μs | 1.1137 μs | 0.9872 μs |  1.00 |    0.01 | 24.6582 | 5.8594 | 406.42 KB |        1.00 |
| FlatFiles_Read             | 1000  | 414.094 μs | 5.4961 μs | 4.8721 μs |  1.88 |    0.02 | 58.1055 | 1.9531 |  953.2 KB |        2.35 |

The generated model reads about 5.5 times faster than FileHelpers and the attribute layout about 4.2 times,
both with a tenth of its allocation, which is the strings the records hold. RecordParser, the fastest of
the others, takes 1.8 to 2.1 times as long as the generated model.

## Writing

N records written to `Stream.Null` through the attribute layout.

| Method                 | Count | Mean          | Error        | StdDev       | Median        | Ratio  | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|----------------------- |------ |--------------:|-------------:|-------------:|--------------:|-------:|--------:|--------:|--------:|----------:|------------:|
| FixedWidthParser_Write | 1     |      66.84 ns |     0.273 ns |     0.256 ns |      66.80 ns |   0.52 |    0.02 |       - |       - |         - |        0.00 |
| FileHelpers_Write      | 1     |     128.67 ns |     2.606 ns |     6.294 ns |     126.76 ns |   1.00 |    0.07 |  0.0215 |       - |     360 B |        1.00 |
| FlatFiles_Write        | 1     |     273.24 ns |     3.325 ns |     3.110 ns |     272.65 ns |   2.13 |    0.10 |  0.0424 |       - |     712 B |        1.98 |
| RecordParser_Write     | 1     |  15,581.73 ns |   550.422 ns | 1,570.385 ns |  14,975.60 ns | 121.37 |   13.44 |  4.0283 |  0.6714 |   65384 B |      181.62 |
|                        |       |               |              |              |               |        |         |         |         |           |             |
| FixedWidthParser_Write | 100   |   5,159.48 ns |     8.964 ns |     8.385 ns |   5,156.54 ns |   0.43 |    0.00 |       - |       - |         - |        0.00 |
| FileHelpers_Write      | 100   |  11,897.36 ns |    77.800 ns |    64.966 ns |  11,890.04 ns |   1.00 |    0.01 |  1.9531 |       - |   32832 B |        1.00 |
| FlatFiles_Write        | 100   |  30,719.14 ns |   510.520 ns |   452.562 ns |  30,769.02 ns |   2.58 |    0.04 |  4.2114 |       - |   71200 B |        2.17 |
| RecordParser_Write     | 100   |  54,366.37 ns | 1,017.927 ns |   952.169 ns |  54,458.00 ns |   4.57 |    0.08 |  4.0283 |  0.6104 |   67696 B |        2.06 |
|                        |       |               |              |              |               |        |         |         |         |           |             |
| FixedWidthParser_Write | 1000  |  57,478.75 ns |    65.891 ns |    61.634 ns |  57,493.25 ns |   0.47 |    0.00 |       - |       - |         - |        0.00 |
| FileHelpers_Write      | 1000  | 121,174.48 ns |   500.343 ns |   443.541 ns | 121,058.04 ns |   1.00 |    0.00 | 19.5313 |       - |  328032 B |        1.00 |
| FlatFiles_Write        | 1000  | 262,811.90 ns | 3,274.975 ns | 3,063.413 ns | 262,086.38 ns |   2.17 |    0.03 | 42.4805 |       - |  712000 B |        2.17 |
| RecordParser_Write     | 1000  | 346,479.69 ns | 5,197.862 ns | 4,862.083 ns | 344,529.69 ns |   2.86 |    0.04 | 53.7109 | 52.7344 |  880427 B |        2.68 |

The writer takes about half of FileHelpers' time and allocates nothing, whatever the record count.

## Not measured

- Files on disk, cold or cached: every figure here is in memory.
- RecordParser and the parallel modes of FixedWidthParser with parallelism on;
  [parallel-aggregate.md](../parallel-aggregate.md) covers FixedWidthParser's own.
- Other models: wider lines, `decimal` and date columns, non-ASCII text.
