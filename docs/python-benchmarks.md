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
| pandas.read_fwf | 15.908 | 1.0x |
| polars str.slice (reference) | 0.351 | 45x |
| fw.read_pandas | 0.293 | 54x |
| fw.read_pandas, 1 thread | 0.747 | 21x |
| fw.read_polars | 0.267 | 60x |
| fw.read_arrow | 0.239 | 67x |
| fw.read (NumPy views) | 0.250 | 64x |
| fw.read, 1 thread | 0.678 | 23x |

A single thread is already 23x faster than `read_fwf`; the default, one thread per processor, adds another 2.7x.
`fw.read_pandas` costs about 50 ms over `fw.read_arrow`, pyarrow's conversion to pandas.

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
