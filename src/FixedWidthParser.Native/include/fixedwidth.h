/* FixedWidthParser C ABI: read and write fixed-width files into a columnar table.
 *
 * Conventions:
 * - Every function returns an int32_t status: FW_OK (0) or a negative FW_* code. On failure,
 *   fw_last_error() gives the calling thread's message; after FW_PARSE_ERROR, fw_last_parse_error()
 *   also gives the 1-based line number and the layout column.
 * - Strings crossing the ABI are UTF-8 pointer + length, never NUL-terminated.
 * - Every options struct starts with int32_t struct_size, which the caller sets to sizeof(the struct).
 *   A NULL options pointer, or a zeroed struct with struct_size set, means every default. Any other
 *   struct_size is refused with FW_INVALID_ARGUMENT. New fields only ever arrive at the end.
 * - Handles (fw_layout, fw_reader) are opaque. A freed handle stays invalid (FW_INVALID_HANDLE); it
 *   is never reissued.
 * - Threads: a layout is immutable and may be used by several threads at once. A reader is used by
 *   one thread at a time. Reads and writes may run concurrently on different tables.
 * - Column positions and lengths are bytes. Text is decoded with the read encoding and handed out as
 *   UTF-8; numbers and dates use the invariant culture.
 *
 * Call fw_abi_version() first and refuse to continue when it differs from FW_ABI_VERSION. */
#ifndef FIXEDWIDTH_H
#define FIXEDWIDTH_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#define FW_ABI_VERSION 1

#define FW_OK                0
#define FW_EOF              -1
#define FW_BUFFER_TOO_SMALL -2
#define FW_INVALID_HANDLE   -3
#define FW_INVALID_ARGUMENT -4
#define FW_ERROR            -5
#define FW_PARSE_ERROR      -6

/* Column types and their fw_column values. */
#define FW_T_STRING    0 /* int32 offsets (length + 1) into data, valid UTF-8: bytes invalid in the read
                            encoding become U+FFFD */
#define FW_T_INT64     1 /* int64 */
#define FW_T_FLOAT64   2 /* double */
#define FW_T_DECIMAL   3 /* int64 unscaled value: 10^-scale per unit; at most 18 digits */
#define FW_T_DATE      4 /* int32 days since 1970-01-01 */
#define FW_T_TIME      5 /* int64 microseconds since midnight */
#define FW_T_TIMESTAMP 6 /* int64 microseconds since 1970-01-01T00:00:00Z: a zone or offset in the text is
                            applied, text without one is taken as UTC */

#define FW_TRIM_BOTH     0
#define FW_TRIM_TRAILING 1
#define FW_TRIM_LEADING  2

#define FW_ALIGN_DEFAULT 0 /* left for STRING, right for everything else */
#define FW_ALIGN_LEFT    1
#define FW_ALIGN_RIGHT   2

#define FW_OVERFLOW_THROW    0 /* a value wider than its column fails the write */
#define FW_OVERFLOW_TRUNCATE 1 /* STRING only: keep the aligned side, whole characters */

#define FW_OPT_DEFAULT 0
#define FW_OPT_FALSE   1
#define FW_OPT_TRUE    2

int32_t fw_abi_version(void);

/* Copies the calling thread's last error message into buffer (not NUL-terminated) and its length into
 * *out_len. FW_BUFFER_TOO_SMALL when capacity is short; *out_len is then the length needed. */
int32_t fw_last_error(uint8_t* buffer, int32_t capacity, int32_t* out_len);

typedef struct fw_parse_error {
    int32_t struct_size;
    int64_t line_number; /* 1-based, counting every line of the source as a sequential read does */
    int32_t column;      /* index in the layout; -1 when the line is shorter than the layout */
} fw_parse_error;

/* The parts of the calling thread's last FW_PARSE_ERROR. */
int32_t fw_last_parse_error(fw_parse_error* out_error);

/* ---- Layouts ---------------------------------------------------------------------------------- */

