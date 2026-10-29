"""Writing: a pandas, polars or pyarrow table through a layout, to a file, a stream or bytes. Requires pyarrow."""

from __future__ import annotations

import ctypes
import decimal
import io
import operator
import os
from typing import IO, Any

from fixedwidth import _native
from fixedwidth.layout import Layout, check
from fixedwidth.reader import _fail
from fixedwidth.types import (
    Column,
    ColumnType,
    FixedWidthError,
    WriteOptions,
    code_page,
)


def write(data: Any, destination: str | os.PathLike[str] | IO[bytes], layout: Layout, options: WriteOptions | None = None) -> None:
    """Writes every row of `data` (pandas, polars, pyarrow, or anything with `__arrow_c_stream__`) to `destination`:
    a path, or a binary stream with `write()` (an open file, `gzip.open(..., "wb")`, a socket's `makefile("wb")`).

    A stream gets the lines in order, a batch per `write()` call on this thread, while batches format in parallel;
    memory stays at a batch per thread. Columns are matched to the layout by name. A null writes as its column
    filled with `trim_char`."""
    lib = _native.load_library()
    table, keep = _native_table(data, layout)
    raw, newline = _write_options(options)
    raw_ref = ctypes.byref(raw) if raw else None
    if isinstance(destination, (str, os.PathLike)):
        name, name_len = _native.utf8(os.fspath(destination))
        check(lib.fw_write_file(name, name_len, ctypes.byref(table), layout.handle, raw_ref))
    else:
        sink, callback = _sink(destination)
        check(lib.fw_write_stream(ctypes.byref(table), layout.handle, raw_ref, ctypes.byref(sink)))
        del callback
    del keep, newline


def _sink(stream: Any) -> tuple[_native.Sink, object]:
    """An fw_sink over `stream`'s write(); keep the callback alive until the native call returns."""
    # The standard streams copy or write a buffer before write() returns, so they get the native memory itself;
    # anything else gets a copy it may keep.
    zero_copy = isinstance(stream, io.IOBase)

    def write(_user_data: int, buffer: int, length: int) -> int:
        try:
            view = memoryview((ctypes.c_ubyte * length).from_address(buffer)).cast("B")
            data = view if zero_copy else bytes(view)
            done = 0
            while done < length:
                count = stream.write(data[done:])
                if count is None:
                    if isinstance(stream, io.RawIOBase):
                        raise OSError("the stream would block; writing needs a blocking stream")
                    break  # a write() that returns nothing took everything
                count = operator.index(count)
                if count <= 0:
                    raise OSError("the stream accepted no bytes")
                done += count
            return 0
        except BaseException as error:  # noqa: BLE001 - every failure must reach the native side
            return _fail(error)

    callback = _native.SinkWrite(write)
    return _native.Sink(ctypes.sizeof(_native.Sink), None, callback), callback


def write_bytes(data: Any, layout: Layout, options: WriteOptions | None = None) -> bytes:
    """Same as `write()`, returning the bytes."""
    lib = _native.load_library()
    table, keep = _native_table(data, layout)
    raw, newline = _write_options(options)
    buffer = _native.Buffer()
    check(lib.fw_write_memory(ctypes.byref(table), layout.handle, ctypes.byref(raw) if raw else None, ctypes.byref(buffer)))
    try:
        return ctypes.string_at(buffer.data, buffer.length) if buffer.length else b""
    finally:
        lib.fw_free_buffer(ctypes.byref(buffer))
        del keep, newline


def _write_options(options: WriteOptions | None) -> tuple[_native.WriteOptions | None, object]:
    if options is None:
        return None, None
    newline, newline_len = _native.utf8(options.newline)
    raw = _native.WriteOptions(
        struct_size=ctypes.sizeof(_native.WriteOptions),
        encoding=code_page(options.encoding),
        newline=ctypes.cast(newline, ctypes.POINTER(ctypes.c_uint8)),
        newline_len=newline_len,
        degree_of_parallelism=options.degree_of_parallelism,
        batch_rows=options.batch_rows,
    )
    return raw, newline


def _arrow_table(data: Any) -> Any:
    import pyarrow

    if isinstance(data, pyarrow.RecordBatch):
        return pyarrow.Table.from_batches([data])
    if isinstance(data, pyarrow.Table):
        return data
    if type(data).__module__.startswith("pandas"):
        return pyarrow.Table.from_pandas(data, preserve_index=False)
    if hasattr(data, "__arrow_c_stream__"):
        return pyarrow.table(data)
    raise TypeError(f"cannot write {type(data).__name__}: pass a pandas, polars or pyarrow table")


def _native_table(data: Any, layout: Layout) -> tuple[_native.Table, list[object]]:
    import pyarrow

    arrow = _arrow_table(data).combine_chunks()
    columns = (_native.Column * max(1, len(layout.columns)))()
    keep: list[object] = [columns]
    for index, spec in enumerate(layout.columns):
        if spec.name not in arrow.column_names:
            raise FixedWidthError(f"column '{spec.name}' of the layout is not in the data")
        chunked = arrow.column(spec.name)
        array = chunked.chunk(0) if chunked.num_chunks else pyarrow.array([], type=chunked.type)
        _fill(columns[index], spec, array, keep)
    table = _native.Table(column_count=len(layout.columns), row_count=arrow.num_rows, columns=columns)
    return table, keep


