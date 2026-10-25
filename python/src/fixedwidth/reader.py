"""Reading: whole sources into a Table, Arrow, pandas or polars, and batched reads."""

from __future__ import annotations

import ctypes
import io
import itertools
import operator
import os
from collections.abc import Iterator
from typing import IO, Any, Union

from fixedwidth import _native
from fixedwidth.arrow_stream import ArrowStream
from fixedwidth.layout import Layout, check
from fixedwidth.table import Table, _Owner
from fixedwidth.types import ReaderOptions, ReadOptions, code_page

Source = Union[str, "os.PathLike[str]", bytes, bytearray, memoryview, IO[bytes]]


def _is_stream(source: object) -> bool:
    """An object with readinto() or read() that is not an in-memory buffer: an open file, a pipe, a decompressor."""
    return not isinstance(source, (str, os.PathLike, bytes, bytearray, memoryview, io.BytesIO)) and (
        hasattr(source, "readinto") or hasattr(source, "read")
    )


class _Input:
    """A file path or a buffer, as the pointer + length arguments the native calls take. A stream is read whole."""

    def __init__(self, source: Source) -> None:
        self.keep: object = None
        if isinstance(source, (str, os.PathLike)):
            self.path, self.path_len = _native.utf8(os.fspath(source))
            return
        self.path = None
        if isinstance(source, io.BytesIO):
            # Read in place from the current position; the exported buffer stops the BytesIO resizing meanwhile.
            source = source.getbuffer()[source.tell():]
        elif _is_stream(source):
            source = source.read()
            if source is None:
                raise OSError("the stream has no data ready; reading it needs a blocking stream")
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


# Streams the native side is reading, by the key it passes back to the callbacks; release drops them.
_keys = itertools.count(1)
_live: dict[int, object] = {}


def _fail(error: BaseException) -> int:
    message = (str(error) or type(error).__name__).encode("utf-8", errors="replace")
    _native.load_library().fw_set_source_error(message, len(message))
    return -1


@_native.StreamRead
def _read(key: int, buffer: int, length: int) -> int:
    try:
        stream = _live[key]
        readinto = getattr(stream, "readinto", None)
        if readinto is None:
            data = bytes(stream.read(length))
            if len(data) > length:
                raise OSError("the stream returned more bytes than requested")
            ctypes.memmove(buffer, data, len(data))
            return len(data)
        count = readinto(memoryview((ctypes.c_ubyte * length).from_address(buffer)).cast("B"))
        if count is None:
            raise OSError("the stream has no data ready; reading it needs a blocking stream")
        count = operator.index(count)
        if count > length:
            raise OSError("the stream returned more bytes than requested")
        return count
    except BaseException as error:  # noqa: BLE001 - every failure, KeyboardInterrupt included, must reach the native side
        return _fail(error)


@_native.StreamRelease
def _release(key: int) -> None:
    _live.pop(key, None)


def _stream(source: object) -> _native.NativeStream:
    """An fw_stream over `source`, kept alive until the native side releases it."""
    key = next(_keys)
    _live[key] = source
    return _native.NativeStream(ctypes.sizeof(_native.NativeStream), key, _read, _release)


def read(source: Source, layout: Layout, options: ReadOptions | None = None) -> Table:
    """Reads a whole file, buffer or stream into a Table (see `ReadOptions` for header lines, skipping and threads).
    A stream is read into memory first, as the parallel read needs all of it; `read_batches()` streams it instead."""
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
    """Reads sequentially, yielding a Table of at most `options.batch_rows` rows at a time. A stream (an open file,
    a pipe, `gzip.open(...)`, `zipfile.ZipFile(...).open(...)`) is read as the batches are taken, in constant memory."""
    lib = _native.load_library()
    raw = _reader_options(options)
    reader = ctypes.c_void_p()
    if _is_stream(source):
        data = None
        status = lib.fw_reader_open_stream(ctypes.byref(_stream(source)), layout.handle, _ref(raw), ctypes.byref(reader))
    elif (data := _Input(source)).is_file:
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
    or `pyarrow.RecordBatchReader.from_stream(stream)`. Needs no third-party package to create. A stream source is
    read lazily, as in `read_batches()`."""
    lib = _native.load_library()
    raw = _reader_options(options)
    stream = _native.ArrowArrayStream()
    if _is_stream(source):
        data = None
        status = lib.fw_read_arrow_stream_stream(ctypes.byref(_stream(source)), layout.handle, _ref(raw), ctypes.byref(stream))
    elif (data := _Input(source)).is_file:
        status = lib.fw_read_arrow_stream_file(data.path, data.path_len, layout.handle, _ref(raw), ctypes.byref(stream))
    else:
        status = lib.fw_read_arrow_stream_memory(data.address, data.length, layout.handle, _ref(raw), ctypes.byref(stream))
    check(status)
    return ArrowStream(stream, keep=data)
