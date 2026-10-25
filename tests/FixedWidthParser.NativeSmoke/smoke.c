/* Reference C consumer of the FixedWidthParser native library: loads it dynamically (NativeAOT ships
 * no import library on Windows), pins every ABI struct's layout, and exercises every export.
 *
 *     fw_smoke [path to FixedWidthParser.Native.{dll,so,dylib}]
 *
 * Prints "smoke ok" and exits 0 when everything matches. */
#include "fixedwidth.h"
#include "fixedwidth_arrow.h"

#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <windows.h>
typedef HMODULE fw_lib;
#define FW_OPEN(path) LoadLibraryA(path)
#define FW_SYM(lib, name) ((void*)GetProcAddress(lib, name))
#else
#include <dlfcn.h>
typedef void* fw_lib;
#define FW_OPEN(path) dlopen(path, RTLD_NOW)
#define FW_SYM(lib, name) dlsym(lib, name)
#endif

#ifndef FIXEDWIDTH_LIB_PATH_DEFAULT
#define FIXEDWIDTH_LIB_PATH_DEFAULT "FixedWidthParser.Native.dll"
#endif

#define FW_STATIC_ASSERT(cond, name) typedef char fw_static_assert_##name[(cond) ? 1 : -1]

/* Every published target is 64-bit with natural alignment; the offsets below follow from that. */
FW_STATIC_ASSERT(sizeof(void*) == 8, pointer_is_64_bit);
FW_STATIC_ASSERT(offsetof(fw_column_spec, name) == 8, spec_name);
FW_STATIC_ASSERT(offsetof(fw_column_spec, start) == 20, spec_start);
FW_STATIC_ASSERT(offsetof(fw_column_spec, scale) == 36, spec_scale);
FW_STATIC_ASSERT(offsetof(fw_column_spec, format) == 40, spec_format);
FW_STATIC_ASSERT(offsetof(fw_column_spec, overflow) == 68, spec_overflow);
FW_STATIC_ASSERT(offsetof(fw_column_spec, dictionary) == 72, spec_dictionary);
FW_STATIC_ASSERT(sizeof(fw_column_spec) == 80, spec_size);
FW_STATIC_ASSERT(sizeof(fw_layout_options) == 12, layout_options_size);
FW_STATIC_ASSERT(offsetof(fw_read_options, header_lines) == 8, read_header_lines);
FW_STATIC_ASSERT(offsetof(fw_read_options, skip_byte_order_mark) == 32, read_bom);
FW_STATIC_ASSERT(sizeof(fw_read_options) == 40, read_options_size);
FW_STATIC_ASSERT(offsetof(fw_column, length) == 8, column_length);
FW_STATIC_ASSERT(offsetof(fw_column, values) == 16, column_values);
FW_STATIC_ASSERT(offsetof(fw_column, data_len) == 40, column_data_len);
FW_STATIC_ASSERT(sizeof(fw_column) == 48, column_size);
FW_STATIC_ASSERT(offsetof(fw_table, row_count) == 8, table_row_count);
FW_STATIC_ASSERT(sizeof(fw_table) == 24, table_size);
FW_STATIC_ASSERT(sizeof(fw_read_result) == 24, read_result_size);
FW_STATIC_ASSERT(sizeof(fw_parse_error) == 24, parse_error_size);
FW_STATIC_ASSERT(offsetof(fw_reader_options, read) == 8, reader_read);
FW_STATIC_ASSERT(offsetof(fw_reader_options, batch_rows) == 48, reader_batch_rows);
FW_STATIC_ASSERT(sizeof(fw_reader_options) == 56, reader_options_size);
FW_STATIC_ASSERT(offsetof(fw_write_options, newline) == 8, write_newline);
FW_STATIC_ASSERT(offsetof(fw_write_options, batch_rows) == 24, write_batch_rows);
FW_STATIC_ASSERT(sizeof(fw_write_options) == 32, write_options_size);
FW_STATIC_ASSERT(sizeof(fw_buffer) == 16, buffer_size);

#define CHECK(cond)                                                                   \
    do {                                                                              \
        if (!(cond)) {                                                                \
            fprintf(stderr, "%s:%d: check failed: %s (%s)\n", __FILE__, __LINE__, #cond, \
                    last_error());                                                    \
            exit(1);                                                                  \
        }                                                                             \
    } while (0)