typedef struct fw_layout fw_layout;

typedef struct fw_column_spec {
    int32_t struct_size;       /* sizeof(fw_column_spec); the same in every element */
    const uint8_t* name;       /* UTF-8, required */
    int32_t name_len;
    int32_t start;             /* zero-based byte offset */
    int32_t length;            /* bytes, at least 1 */
    int32_t type;              /* FW_T_* */
    int32_t nullable;          /* a blank column (only trim_char and spaces) reads as null */
    int32_t scale;             /* 0..18: FLOAT64 divides digits-only text by 10^scale; DECIMAL records it */
    const uint8_t* format;     /* exact date/time format, or a number format on write; NULL for none */
    int32_t format_len;
    int32_t trim_char;         /* ASCII; 0 = space. A null is written as this character. */
    int32_t trim_mode;         /* FW_TRIM_* */
    int32_t alignment;         /* FW_ALIGN_*, on write */
    int32_t padding;           /* ASCII; 0 = space, on write. '0' keeps a sign leftmost. */
    int32_t overflow;          /* FW_OVERFLOW_*, on write */
} fw_column_spec;

typedef struct fw_layout_options {
    int32_t struct_size;
    int32_t line_length;       /* 0 = the end of the last column; longer pads lines with filler on write */
    int32_t filler;            /* ASCII written in gaps between columns; 0 = space */
} fw_layout_options;

/* Validates the columns (positions, overlaps, types, scale, formats, ASCII options) and creates an
 * immutable layout. The specs and their strings may be freed after the call. Free with fw_layout_free;
 * readers and Arrow streams opened from it keep working after that. */
int32_t fw_layout_create(const fw_column_spec* columns, int32_t column_count,
                         const fw_layout_options* options, fw_layout** out_layout);
void fw_layout_free(fw_layout* layout);

/* ---- Reading ---------------------------------------------------------------------------------- */

typedef struct fw_read_options {
    int32_t struct_size;
    int64_t header_lines;          /* lines skipped first, never parsed; counted by line breaks */
    int32_t skip_invalid;          /* a line that fails to parse is skipped instead of an error */
    int32_t degree_of_parallelism; /* 0 = one thread per processor, 1 = sequential */
    int32_t encoding;              /* code page of the text; 0 = UTF-8, 28591 = Latin-1, 1252, ... */
    int32_t max_line_length;       /* bytes; 0 = 1 MiB. A longer line fails with FW_ERROR. */
    int32_t skip_byte_order_mark;  /* FW_OPT_*; by default only a UTF-8 read skips a leading BOM */
} fw_read_options;

typedef struct fw_column {
    int32_t type;                  /* FW_T_* */
    int32_t scale;
    int64_t length;                /* rows */
    const void* values;            /* see FW_T_*; may be NULL when length is 0 */
    const uint8_t* validity;       /* LSB-first bitmap, 1 = valid; NULL when nothing is null */
    const uint8_t* data;           /* STRING bytes; NULL otherwise or when empty */
    int64_t data_len;
} fw_column;

typedef struct fw_table {
    int32_t column_count;
    int64_t row_count;
    fw_column* columns;            /* in layout order */
} fw_table;

typedef struct fw_read_result {
    int32_t struct_size;
    int64_t lines_read;            /* rows in the table */
    int64_t lines_skipped;         /* header lines plus invalid lines skipped; empty lines are not counted */
} fw_read_result;

/* Reads a whole file or buffer into *out_table, which the caller frees with fw_free_table. out_result
 * may be NULL. A read with degree_of_parallelism other than 1 splits the source into byte ranges and
 * gives the same table and error line numbers as a sequential read. fw_read_memory does not keep
 * data past the call. A string column holding more than 2 GiB of text fails: read in batches. */
int32_t fw_read_file(const uint8_t* path, int32_t path_len, const fw_layout* layout,
                     const fw_read_options* options, fw_table* out_table, fw_read_result* out_result);
