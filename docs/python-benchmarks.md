# Python: fixedwidth against pandas.read_fwf

`python/benchmarks/bench_read.py` generates a synthetic file shaped like B3's COTAHIST quote history (245-byte
lines, all 26 fields of record type 01, a 00 header and a 99 trailer; layout in `python/benchmarks/cotahist.py`),
checks that every reader returns the same values as `pandas.read_fwf` given the same column specs (fixedwidth
exactly, the polars reference within floating-point rounding), and times each one, best of several runs.

    python python/benchmarks/bench_read.py --lines 1000000 --runs 5

`read_fwf` uses its Python engine, which `skipfooter` needs to drop the trailer.

## Results

1,000,000 lines (235 MiB). AMD Ryzen 7 5700X (8 cores, 16 threads), Windows 10, Python 3.14.7, pandas 3.1.0,
pyarrow 26.0.0, polars 2.0.0. `read_fwf` best of 1 run, the others best of 5.

| Reader | Seconds | vs read_fwf |
|---|---:|---:|
| pandas.read_fwf | 14.412 | 1.0x |
| polars str.slice (reference) | 0.328 | 44x |
| fw.read_pandas | 0.207 | 70x |
| fw.read_pandas, 1 thread | 0.618 | 23x |
| fw.read_polars | 0.188 | 77x |
| fw.read_arrow | 0.169 | 85x |
| fw.read (NumPy views) | 0.169 | 85x |
| fw.read, 1 thread | 0.580 | 25x |

A single thread is already 25x faster than `read_fwf`; the default, one thread per processor, adds another 3.4x.
`fw.read_pandas` costs about 40 ms over `fw.read_arrow`, pyarrow's conversion to pandas.

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

Not done: the concatenation still copies every column, and its page faults are the next cost. Handing
`read_arrow` the partitions as Arrow chunks would skip that copy for pyarrow, pandas and polars.
