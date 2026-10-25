# Python: fixedwidth against pandas and polars

`python/benchmarks/bench_read.py` generates a synthetic file shaped like B3's COTAHIST quote history (245-byte
lines, all 26 fields of record type 01, a 00 header and a 99 trailer; layout in `python/benchmarks/cotahist.py`),
checks that every reader returns the same values as `pandas.read_fwf` given the same column specs (fixedwidth
exactly, the polars reference within floating-point rounding), and times each one, best of several runs.
`python/benchmarks/bench_write.py` writes the same table back out (see [Writing](#writing)).

    python python/benchmarks/bench_read.py --lines 1000000 --runs 5
    python python/benchmarks/bench_write.py --lines 1000000 --runs 5

`read_fwf` uses its Python engine, which `skipfooter` needs to drop the trailer. The generator caps a day's
quantity at 10^9 shares, so a volume has at most 15 digits and survives a FLOAT64 column exactly; the writers
could not reproduce a 16-digit one from a float64 either.

## Reading

1,000,000 lines (235 MiB). AMD Ryzen 7 5700X (8 cores, 16 threads), Windows 10, Python 3.14.7, pandas 3.1.0,
pyarrow 26.0.0, polars 2.0.0. `read_fwf` best of 1 run, the others best of 5.

| Reader | Seconds | vs read_fwf |
|---|---:|---:|
| pandas.read_fwf | 14.751 | 1.0x |
| polars str.slice (reference) | 0.349 | 42x |
| fw.read_pandas | 0.212 | 70x |
| fw.read_pandas, 1 thread | 0.655 | 23x |
| fw.read_polars | 0.198 | 74x |
| fw.read_arrow | 0.195 | 76x |
| fw.read (NumPy views) | 0.166 | 89x |
| fw.read, 1 thread | 0.600 | 25x |

A single thread is already 25x faster than `read_fwf`; the default, one thread per processor, adds another 3.6x.
`fw.read_pandas` costs about 40 ms over `fw.read_arrow`, pyarrow's conversion to pandas.

## Writing

`bench_write.py` reads the generated file once, before any timing, into pandas, polars and pyarrow tables, and
times each writer turning that table back into the record lines. The expected output is the generated file's
body, so every writer must produce it byte for byte; the script asserts that. Neither pandas nor polars writes
fixed-width files, so the baselines are what their users write instead:

- `numpy.savetxt` over the pandas columns with one printf field per column (`%-12s`, `%013d`), floats scaled to
  their implied decimals and dates turned into `yyyymmdd` integers first.
- polars string expressions: `pad_end` for text, the scaled integers zero-padded with the sign kept leftmost,
  `strftime` for dates, joined with `concat_str` into one column written by `write_csv`.

Same machine and versions; `numpy.savetxt` best of 1 run, the others best of 5.

| Writer | Seconds | vs savetxt |
|---|---:|---:|
| numpy.savetxt (pandas) | 4.302 | 1.0x |
| polars pad + concat_str | 0.707 | 6.1x |
| fw.write (pandas) | 0.219 | 20x |
| fw.write (polars) | 0.190 | 23x |
| fw.write (pyarrow) | 0.162 | 27x |
| fw.write (pyarrow), 1 thread | 0.550 | 7.8x |
| fw.write_bytes (pyarrow) | 0.342 | 13x |

`fw.write` is 4.4x faster than the polars pipeline. A pandas frame costs ~60 ms more than a pyarrow table, its
conversion to Arrow; `write_bytes` timing includes copying the 235 MB into a `bytes` and writing it to the file.
Of `fw.write`'s 0.16 s, ~20 ms is the Python side handing the columns over and ~140 ms the native write.

### Making the native write faster

The first run measured 0.259 s, ~240 ms of it native. Per value, a zero-padded integer (16 of the 26 columns)
cost 19 ns and a `yyyyMMdd` date 47 ns. At 16 threads, `fw_write_file` took 240 ms where `fw_write_memory`
took 141 ms, with the two equal on one thread. Windows runs one synchronous I/O at a time on a file handle, so
the parallel batches queued for it.

- The output file is opened for asynchronous I/O. Each batch still waits for its own write, but they run
  together: 240 → 170 ms.
- A right-aligned integer with no format writes its digits from the right straight into the column, then the
  padding, with "-" kept leftmost: 19 → 9 ns. Formatting at the start and aligning afterwards, as before, cost
  a copy, and counting the digits first cost a second pass.
- A date or time whose format is only digit fields and `- . T / :` reuses the reader's compiled `FastDate`
  to write its digits: 47 → 23 ns.

A test checks both against the BCL's char path in four cultures, both alignments, and zero and space padding.

| Writer | Before | After |
|---|---:|---:|
| fw.write (pyarrow) | 0.259 s | 0.162 s |
| fw.write (pyarrow), 1 thread | 0.838 s | 0.550 s |

## Fast paths for dates and integers

The two `yyyyMMdd` date columns cost 85 ns each through `DateOnly.TryParseExact` on the decoded text, and the
sixteen integer and implied-decimal columns 12–14 ns each through `long.TryParse`. A format made only of
fixed-width digit fields (`yyyy MM dd HH mm ss` and `- . T / :`) is now compiled once per column and read straight
from the bytes (18 ns), and a digits-only integer goes through fast_float's eight-digit step (9 ns), each falling
back to the BCL for anything else. Same machine, same run order, before and after:

| Reader | Before | After |
|---|---:|---:|
| fw.read, 1 thread | 0.920 s | 0.678 s |
| fw.read | 0.261 s | 0.250 s |

The parallel read barely moved, so parsing is not what bounds it.

## The parallel read

Timed inside the native read at 16 threads, the 0.25 s split into ~170 ms of partitioned parsing and ~40 ms of
concatenating the partitions' columns on one thread. Every partition was busy all along, yet the work took ~2.1 s
of thread time against ~0.7 s on one thread, and more threads made each partition slower (1.0x at 2 threads,
1.3x at 4, 1.7x at 8, 3x at 16). An in-memory source changed little, so it was not file I/O. What grows with
the threads is new memory: a read writes ~200 MB of columns, 50,000 demand-zero page faults on one thread and
twice that in parallel, since the concatenation copies everything into fresh memory again, and Windows takes
those faults under a per-process lock.

Three changes, together:

- A partition sizes its column buffers once from its byte count (fixed-width lines make that a row count)
  instead of doubling them from 256 elements.
- `skip_invalid` skips a bad line without building its exception. Numbering the line meant counting line
  breaks from the start of the file, on one thread, for B3's trailer at the very end: ~27 ms.
- The concatenation runs one column per thread.

| Reader | Before | After |
|---|---:|---:|
| fw.read | 0.250 s | 0.169 s |
| fw.read, 1 thread | 0.678 s | 0.580 s |

### Parsing into the final columns

The concatenation still wrote every column into fresh memory a second time. Fixed-width lines fix where each
partition's rows land in the result: with the first line's length as the stride, a partition starting at byte
`b` starts at row `(b - dataStart) / stride`, rounded up. The read now allocates the final columns once and lends
each partition its slice; numeric, date and time values go straight there, and so do STRING bytes, with room
for the column's full width on every row. Publishing moves each partition down to close the gaps that skipped
lines and trimmed text leave, in memory already touched, and hands the buffers over. A partition with more
rows than its slice, as when line lengths vary, copies its column out, and that column is concatenated as
before. STRING offsets are still copied: each slice's leading 0 would fall on the previous slice's last offset.

Same machine, same run order, best of 9:

| Reader | Before | After |
|---|---:|---:|
| fw.read | 0.184 s | 0.153 s |
| fw.read_polars | 0.198 s | 0.165 s |
| fw.read, 1 thread | 0.588 s | 0.593 s |
