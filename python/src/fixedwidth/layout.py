"""Layout: the columns of a line, validated once by the native library and shared by every call."""

from __future__ import annotations

import ctypes
from collections.abc import Sequence

from fixedwidth import _native
from fixedwidth.types import Column, ColumnType, FixedWidthError, LayoutOptions, ParseError


def check(status: int) -> None:
    """Raises the error a native status reports."""
    if status == _native.FW_OK:
        return
    message = _native.last_error() or f"native status {status}"
    if status == _native.FW_PARSE_ERROR:
        line_number, column = _native.last_parse_error()
        raise ParseError(message, line_number, column)
    raise FixedWidthError(message)


def ascii_option(value: str | None, what: str) -> int:
    if value is None:
        return 0
    if len(value) != 1 or ord(value) > 127:
        raise ValueError(f"{what} must be one ASCII character, got {value!r}")
    return ord(value)


class Layout:
    """How the columns of a line map to names and types; pass it to every read and write.

    The native layout is immutable and may be shared across threads.
    """

    def __init__(self, columns: Sequence[Column], options: LayoutOptions | None = None) -> None:
        self._lib = _native.load_library()
        self._handle: int | None = None
        self._columns = list(columns)
        specs = (_native.ColumnSpec * max(1, len(self._columns)))()
        keep = [self._fill(specs[i], column) for i, column in enumerate(self._columns)]
        raw = None
        if options is not None:
            raw = _native.LayoutOptions(
                struct_size=ctypes.sizeof(_native.LayoutOptions),
                line_length=options.line_length,
                filler=ascii_option(options.filler, "filler"),
            )
        handle = ctypes.c_void_p()
        check(self._lib.fw_layout_create(specs, len(self._columns), ctypes.byref(raw) if raw else None, ctypes.byref(handle)))
        self._handle = handle.value
        del keep

    @staticmethod
    def _fill(spec: _native.ColumnSpec, column: Column) -> tuple[object, ...]:
        name, name_len = _native.utf8(column.name)
        fmt, fmt_len = _native.utf8(column.format) if column.format is not None else (None, 0)
        spec.struct_size = ctypes.sizeof(_native.ColumnSpec)
        spec.name = ctypes.cast(name, ctypes.POINTER(ctypes.c_uint8))
        spec.name_len = name_len
        spec.start = column.start
        spec.length = column.length
        spec.type = int(ColumnType(column.type))
        spec.nullable = int(column.nullable)
        spec.scale = column.scale
        spec.format = ctypes.cast(fmt, ctypes.POINTER(ctypes.c_uint8)) if fmt is not None else None
        spec.format_len = fmt_len
        spec.trim_char = ascii_option(column.trim_char, "trim_char")
        spec.trim_mode = int(column.trim_mode or 0)
        spec.alignment = int(column.alignment or 0)
        spec.padding = ascii_option(column.padding, "padding")
        spec.overflow = int(column.overflow or 0)
        spec.dictionary = int(column.dictionary)
        return name, fmt

    @property
    def columns(self) -> list[Column]:
        return list(self._columns)

    @property
    def names(self) -> list[str]:
        return [column.name for column in self._columns]

    @property
    def dictionaries(self) -> frozenset[str]:
        """The columns set to reach Arrow dictionary-encoded."""
        return frozenset(column.name for column in self._columns if column.dictionary)

    @property
    def handle(self) -> int:
        return self._handle or 0

    def __del__(self) -> None:
        if getattr(self, "_handle", None):
            self._lib.fw_layout_free(self._handle)
            self._handle = None

    def __repr__(self) -> str:
        return f"Layout({len(self._columns)} columns: {', '.join(self.names)})"