static struct {
    int32_t (*abi_version)(void);
    int32_t (*last_error)(uint8_t*, int32_t, int32_t*);
    int32_t (*last_parse_error)(fw_parse_error*);
    int32_t (*layout_create)(const fw_column_spec*, int32_t, const fw_layout_options*, fw_layout**);
    void (*layout_free)(fw_layout*);
    int32_t (*read_file)(const uint8_t*, int32_t, const fw_layout*, const fw_read_options*, fw_table*, fw_read_result*);
    int32_t (*read_memory)(const uint8_t*, int64_t, const fw_layout*, const fw_read_options*, fw_table*, fw_read_result*);
    void (*free_table)(fw_table*);
    int32_t (*reader_open_memory)(const uint8_t*, int64_t, const fw_layout*, const fw_reader_options*, fw_reader**);
    int32_t (*reader_next)(fw_reader*, fw_table*);
    void (*reader_close)(fw_reader*);
    int32_t (*read_arrow_memory)(const uint8_t*, int64_t, const fw_layout*, const fw_read_options*,
                                 struct ArrowArray*, struct ArrowSchema*, fw_read_result*);
    int32_t (*read_arrow_stream_memory)(const uint8_t*, int64_t, const fw_layout*, const fw_reader_options*,
                                        struct ArrowArrayStream*);
    int32_t (*write_file)(const uint8_t*, int32_t, const fw_table*, const fw_layout*, const fw_write_options*);
    int32_t (*write_memory)(const fw_table*, const fw_layout*, const fw_write_options*, fw_buffer*);
    void (*free_buffer)(fw_buffer*);
} fw;

static char error_text[1024];

static const char* last_error(void)
{
    int32_t length = 0;
    error_text[0] = 0;
    if (fw.last_error != NULL && fw.last_error((uint8_t*)error_text, sizeof(error_text) - 1, &length) == FW_OK) {
        error_text[length] = 0;
    }
    return error_text;
}

static void* symbol(fw_lib lib, const char* name)
{
    void* address = FW_SYM(lib, name);
    if (address == NULL) {
        fprintf(stderr, "missing export %s\n", name);
        exit(1);
    }
    return address;
}

static void load(const char* path)
{
    fw_lib lib = FW_OPEN(path);
    if (lib == NULL) {
        fprintf(stderr, "cannot load %s\n", path);
        exit(1);
    }
    *(void**)&fw.abi_version = symbol(lib, "fw_abi_version");
    *(void**)&fw.last_error = symbol(lib, "fw_last_error");
    *(void**)&fw.last_parse_error = symbol(lib, "fw_last_parse_error");
    *(void**)&fw.layout_create = symbol(lib, "fw_layout_create");
    *(void**)&fw.layout_free = symbol(lib, "fw_layout_free");
    *(void**)&fw.read_file = symbol(lib, "fw_read_file");
    *(void**)&fw.read_memory = symbol(lib, "fw_read_memory");
    *(void**)&fw.free_table = symbol(lib, "fw_free_table");
    *(void**)&fw.reader_open_memory = symbol(lib, "fw_reader_open_memory");
    *(void**)&fw.reader_next = symbol(lib, "fw_reader_next");
    *(void**)&fw.reader_close = symbol(lib, "fw_reader_close");
    *(void**)&fw.read_arrow_memory = symbol(lib, "fw_read_arrow_memory");
    *(void**)&fw.read_arrow_stream_memory = symbol(lib, "fw_read_arrow_stream_memory");
    *(void**)&fw.write_file = symbol(lib, "fw_write_file");
    *(void**)&fw.write_memory = symbol(lib, "fw_write_memory");
    *(void**)&fw.free_buffer = symbol(lib, "fw_free_buffer");
    symbol(lib, "fw_reader_open_file");
    symbol(lib, "fw_read_arrow_file");
    symbol(lib, "fw_read_arrow_stream_file");
}

/* A COTAHIST-like file: a header, quotes, an invalid line and a trailer. */
static const char CONTENT[] =
    "00COTAHIST.2023\n"
    "01PETR4       0000000123456000000000000001000\n"
    "01VALE3       0000000067890                  \n"
    "01ITUB4       0000000004321000000000000000050\n"
    "99TRAILER\n";

static fw_layout* cotahist_layout(void)
{
    fw_column_spec columns[4];
    memset(columns, 0, sizeof(columns));
    const char* names[4] = { "tipreg", "codneg", "preabe", "quatot" };
    const int32_t starts[4] = { 0, 2, 14, 27 };
    const int32_t lengths[4] = { 2, 12, 13, 18 };
    const int32_t types[4] = { FW_T_STRING, FW_T_STRING, FW_T_FLOAT64, FW_T_INT64 };
    for (int i = 0; i < 4; i++) {
        columns[i].struct_size = sizeof(fw_column_spec);
        columns[i].name = (const uint8_t*)names[i];
        columns[i].name_len = (int32_t)strlen(names[i]);
        columns[i].start = starts[i];
        columns[i].length = lengths[i];
        columns[i].type = types[i];
    }
    columns[2].scale = 2;
    columns[2].padding = '0';
    columns[3].nullable = 1;
    columns[3].padding = '0';
    fw_layout* layout = NULL;
    CHECK(fw.layout_create(columns, 4, NULL, &layout) == FW_OK);
    CHECK(layout != NULL);
    return layout;
}

