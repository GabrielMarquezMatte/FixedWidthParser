"""ctypes binding to the FixedWidthParser NativeAOT shared library.

Everything here mirrors src/FixedWidthParser.Native/include/fixedwidth.h. If you change one, change both.
"""

from __future__ import annotations

import ctypes
import os
import platform
from functools import lru_cache
from pathlib import Path

FW_OK = 0
FW_EOF = -1
FW_BUFFER_TOO_SMALL = -2
FW_INVALID_HANDLE = -3
FW_INVALID_ARGUMENT = -4
FW_ERROR = -5
FW_PARSE_ERROR = -6

FW_ABI_VERSION = 1

FW_OPT_DEFAULT = 0
FW_OPT_FALSE = 1
FW_OPT_TRUE = 2

_u8p = ctypes.POINTER(ctypes.c_uint8)


class ColumnSpec(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int32),
        ("name", _u8p),
        ("name_len", ctypes.c_int32),
        ("start", ctypes.c_int32),
        ("length", ctypes.c_int32),
        ("type", ctypes.c_int32),
        ("nullable", ctypes.c_int32),
        ("scale", ctypes.c_int32),
        ("format", _u8p),
        ("format_len", ctypes.c_int32),
        ("trim_char", ctypes.c_int32),
        ("trim_mode", ctypes.c_int32),
        ("alignment", ctypes.c_int32),
        ("padding", ctypes.c_int32),
        ("overflow", ctypes.c_int32),
    ]


class LayoutOptions(ctypes.Structure):
    _fields_ = [("struct_size", ctypes.c_int32), ("line_length", ctypes.c_int32), ("filler", ctypes.c_int32)]


class ReadOptions(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int32),
        ("header_lines", ctypes.c_int64),
        ("skip_invalid", ctypes.c_int32),
        ("degree_of_parallelism", ctypes.c_int32),
        ("encoding", ctypes.c_int32),
        ("max_line_length", ctypes.c_int32),
        ("skip_byte_order_mark", ctypes.c_int32),
    ]


class ReaderOptions(ctypes.Structure):
    _fields_ = [("struct_size", ctypes.c_int32), ("read", ReadOptions), ("batch_rows", ctypes.c_int64)]


class Column(ctypes.Structure):
    _fields_ = [
        ("type", ctypes.c_int32),
        ("scale", ctypes.c_int32),
        ("length", ctypes.c_int64),
        ("values", ctypes.c_void_p),
        ("validity", ctypes.c_void_p),
        ("data", ctypes.c_void_p),
        ("data_len", ctypes.c_int64),
    ]


class Table(ctypes.Structure):
    _fields_ = [("column_count", ctypes.c_int32), ("row_count", ctypes.c_int64), ("columns", ctypes.POINTER(Column))]


class ReadResult(ctypes.Structure):
    _fields_ = [("struct_size", ctypes.c_int32), ("lines_read", ctypes.c_int64), ("lines_skipped", ctypes.c_int64)]


class ParseErrorInfo(ctypes.Structure):
    _fields_ = [("struct_size", ctypes.c_int32), ("line_number", ctypes.c_int64), ("column", ctypes.c_int32)]


class WriteOptions(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_int32),
        ("encoding", ctypes.c_int32),
        ("newline", _u8p),
        ("newline_len", ctypes.c_int32),
        ("degree_of_parallelism", ctypes.c_int32),
        ("batch_rows", ctypes.c_int64),
    ]


class Buffer(ctypes.Structure):
    _fields_ = [("data", ctypes.c_void_p), ("length", ctypes.c_int64)]


class ArrowSchema(ctypes.Structure):
    pass


class ArrowArray(ctypes.Structure):
    pass


_ArrowSchemaRelease = ctypes.CFUNCTYPE(None, ctypes.POINTER(ArrowSchema))
_ArrowArrayRelease = ctypes.CFUNCTYPE(None, ctypes.POINTER(ArrowArray))

ArrowSchema._fields_ = [
    ("format", ctypes.c_char_p),
    ("name", ctypes.c_char_p),
    ("metadata", ctypes.c_char_p),
    ("flags", ctypes.c_int64),
    ("n_children", ctypes.c_int64),
    ("children", ctypes.POINTER(ctypes.POINTER(ArrowSchema))),
    ("dictionary", ctypes.POINTER(ArrowSchema)),
    ("release", _ArrowSchemaRelease),
    ("private_data", ctypes.c_void_p),
]
ArrowArray._fields_ = [
    ("length", ctypes.c_int64),
    ("null_count", ctypes.c_int64),
    ("offset", ctypes.c_int64),
    ("n_buffers", ctypes.c_int64),
    ("n_children", ctypes.c_int64),
    ("buffers", ctypes.POINTER(ctypes.c_void_p)),
    ("children", ctypes.POINTER(ctypes.POINTER(ArrowArray))),
    ("dictionary", ctypes.POINTER(ArrowArray)),
    ("release", _ArrowArrayRelease),
    ("private_data", ctypes.c_void_p),
]


class ArrowArrayStream(ctypes.Structure):
    pass


_ArrowStreamGetSchema = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.POINTER(ArrowArrayStream), ctypes.POINTER(ArrowSchema))
_ArrowStreamGetNext = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.POINTER(ArrowArrayStream), ctypes.POINTER(ArrowArray))
_ArrowStreamGetLastError = ctypes.CFUNCTYPE(ctypes.c_char_p, ctypes.POINTER(ArrowArrayStream))
_ArrowStreamRelease = ctypes.CFUNCTYPE(None, ctypes.POINTER(ArrowArrayStream))

