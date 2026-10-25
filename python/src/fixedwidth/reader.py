"""Reading: whole sources into a Table, Arrow, pandas or polars, and batched reads."""

from __future__ import annotations

import ctypes
import os
from collections.abc import Iterator
from typing import Any, Union

from fixedwidth import _native
from fixedwidth.arrow_stream import ArrowStream
from fixedwidth.layout import Layout, check
from fixedwidth.table import Table, _Owner
from fixedwidth.types import ReaderOptions, ReadOptions, code_page

Source = Union[str, "os.PathLike[str]", bytes, bytearray, memoryview]


class _Input:
    """A file path or a buffer, as the pointer + length arguments the native calls take."""

    def __init__(self, source: Source) -> None:
        self.keep: object = None
        if isinstance(source, (str, os.PathLike)):
            self.path, self.path_len = _native.utf8(os.fspath(source))
            return
        self.path = None
        view = memoryview(source)
        if view.readonly or not view.contiguous:
            data = bytes(view)
            self.keep = data
            self.address = ctypes.cast(ctypes.c_char_p(data), ctypes.c_void_p).value
        else:
            buffer = (ctypes.c_char * view.nbytes).from_buffer(view)
            self.keep = (view, buffer)
            self.address = ctypes.addressof(buffer)
        self.length = view.nbytes

    @property
    def is_file(self) -> bool:
        return self.path is not None


def _read_options(options: ReadOptions | None) -> _native.ReadOptions | None:
    if options is None:
        return None
    bom = options.skip_byte_order_mark
    return _native.ReadOptions(
        struct_size=ctypes.sizeof(_native.ReadOptions),
        header_lines=options.header_lines,
        skip_invalid=int(options.skip_invalid),
        degree_of_parallelism=options.degree_of_parallelism,
        encoding=code_page(options.encoding),
        max_line_length=options.max_line_length,
        skip_byte_order_mark=_native.FW_OPT_DEFAULT if bom is None else (_native.FW_OPT_TRUE if bom else _native.FW_OPT_FALSE),
    )


def _reader_options(options: ReaderOptions | None) -> _native.ReaderOptions | None:
    if options is None:
        return None
    raw = _native.ReaderOptions(struct_size=ctypes.sizeof(_native.ReaderOptions), batch_rows=options.batch_rows)
    read = _read_options(options.read)
    if read is not None:
        raw.read = read
    return raw


def _ref(value: object) -> object:
    return ctypes.byref(value) if value is not None else None


def read(source: Source, layout: Layout, options: ReadOptions | None = None) -> Table:
    """Reads a whole file or buffer into a Table (see `ReadOptions` for header lines, skipping and threads)."""
    lib = _native.load_library()
    data = _Input(source)
    raw = _read_options(options)
    table = _native.Table()
    result = _native.ReadResult(struct_size=ctypes.sizeof(_native.ReadResult))
    if data.is_file:
        status = lib.fw_read_file(data.path, data.path_len, layout.handle, _ref(raw), ctypes.byref(table), ctypes.byref(result))
    else:
        status = lib.fw_read_memory(data.address, data.length, layout.handle, _ref(raw), ctypes.byref(table), ctypes.byref(result))
    check(status)
    return Table(_Owner(table), layout.names, int(result.lines_read), int(result.lines_skipped))


def read_arrow(source: Source, layout: Layout, options: ReadOptions | None = None) -> Any:
    """Reads a whole file or buffer into a `pyarrow.Table`, handed over without a copy. Requires pyarrow."""
    import pyarrow

    lib = _native.load_library()
    data = _Input(source)
    raw = _read_options(options)
    array = _native.ArrowArray()
    schema = _native.ArrowSchema()
    if data.is_file:
        status = lib.fw_read_arrow_file(data.path, data.path_len, layout.handle, _ref(raw), ctypes.byref(array), ctypes.byref(schema), None)
    else:
        status = lib.fw_read_arrow_memory(data.address, data.length, layout.handle, _ref(raw), ctypes.byref(array), ctypes.byref(schema), None)
    check(status)
    batch = pyarrow.RecordBatch._import_from_c(ctypes.addressof(array), ctypes.addressof(schema))
    return pyarrow.Table.from_batches([batch])


def read_pandas(source: Source, layout: Layout, options: ReadOptions | None = None) -> Any:
    """`read_arrow()` as a `pandas.DataFrame`; each Arrow buffer is freed as pandas takes it. Requires pyarrow and pandas."""
    return read_arrow(source, layout, options).to_pandas(self_destruct=True, split_blocks=True)


def read_polars(source: Source, layout: Layout, options: ReadOptions | None = None) -> Any:
    """`read_arrow()` as a `polars.DataFrame`, zero-copy. Without pyarrow, reads sequentially through an Arrow stream."""
    import polars

    try:
        return polars.from_arrow(read_arrow(source, layout, options))
    except ImportError:
        return polars.DataFrame(read_arrow_stream(source, layout, ReaderOptions(read=options or ReadOptions())))


def read_batches(source: Source, layout: Layout, options: ReaderOptions | None = None) -> Iterator[Table]:
    """Reads sequentially, yielding a Table of at most `options.batch_rows` rows at a time."""
    lib = _native.load_library()
    data = _Input(source)
    raw = _reader_options(options)
    reader = ctypes.c_void_p()
    if data.is_file:
        status = lib.fw_reader_open_file(data.path, data.path_len, layout.handle, _ref(raw), ctypes.byref(reader))
    else:
        status = lib.fw_reader_open_memory(data.address, data.length, layout.handle, _ref(raw), ctypes.byref(reader))
    check(status)
    try:
        while True:
            table = _native.Table()
            status = lib.fw_reader_next(reader, ctypes.byref(table))
            if status == _native.FW_EOF:
                return
            check(status)
            yield Table(_Owner(table), layout.names)
    finally:
        lib.fw_reader_close(reader)
        del data


def read_arrow_stream(source: Source, layout: Layout, options: ReaderOptions | None = None) -> ArrowStream:
    """The batches of `read_batches()` as a single-use Arrow PyCapsule stream, for `polars.DataFrame(stream)`
    or `pyarrow.RecordBatchReader.from_stream(stream)`. Needs no third-party package to create."""
    lib = _native.load_library()
    data = _Input(source)
    raw = _reader_options(options)
    stream = _native.ArrowArrayStream()
    if data.is_file:
        status = lib.fw_read_arrow_stream_file(data.path, data.path_len, layout.handle, _ref(raw), ctypes.byref(stream))
    else:
        status = lib.fw_read_arrow_stream_memory(data.address, data.length, layout.handle, _ref(raw), ctypes.byref(stream))
    check(status)
    return ArrowStream(stream, keep=data)