static fw_read_options read_options(int32_t degree)
{
    fw_read_options options;
    memset(&options, 0, sizeof(options));
    options.struct_size = sizeof(options);
    options.header_lines = 1;
    options.skip_invalid = 1;
    options.degree_of_parallelism = degree;
    return options;
}

static void check_table(const fw_table* table)
{
    CHECK(table->column_count == 4);
    CHECK(table->row_count == 3);
    const int32_t* offsets = (const int32_t*)table->columns[1].values;
    CHECK(offsets[1] - offsets[0] == 5 && memcmp(table->columns[1].data, "PETR4", 5) == 0);
    const double* prices = (const double*)table->columns[2].values;
    CHECK(prices[0] == 1234.56 && prices[2] == 43.21);
    const int64_t* quantities = (const int64_t*)table->columns[3].values;
    CHECK(quantities[0] == 1000 && quantities[2] == 50);
    CHECK(table->columns[3].validity != NULL && (table->columns[3].validity[0] & 2) == 0);
}

static void reads(fw_layout* layout, const char* path)
{
    fw_read_options options = read_options(1);
    fw_table table;
    fw_read_result result = { sizeof(fw_read_result), 0, 0 };
    CHECK(fw.read_memory((const uint8_t*)CONTENT, sizeof(CONTENT) - 1, layout, &options, &table, &result) == FW_OK);
    check_table(&table);
    CHECK(result.lines_read == 3 && result.lines_skipped == 2);
    fw.free_table(&table);
    CHECK(table.columns == NULL);

    options = read_options(4);
    CHECK(fw.read_memory((const uint8_t*)CONTENT, sizeof(CONTENT) - 1, layout, &options, &table, NULL) == FW_OK);
    check_table(&table);
    fw.free_table(&table);

    FILE* file = fopen(path, "wb");
    CHECK(file != NULL);
    fwrite(CONTENT, 1, sizeof(CONTENT) - 1, file);
    fclose(file);
    options = read_options(1);
    CHECK(fw.read_file((const uint8_t*)path, (int32_t)strlen(path), layout, &options, &table, NULL) == FW_OK);
    check_table(&table);
    fw.free_table(&table);
}

static void parse_error(fw_layout* layout)
{
    fw_read_options options = read_options(1);
    options.skip_invalid = 0;
    fw_table table;
    CHECK(fw.read_memory((const uint8_t*)CONTENT, sizeof(CONTENT) - 1, layout, &options, &table, NULL) == FW_PARSE_ERROR);
    fw_parse_error error = { sizeof(fw_parse_error), 0, 0 };
    CHECK(fw.last_parse_error(&error) == FW_OK);
    CHECK(error.line_number == 5 && error.column == -1);
    CHECK(strstr(last_error(), "Line 5") != NULL);
}

static void batches(fw_layout* layout)
{
    fw_reader_options options;
    memset(&options, 0, sizeof(options));
    options.struct_size = sizeof(options);
    options.read = read_options(0);
    options.batch_rows = 2;
    fw_reader* reader = NULL;
    CHECK(fw.reader_open_memory((const uint8_t*)CONTENT, sizeof(CONTENT) - 1, layout, &options, &reader) == FW_OK);
    fw_table table;
    int64_t rows = 0;
    int32_t status;
    while ((status = fw.reader_next(reader, &table)) == FW_OK) {
        rows += table.row_count;
        fw.free_table(&table);
    }
    CHECK(status == FW_EOF && rows == 3);
    fw.reader_close(reader);
    CHECK(fw.reader_next(reader, &table) == FW_INVALID_HANDLE);
}