ArrowArrayStream._fields_ = [
    ("get_schema", _ArrowStreamGetSchema),
    ("get_next", _ArrowStreamGetNext),
    ("get_last_error", _ArrowStreamGetLastError),
    ("release", _ArrowStreamRelease),
    ("private_data", ctypes.c_void_p),
]

_P = ctypes.POINTER
_SIGNATURES: dict[str, tuple[object, list[object]]] = {
    "fw_abi_version": (ctypes.c_int32, []),
    "fw_last_error": (ctypes.c_int32, [_u8p, ctypes.c_int32, _P(ctypes.c_int32)]),
    "fw_last_parse_error": (ctypes.c_int32, [_P(ParseErrorInfo)]),
    "fw_layout_create": (ctypes.c_int32, [_P(ColumnSpec), ctypes.c_int32, _P(LayoutOptions), _P(ctypes.c_void_p)]),
    "fw_layout_free": (None, [ctypes.c_void_p]),
    "fw_read_file": (ctypes.c_int32, [_u8p, ctypes.c_int32, ctypes.c_void_p, _P(ReadOptions), _P(Table), _P(ReadResult)]),
    "fw_read_memory": (ctypes.c_int32, [ctypes.c_void_p, ctypes.c_int64, ctypes.c_void_p, _P(ReadOptions), _P(Table), _P(ReadResult)]),
    "fw_free_table": (None, [_P(Table)]),
    "fw_reader_open_file": (ctypes.c_int32, [_u8p, ctypes.c_int32, ctypes.c_void_p, _P(ReaderOptions), _P(ctypes.c_void_p)]),
    "fw_reader_open_memory": (ctypes.c_int32, [ctypes.c_void_p, ctypes.c_int64, ctypes.c_void_p, _P(ReaderOptions), _P(ctypes.c_void_p)]),
    "fw_reader_next": (ctypes.c_int32, [ctypes.c_void_p, _P(Table)]),
    "fw_reader_close": (None, [ctypes.c_void_p]),
    "fw_read_arrow_file": (ctypes.c_int32, [_u8p, ctypes.c_int32, ctypes.c_void_p, _P(ReadOptions), _P(ArrowArray), _P(ArrowSchema), _P(ReadResult)]),
    "fw_read_arrow_memory": (ctypes.c_int32, [ctypes.c_void_p, ctypes.c_int64, ctypes.c_void_p, _P(ReadOptions), _P(ArrowArray), _P(ArrowSchema), _P(ReadResult)]),
    "fw_read_arrow_stream_file": (ctypes.c_int32, [_u8p, ctypes.c_int32, ctypes.c_void_p, _P(ReaderOptions), _P(ArrowArrayStream)]),
    "fw_read_arrow_stream_memory": (ctypes.c_int32, [ctypes.c_void_p, ctypes.c_int64, ctypes.c_void_p, _P(ReaderOptions), _P(ArrowArrayStream)]),
    "fw_write_file": (ctypes.c_int32, [_u8p, ctypes.c_int32, _P(Table), ctypes.c_void_p, _P(WriteOptions)]),
    "fw_write_memory": (ctypes.c_int32, [_P(Table), ctypes.c_void_p, _P(WriteOptions), _P(Buffer)]),
    "fw_free_buffer": (None, [_P(Buffer)]),
}


def library_filename() -> str:
    system = platform.system()
    if system == "Windows":
        return "FixedWidthParser.Native.dll"
    if system == "Darwin":
        return "FixedWidthParser.Native.dylib"
    return "FixedWidthParser.Native.so"


def _candidate_paths() -> list[Path]:
    paths = []
    override = os.environ.get("FIXEDWIDTH_NATIVE_LIB")
    if override:
        paths.append(Path(override))
    paths.append(Path(__file__).parent / "_lib" / library_filename())
    return paths


@lru_cache(maxsize=1)
def load_library() -> ctypes.CDLL:
    """Loads the bundled library (or FIXEDWIDTH_NATIVE_LIB) once and checks its ABI version."""
    tried = []
    for path in _candidate_paths():
        if not path.exists():
            tried.append(str(path))
            continue
        lib = ctypes.CDLL(str(path))
        for name, (restype, argtypes) in _SIGNATURES.items():
            function = getattr(lib, name)
            function.restype = restype
            function.argtypes = argtypes
        version = lib.fw_abi_version()
        if version != FW_ABI_VERSION:
            raise OSError(f"{path} has ABI version {version}; this package needs {FW_ABI_VERSION}")
        return lib
    raise OSError(
        "FixedWidthParser.Native was not found (looked in " + ", ".join(tried) + "). "
        "Build it with `python python/scripts/build_native.py` or set FIXEDWIDTH_NATIVE_LIB."
    )


def last_error() -> str:
    lib = load_library()
    buffer = (ctypes.c_uint8 * 4096)()
    length = ctypes.c_int32()
    status = lib.fw_last_error(buffer, len(buffer), ctypes.byref(length))
    if status == FW_BUFFER_TOO_SMALL:
        buffer = (ctypes.c_uint8 * length.value)()
        lib.fw_last_error(buffer, len(buffer), ctypes.byref(length))
    return bytes(buffer[: length.value]).decode("utf-8", "replace")


def last_parse_error() -> tuple[int, int]:
    info = ParseErrorInfo(struct_size=ctypes.sizeof(ParseErrorInfo))
    load_library().fw_last_parse_error(ctypes.byref(info))
    return int(info.line_number), int(info.column)


def utf8(text: str) -> tuple[ctypes.Array, int]:
    """A UTF-8 buffer and its length, for a pointer + length argument."""
    encoded = text.encode("utf-8")
    return (ctypes.c_uint8 * max(1, len(encoded))).from_buffer_copy(encoded.ljust(1, b"\0")), len(encoded)