int32_t fw_read_memory(const uint8_t* data, int64_t data_len, const fw_layout* layout,
                       const fw_read_options* options, fw_table* out_table, fw_read_result* out_result);

/* Frees a table the library published and zeroes it. Safe on a zeroed table. */
void fw_free_table(fw_table* table);

typedef struct fw_reader fw_reader;

typedef struct fw_reader_options {
    int32_t struct_size;
    fw_read_options read;          /* read.struct_size 0 means its defaults; degree_of_parallelism is ignored */
    int64_t batch_rows;            /* rows per table; 0 = 65,536 */
} fw_reader_options;

/* A sequential reader handing out tables of at most batch_rows rows. fw_reader_open_memory reads data
 * in place: keep it alive until fw_reader_close. */
int32_t fw_reader_open_file(const uint8_t* path, int32_t path_len, const fw_layout* layout,
                            const fw_reader_options* options, fw_reader** out_reader);
int32_t fw_reader_open_memory(const uint8_t* data, int64_t data_len, const fw_layout* layout,
                              const fw_reader_options* options, fw_reader** out_reader);

/* A forward-only source the library pulls from, for a pipe, a socket, a decompressor or a language's own stream
 * objects. read copies up to len bytes into buf and returns how many, 0 at the end, or -1 on failure, after
 * fw_set_source_error to say why; it is called from the thread reading. release, which may be NULL, is called
 * once the library is done with the stream: when the reader is closed or the Arrow stream released, or right
 * away if opening fails after the stream was accepted. A stream rejected as an argument is not released. */
typedef struct fw_stream {
    int32_t struct_size;
    void* user_data;
    int64_t (*read)(void* user_data, uint8_t* buf, int64_t len);
    void (*release)(void* user_data);
} fw_stream;

/* Reads the stream from where it stands, skipping read.header_lines first; it never seeks. */
int32_t fw_reader_open_stream(const fw_stream* stream, const fw_layout* layout,
                              const fw_reader_options* options, fw_reader** out_reader);

/* Called by a read callback before it returns -1: the message becomes the failure's fw_last_error. */
void fw_set_source_error(const uint8_t* message, int32_t len);

/* FW_OK with a table the caller frees with fw_free_table, FW_EOF at the end. After a failure the
 * reader is finished: later calls return FW_EOF. */
int32_t fw_reader_next(fw_reader* reader, fw_table* out_table);
void fw_reader_close(fw_reader* reader);

/* ---- Writing ---------------------------------------------------------------------------------- */

typedef struct fw_write_options {
    int32_t struct_size;
    int32_t encoding;              /* code page for text; 0 = UTF-8 */
    const uint8_t* newline;        /* NULL = "\n" */
    int32_t newline_len;
    int32_t degree_of_parallelism; /* 0 = one thread per processor, 1 = serial */
    int64_t batch_rows;            /* lines formatted per task; 0 = 8,192 */
} fw_write_options;

typedef struct fw_buffer {
    uint8_t* data;
    int64_t length;
} fw_buffer;

/* Writes every row of table through layout. Columns match the layout by position and type (and scale
 * for DECIMAL), else FW_INVALID_ARGUMENT. A null writes as its column filled with trim_char; a date or
 * time without a format writes as ISO 8601 to the second. The output is byte-identical whatever
 * degree_of_parallelism is. The table is read from several threads and must not change during the
 * call. fw_write_memory's buffer is freed with fw_free_buffer. */
int32_t fw_write_file(const uint8_t* path, int32_t path_len, const fw_table* table,
                      const fw_layout* layout, const fw_write_options* options);
int32_t fw_write_memory(const fw_table* table, const fw_layout* layout,
                        const fw_write_options* options, fw_buffer* out_buffer);
void fw_free_buffer(fw_buffer* buffer);

#ifdef __cplusplus
}
#endif

#endif /* FIXEDWIDTH_H */