static void arrow(fw_layout* layout)
{
    fw_read_options options = read_options(1);
    struct ArrowArray array;
    struct ArrowSchema schema;
    CHECK(fw.read_arrow_memory((const uint8_t*)CONTENT, sizeof(CONTENT) - 1, layout, &options, &array, &schema, NULL) == FW_OK);
    CHECK(strcmp(schema.format, "+s") == 0 && schema.n_children == 4);
    CHECK(strcmp(schema.children[2]->format, "g") == 0 && strcmp(schema.children[2]->name, "preabe") == 0);
    CHECK(array.length == 3 && array.n_children == 4 && array.children[3]->null_count == 1);
    CHECK(((const double*)array.children[2]->buffers[1])[0] == 1234.56);
    array.release(&array);
    schema.release(&schema);
    CHECK(array.release == NULL && schema.release == NULL);

    fw_reader_options reader = { sizeof(fw_reader_options), read_options(1), 2 };
    struct ArrowArrayStream stream;
    CHECK(fw.read_arrow_stream_memory((const uint8_t*)CONTENT, sizeof(CONTENT) - 1, layout, &reader, &stream) == FW_OK);
    CHECK(stream.get_schema(&stream, &schema) == 0);
    schema.release(&schema);
    int64_t rows = 0;
    while (stream.get_next(&stream, &array) == 0 && array.release != NULL) {
        rows += array.length;
        array.release(&array);
    }
    CHECK(rows == 3);
    stream.release(&stream);
}

static void writes(fw_layout* layout, const char* path)
{
    fw_read_options read = read_options(1);
    fw_table table;
    CHECK(fw.read_memory((const uint8_t*)CONTENT, sizeof(CONTENT) - 1, layout, &read, &table, NULL) == FW_OK);
    fw_write_options options;
    memset(&options, 0, sizeof(options));
    options.struct_size = sizeof(options);
    options.degree_of_parallelism = 4;
    options.batch_rows = 1;
    fw_buffer buffer;
    CHECK(fw.write_memory(&table, layout, &options, &buffer) == FW_OK);
    const char* first = "01PETR4       0000000123456000000000000001000\n";
    CHECK(buffer.length == 3 * 46 && memcmp(buffer.data, first, 46) == 0);
    CHECK(fw.write_file((const uint8_t*)path, (int32_t)strlen(path), &table, layout, &options) == FW_OK);
    FILE* file = fopen(path, "rb");
    CHECK(file != NULL);
    char written[3 * 46];
    CHECK(fread(written, 1, sizeof(written), file) == sizeof(written));
    fclose(file);
    CHECK(memcmp(written, buffer.data, sizeof(written)) == 0);
    fw.free_buffer(&buffer);
    CHECK(buffer.data == NULL);
    fw.free_table(&table);
}

static void null_arguments(fw_layout* layout)
{
    fw_table table;
    fw_layout* other = NULL;
    CHECK(fw.layout_create(NULL, 1, NULL, &other) == FW_INVALID_ARGUMENT);
    CHECK(fw.read_memory((const uint8_t*)CONTENT, 4, layout, NULL, NULL, NULL) == FW_INVALID_ARGUMENT);
    CHECK(fw.read_memory(NULL, 4, layout, NULL, &table, NULL) == FW_INVALID_ARGUMENT);
    CHECK(fw.last_error(NULL, 0, NULL) == FW_INVALID_ARGUMENT);
    CHECK(fw.last_parse_error(NULL) == FW_INVALID_ARGUMENT);
    CHECK(fw.write_memory(NULL, layout, NULL, NULL) == FW_INVALID_ARGUMENT);

    fw_read_options bad = read_options(1);
    bad.struct_size = 12;
    CHECK(fw.read_memory((const uint8_t*)CONTENT, 4, layout, &bad, &table, NULL) == FW_INVALID_ARGUMENT);
    CHECK(strstr(last_error(), "struct_size") != NULL);

    fw_column_spec overlap[2];
    memset(overlap, 0, sizeof(overlap));
    for (int i = 0; i < 2; i++) {
        overlap[i].struct_size = sizeof(fw_column_spec);
        overlap[i].name = (const uint8_t*)(i == 0 ? "a" : "b");
        overlap[i].name_len = 1;
        overlap[i].start = i * 2;
        overlap[i].length = 4;
    }
    CHECK(fw.layout_create(overlap, 2, NULL, &other) == FW_INVALID_ARGUMENT && other == NULL);
    CHECK(strstr(last_error(), "Overlapping") != NULL);

    fw.layout_free(layout);
    CHECK(fw.read_memory((const uint8_t*)CONTENT, 4, layout, NULL, &table, NULL) == FW_INVALID_HANDLE);
    fw.layout_free(layout);
    fw.free_table(NULL);
    fw.free_buffer(NULL);
}

int main(int argc, char** argv)
{
    load(argc > 1 ? argv[1] : FIXEDWIDTH_LIB_PATH_DEFAULT);
    CHECK(fw.abi_version() == FW_ABI_VERSION);
    const char* path = "fw_smoke.txt";

    fw_layout* layout = cotahist_layout();
    reads(layout, path);
    parse_error(layout);
    batches(layout);
    arrow(layout);
    writes(layout, path);
    null_arguments(layout);
    remove(path);

    printf("smoke ok\n");
    return 0;
}