def _fill(target: _native.Column, spec: Column, array: Any, keep: list[object]) -> None:
    import numpy
    import pyarrow

    kind = ColumnType(spec.type)
    target.type = int(kind)
    target.scale = spec.scale if kind is ColumnType.DECIMAL else 0
    target.length = len(array)
    if array.null_count:
        valid = numpy.packbits(~array.is_null().to_numpy(zero_copy_only=False), bitorder="little")
        keep.append(valid)
        target.validity = valid.ctypes.data
    if kind is ColumnType.STRING:
        _fill_strings(target, array.cast(pyarrow.string()).fill_null(""), keep)
        return
    values = numpy.ascontiguousarray(_values(kind, spec, array, pyarrow, numpy))
    keep.append(values)
    target.values = values.ctypes.data if len(values) else None


def _values(kind: ColumnType, spec: Column, array: Any, pa: Any, np: Any) -> Any:
    if kind is ColumnType.INT64:
        return array.cast(pa.int64()).fill_null(0).to_numpy(zero_copy_only=False)
    if kind is ColumnType.FLOAT64:
        return array.cast(pa.float64()).fill_null(0).to_numpy(zero_copy_only=False)
    if kind is ColumnType.DECIMAL:
        return _decimals(spec, array, pa, np)
    if kind is ColumnType.DATE:
        return array.cast(pa.date32()).cast(pa.int32()).fill_null(0).to_numpy(zero_copy_only=False)
    if kind is ColumnType.TIME:
        return array.cast(pa.time64("us")).cast(pa.int64()).fill_null(0).to_numpy(zero_copy_only=False)
    if pa.types.is_timestamp(array.type) and array.type.tz is not None:
        array = array.cast(pa.timestamp(array.type.unit))
    return array.cast(pa.timestamp("us")).cast(pa.int64()).fill_null(0).to_numpy(zero_copy_only=False)


def _decimals(spec: Column, array: Any, pa: Any, np: Any) -> Any:
    """The unscaled integers a DECIMAL column stores, each of which must fit an int64.

    Decimals and integers scale exactly, and a decimal with more digits than the scale fails. Floats, binary
    approximations to begin with, round half to even to the scale; NaN and infinity fail."""
    if pa.types.is_decimal(array.type):
        return np.array([_unscaled(v, spec) for v in array.to_pylist()], dtype=np.int64)
    factor = 10**spec.scale
    if pa.types.is_integer(array.type):
        import pyarrow.compute

        bounds = pyarrow.compute.min_max(array).as_py()
        for value in (bounds["min"], bounds["max"]):
            if value is not None:
                _check_int64(value * factor, value, spec)
        return array.cast(pa.int64()).fill_null(0).to_numpy(zero_copy_only=False) * factor
    floats = array.cast(pa.float64()).fill_null(0).to_numpy(zero_copy_only=False)
    with np.errstate(over="ignore", invalid="ignore"):
        scaled = np.round(floats * factor)
    # Written so NaN fails too; 2**63 is exact in a float64, the int64 range is not.
    outside = ~((scaled >= -(2.0**63)) & (scaled < 2.0**63))
    if outside.any():
        value = floats[int(np.argmax(outside))]
        raise FixedWidthError(f"value {value} of column '{spec.name}' is not finite or does not fit an int64 at scale {spec.scale}")
    return scaled.astype(np.int64)


# Scales without rounding, whatever the caller's decimal context.
_EXACT = decimal.Context(prec=decimal.MAX_PREC)


def _unscaled(value: Any, spec: Column) -> int:
    """A decimal as the integer the column stores; more decimals than its scale would be lost, so they fail."""
    if value is None:
        return 0
    scaled = value.scaleb(spec.scale, _EXACT)
    if scaled != scaled.to_integral_value():
        raise FixedWidthError(f"value {value} of column '{spec.name}' has more decimals than its scale {spec.scale}")
    return _check_int64(int(scaled), value, spec)


def _check_int64(unscaled: int, value: Any, spec: Column) -> int:
    if not -(2**63) <= unscaled < 2**63:
        raise FixedWidthError(f"value {value} of column '{spec.name}' does not fit an int64 at scale {spec.scale}")
    return unscaled


def _fill_strings(target: _native.Column, array: Any, keep: list[object]) -> None:
    import numpy

    buffers = array.buffers()
    n = len(array)
    offsets = numpy.frombuffer(buffers[1], dtype=numpy.int32)[array.offset : array.offset + n + 1] if n else numpy.zeros(1, numpy.int32)
    base = int(offsets[0])
    rebased = numpy.ascontiguousarray(offsets - base, dtype=numpy.int32)
    keep.append(rebased)
    keep.append(array)
    target.values = rebased.ctypes.data
    data_len = int(rebased[-1])
    target.data_len = data_len
    target.data = buffers[2].address + base if data_len and buffers[2] is not None else None
