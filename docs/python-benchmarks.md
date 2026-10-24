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
| pandas.read_fwf | 14.955 | 1.0x |
| polars str.slice (reference) | 0.306 | 49x |
| fw.read_pandas | 0.290 | 52x |
| fw.read_pandas, 1 thread | 0.940 | 16x |
| fw.read_polars | 0.273 | 55x |
| fw.read_arrow | 0.253 | 59x |
| fw.read (NumPy views) | 0.263 | 57x |
| fw.read, 1 thread | 0.947 | 16x |

A single thread is already 16x faster than `read_fwf`; the default, one thread per processor, adds another 3.6x.
`fw.read_pandas` costs about 40 ms over `fw.read_arrow`, pyarrow's conversion to pandas.
