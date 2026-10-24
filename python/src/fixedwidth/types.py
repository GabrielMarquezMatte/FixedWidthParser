"""The public value types: column types, column descriptions, options and errors."""

from __future__ import annotations

import re
from dataclasses import KW_ONLY, dataclass, field
from enum import IntEnum


class ColumnType(IntEnum):
    """FW_T_*: how a column is parsed and what its values are."""

    STRING = 0
    INT64 = 1
    FLOAT64 = 2
    DECIMAL = 3
    DATE = 4
    TIME = 5
    TIMESTAMP = 6


class TrimMode(IntEnum):
    BOTH = 0
    TRAILING = 1
    LEADING = 2


class Alignment(IntEnum):
    DEFAULT = 0
    LEFT = 1
    RIGHT = 2


class Overflow(IntEnum):
    THROW = 0
    TRUNCATE = 1


@dataclass(frozen=True)
class Column:
    """One column of a layout: its name, zero-based byte `start`, byte `length` and `type`.

    - `nullable`: a blank column (only `trim_char` and spaces) reads as null instead of failing.
    - `scale`: implied decimals. FLOAT64 divides digits-only text by 10**scale (B3's `N(11)V99`);
      DECIMAL keeps the unscaled integer.
    - `format`: the exact date/time format (.NET syntax, e.g. `yyyyMMdd`), or a number format on write.
    - `trim_char`, `trim_mode`: what is stripped before parsing; a null is written as `trim_char`.
    - `alignment`, `padding`, `overflow`: how a value is written. `padding='0'` keeps a sign leftmost.
    """

    name: str
    start: int
    length: int
    type: ColumnType
    _: KW_ONLY
    nullable: bool = False
    scale: int = 0
    format: str | None = None
    trim_char: str | None = None
    trim_mode: TrimMode | None = None
    alignment: Alignment | None = None
    padding: str | None = None
    overflow: Overflow | None = None


@dataclass(frozen=True)
class LayoutOptions:
    """`line_length`: 0 ends lines at the last column; longer pads them with `filler` on write."""

    line_length: int = 0
    filler: str | None = None


@dataclass(frozen=True)
class ReadOptions:
    """Mirrors fw_read_options. `encoding` is a code page or a codec name (`"latin-1"`, `"cp1252"`)."""

    header_lines: int = 0
    skip_invalid: bool = False
    degree_of_parallelism: int = 0
    encoding: int | str | None = None
    max_line_length: int = 0
    skip_byte_order_mark: bool | None = None


@dataclass(frozen=True)
class ReaderOptions:
    """Mirrors fw_reader_options: a sequential read in tables of `batch_rows` rows (0 = 65,536)."""

    read: ReadOptions = field(default_factory=ReadOptions)
    batch_rows: int = 0


@dataclass(frozen=True)
class WriteOptions:
    """Mirrors fw_write_options."""

    encoding: int | str | None = None
    newline: str = "\n"
    degree_of_parallelism: int = 0
    batch_rows: int = 0


class FixedWidthError(Exception):
    """Raised when the native library reports a failure."""


class ParseError(FixedWidthError):
    """A line failed to parse. `line_number` is 1-based; `column` is the layout index, -1 for a short line."""

    def __init__(self, message: str, line_number: int, column: int) -> None:
        super().__init__(message)
        self.line_number = line_number
        self.column = column


_CODE_PAGES = {
    "utf-8": 0, "utf8": 0, "ascii": 20127, "us-ascii": 20127,
    "latin-1": 28591, "latin1": 28591, "iso-8859-1": 28591, "iso8859-1": 28591,
    "windows-1252": 1252, "cp1252": 1252, "iso-8859-15": 28605, "latin-9": 28605,
}


def code_page(encoding: int | str | None) -> int:
    """The code page the native options take for `encoding`."""
    if encoding is None:
        return 0
    if isinstance(encoding, int):
        return encoding
    name = encoding.strip().lower().replace("_", "-")
    if name in _CODE_PAGES:
        return _CODE_PAGES[name]
    match = re.fullmatch(r"(?:cp|windows-)(\d+)", name)
    if match:
        return int(match.group(1))
    raise ValueError(f"unknown encoding {encoding!r}; pass a code page number")
