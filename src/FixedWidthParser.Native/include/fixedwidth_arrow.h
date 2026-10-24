/* Optional Arrow C Data Interface export for FixedWidthParser. Separate from fixedwidth.h so a caller
 * that does not want Arrow never pulls these declarations in.
 *
 * struct ArrowSchema, ArrowArray and ArrowArrayStream are the Arrow C Data and C Stream Interface
 * structs, reproduced from the Apache Arrow specification (Apache License 2.0). */
#ifndef FIXEDWIDTH_ARROW_H
#define FIXEDWIDTH_ARROW_H

#include "fixedwidth.h"

#ifdef __cplusplus
extern "C" {
#endif

#ifndef ARROW_C_DATA_INTERFACE
#define ARROW_C_DATA_INTERFACE

#define ARROW_FLAG_DICTIONARY_ORDERED 1
#define ARROW_FLAG_NULLABLE 2
#define ARROW_FLAG_MAP_KEYS_SORTED 4

struct ArrowSchema {
    const char* format;
    const char* name;
    const char* metadata;
    int64_t flags;
    int64_t n_children;
    struct ArrowSchema** children;
    struct ArrowSchema* dictionary;

    void (*release)(struct ArrowSchema*);
    void* private_data;
};

struct ArrowArray {
    int64_t length;
    int64_t null_count;
    int64_t offset;
    int64_t n_buffers;
    int64_t n_children;
    const void** buffers;
    struct ArrowArray** children;
    struct ArrowArray* dictionary;

    void (*release)(struct ArrowArray*);
    void* private_data;
};

#endif /* ARROW_C_DATA_INTERFACE */

#ifndef ARROW_C_STREAM_INTERFACE
#define ARROW_C_STREAM_INTERFACE

struct ArrowArrayStream {
    int (*get_schema)(struct ArrowArrayStream*, struct ArrowSchema* out);
    int (*get_next)(struct ArrowArrayStream*, struct ArrowArray* out);
    const char* (*get_last_error)(struct ArrowArrayStream*);
    void (*release)(struct ArrowArrayStream*);
    void* private_data;
};

#endif /* ARROW_C_STREAM_INTERFACE */

/* A struct array ("+s"), one child per layout column, named from the layout and flagged nullable when
 * the column is: STRING "u", INT64 "l", FLOAT64 "g", DECIMAL "d:18,<scale>", DATE "tdD",
 * TIME "ttu", TIMESTAMP "tsu:". The column buffers move into the arrays without a copy, except
 * DECIMAL, widened to 128 bits. A parallel read (degree_of_parallelism other than 1) is concatenated
 * into one array. Release both with their release callbacks. */
int32_t fw_read_arrow_file(const uint8_t* path, int32_t path_len, const fw_layout* layout,
                           const fw_read_options* options,
                           struct ArrowArray* out_array, struct ArrowSchema* out_schema, fw_read_result* out_result);
int32_t fw_read_arrow_memory(const uint8_t* data, int64_t data_len, const fw_layout* layout,
                             const fw_read_options* options,
                             struct ArrowArray* out_array, struct ArrowSchema* out_schema, fw_read_result* out_result);

/* The batches of a sequential reader (fw_reader_options) as an ArrowArrayStream; a failure makes
 * get_next return a non-zero errno and get_last_error describe it. fw_read_arrow_stream_memory reads
 * data lazily: keep it alive until the stream is released. */
int32_t fw_read_arrow_stream_file(const uint8_t* path, int32_t path_len, const fw_layout* layout,
                                  const fw_reader_options* options, struct ArrowArrayStream* out_stream);
int32_t fw_read_arrow_stream_memory(const uint8_t* data, int64_t data_len, const fw_layout* layout,
                                    const fw_reader_options* options, struct ArrowArrayStream* out_stream);

#ifdef __cplusplus
}
#endif

#endif /* FIXEDWIDTH_ARROW_H */
