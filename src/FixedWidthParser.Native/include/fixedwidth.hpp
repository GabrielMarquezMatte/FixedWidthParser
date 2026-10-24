// Header-only C++23 wrapper over fixedwidth.h: RAII handles, options structs with defaults, and
// std::expected results. Link against the native library (fw::fixedwidth in cpp/CMakeLists.txt).
#pragma once

#include "fixedwidth.h"

#include <cstddef>
#include <cstdint>
#include <expected>
#include <optional>
#include <span>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace fw
{
    enum class Type : int32_t
    {
        String = FW_T_STRING,
        Int64 = FW_T_INT64,
        Float64 = FW_T_FLOAT64,
        Decimal = FW_T_DECIMAL,
        Date = FW_T_DATE,
        Time = FW_T_TIME,
        Timestamp = FW_T_TIMESTAMP,
    };

    enum class Trim : int32_t { Both = FW_TRIM_BOTH, Trailing = FW_TRIM_TRAILING, Leading = FW_TRIM_LEADING };
    enum class Align : int32_t { Default = FW_ALIGN_DEFAULT, Left = FW_ALIGN_LEFT, Right = FW_ALIGN_RIGHT };
    enum class Overflow : int32_t { Throw = FW_OVERFLOW_THROW, Truncate = FW_OVERFLOW_TRUNCATE };

    /// A failed call: its FW_* status and the library's message; line and column after FW_PARSE_ERROR.
    struct Error
    {
        int32_t status = FW_ERROR;
        std::string message;
        int64_t line_number = 0;
        int32_t column = -1;
    };

    template <class T>
    using Result = std::expected<T, Error>;

    namespace detail
    {
        inline Error last_error(int32_t status)
        {
            Error error;
            error.status = status;
            int32_t length = 0;
            std::string buffer(1024, '\0');
            if (fw_last_error(reinterpret_cast<uint8_t*>(buffer.data()), static_cast<int32_t>(buffer.size()), &length) == FW_BUFFER_TOO_SMALL)
            {
                buffer.resize(static_cast<size_t>(length));
                fw_last_error(reinterpret_cast<uint8_t*>(buffer.data()), length, &length);
            }
            buffer.resize(static_cast<size_t>(length));
            error.message = std::move(buffer);
            if (status == FW_PARSE_ERROR)
            {
                fw_parse_error parse{.struct_size = sizeof(fw_parse_error), .line_number = 0, .column = -1};
                fw_last_parse_error(&parse);
                error.line_number = parse.line_number;
                error.column = parse.column;
            }
            return error;
        }

        inline Result<void> check(int32_t status)
        {
            if (status == FW_OK)
            {
                return {};
            }
            return std::unexpected(last_error(status));
        }

        inline const uint8_t* bytes(std::string_view text)
        {
            return reinterpret_cast<const uint8_t*>(text.data());
        }
    }

    /// One column: name, zero-based byte start, byte length and type, plus everything optional.
    struct ColumnSpec
    {
        std::string name;
        int32_t start = 0;
        int32_t length = 0;
        Type type = Type::String;
        bool nullable = false;
        int32_t scale = 0;
        std::optional<std::string> format;
        char trim_char = 0;
        Trim trim_mode = Trim::Both;
        Align alignment = Align::Default;
        char padding = 0;
        Overflow overflow = Overflow::Throw;
    };

    struct LayoutOptions
    {
        int32_t line_length = 0;
        char filler = 0;
    };

    /// An immutable, validated layout; move-only, shareable across threads by reference.
    class Layout
    {
    public:
        static Result<Layout> create(std::span<const ColumnSpec> columns, const LayoutOptions& options = {})
        {
            std::vector<fw_column_spec> specs;
            specs.reserve(columns.size());
            for (const ColumnSpec& column : columns)
            {
                specs.push_back(fw_column_spec{
                    .struct_size = sizeof(fw_column_spec),
                    .name = detail::bytes(column.name),
                    .name_len = static_cast<int32_t>(column.name.size()),
                    .start = column.start,
                    .length = column.length,
                    .type = static_cast<int32_t>(column.type),
                    .nullable = column.nullable ? 1 : 0,
                    .scale = column.scale,
                    .format = column.format ? detail::bytes(*column.format) : nullptr,
                    .format_len = column.format ? static_cast<int32_t>(column.format->size()) : 0,
                    .trim_char = static_cast<unsigned char>(column.trim_char),
                    .trim_mode = static_cast<int32_t>(column.trim_mode),
                    .alignment = static_cast<int32_t>(column.alignment),
                    .padding = static_cast<unsigned char>(column.padding),
                    .overflow = static_cast<int32_t>(column.overflow),
                });
            }
            const fw_layout_options raw{.struct_size = sizeof(fw_layout_options), .line_length = options.line_length,
                                        .filler = static_cast<unsigned char>(options.filler)};
            fw_layout* handle = nullptr;
            if (auto ok = detail::check(fw_layout_create(specs.data(), static_cast<int32_t>(specs.size()), &raw, &handle)); !ok)
            {
                return std::unexpected(ok.error());
            }
            return Layout(handle);
        }

        Layout(Layout&& other) noexcept : handle_(std::exchange(other.handle_, nullptr)) {}
        Layout& operator=(Layout&& other) noexcept
        {
            std::swap(handle_, other.handle_);
            return *this;
        }
        Layout(const Layout&) = delete;
        Layout& operator=(const Layout&) = delete;
        ~Layout()
        {
            if (handle_ != nullptr)
            {
                fw_layout_free(handle_);
            }
        }

        const fw_layout* get() const noexcept { return handle_; }

    private:
        explicit Layout(fw_layout* handle) : handle_(handle) {}
        fw_layout* handle_;
    };

    struct ReadOptions
    {
        int64_t header_lines = 0;
        bool skip_invalid = false;
        int32_t degree_of_parallelism = 0;
        int32_t encoding = 0;
        int32_t max_line_length = 0;
        std::optional<bool> skip_byte_order_mark;

        fw_read_options to_c() const
        {
            return {
                .struct_size = sizeof(fw_read_options),
                .header_lines = header_lines,
                .skip_invalid = skip_invalid ? 1 : 0,
                .degree_of_parallelism = degree_of_parallelism,
                .encoding = encoding,
                .max_line_length = max_line_length,
                .skip_byte_order_mark = !skip_byte_order_mark ? FW_OPT_DEFAULT : (*skip_byte_order_mark ? FW_OPT_TRUE : FW_OPT_FALSE),
            };
        }
    };

    struct ReaderOptions
    {
        ReadOptions read;
        int64_t batch_rows = 0;

        fw_reader_options to_c() const
        {
            return {.struct_size = sizeof(fw_reader_options), .read = read.to_c(), .batch_rows = batch_rows};
        }
    };

    struct WriteOptions
    {
        int32_t encoding = 0;
        std::string newline = "\n";
        int32_t degree_of_parallelism = 0;
        int64_t batch_rows = 0;
    };

    /// A table the library published; move-only, freed on destruction.
    class Table
    {
    public:
        explicit Table(fw_table table, fw_read_result result = {}) noexcept : table_(table), result_(result) {}
        Table(Table&& other) noexcept : table_(std::exchange(other.table_, {})), result_(other.result_) {}
        Table& operator=(Table&& other) noexcept
        {
            std::swap(table_, other.table_);
            std::swap(result_, other.result_);
            return *this;
        }
        Table(const Table&) = delete;
        Table& operator=(const Table&) = delete;
        ~Table() { fw_free_table(&table_); }

        int64_t row_count() const noexcept { return table_.row_count; }
        int32_t column_count() const noexcept { return table_.column_count; }
        Type type(int32_t column) const noexcept { return static_cast<Type>(table_.columns[column].type); }
        int64_t lines_read() const noexcept { return result_.lines_read; }
        int64_t lines_skipped() const noexcept { return result_.lines_skipped; }
        const fw_table& get() const noexcept { return table_; }

        /// The values of a non-string column as stored (see FW_T_*): int64_t, double or int32_t.
        template <class T>
        std::span<const T> values(int32_t column) const noexcept
        {
            const fw_column& raw = table_.columns[column];
            return {static_cast<const T*>(raw.values), static_cast<size_t>(raw.length)};
        }

        std::string_view string(int32_t column, int64_t row) const noexcept
        {
            const fw_column& raw = table_.columns[column];
            const int32_t* offsets = static_cast<const int32_t*>(raw.values);
            const char* data = reinterpret_cast<const char*>(raw.data);
            return {data == nullptr ? "" : data + offsets[row], static_cast<size_t>(offsets[row + 1] - offsets[row])};
        }

        bool is_valid(int32_t column, int64_t row) const noexcept
        {
            const uint8_t* bits = table_.columns[column].validity;
            return bits == nullptr || (bits[row >> 3] & (1 << (row & 7))) != 0;
        }

    private:
        fw_table table_;
        fw_read_result result_;
    };

    inline Result<Table> read_file(std::string_view path, const Layout& layout, const ReadOptions& options = {})
    {
        fw_table table{};
        fw_read_result result{.struct_size = sizeof(fw_read_result), .lines_read = 0, .lines_skipped = 0};
        const fw_read_options raw = options.to_c();
        if (auto ok = detail::check(fw_read_file(detail::bytes(path), static_cast<int32_t>(path.size()), layout.get(), &raw, &table, &result)); !ok)
        {
            return std::unexpected(ok.error());
        }
        return Table(table, result);
    }

    inline Result<Table> read_memory(std::string_view data, const Layout& layout, const ReadOptions& options = {})
    {
        fw_table table{};
        fw_read_result result{.struct_size = sizeof(fw_read_result), .lines_read = 0, .lines_skipped = 0};
        const fw_read_options raw = options.to_c();
        if (auto ok = detail::check(fw_read_memory(detail::bytes(data), static_cast<int64_t>(data.size()), layout.get(), &raw, &table, &result)); !ok)
        {
            return std::unexpected(ok.error());
        }
        return Table(table, result);
    }

    /// A sequential read in tables of at most batch_rows rows. Use from one thread at a time.
    class Reader
    {
    public:
        static Result<Reader> open_file(std::string_view path, const Layout& layout, const ReaderOptions& options = {})
        {
            const fw_reader_options raw = options.to_c();
            fw_reader* reader = nullptr;
            if (auto ok = detail::check(fw_reader_open_file(detail::bytes(path), static_cast<int32_t>(path.size()), layout.get(), &raw, &reader)); !ok)
            {
                return std::unexpected(ok.error());
            }
            return Reader(reader);
        }

        /// Reads data in place: it must outlive the reader.
        static Result<Reader> open_memory(std::string_view data, const Layout& layout, const ReaderOptions& options = {})
        {
            const fw_reader_options raw = options.to_c();
            fw_reader* reader = nullptr;
            if (auto ok = detail::check(fw_reader_open_memory(detail::bytes(data), static_cast<int64_t>(data.size()), layout.get(), &raw, &reader)); !ok)
            {
                return std::unexpected(ok.error());
            }
            return Reader(reader);
        }

        Reader(Reader&& other) noexcept : reader_(std::exchange(other.reader_, nullptr)) {}
        Reader& operator=(Reader&& other) noexcept
        {
            std::swap(reader_, other.reader_);
            return *this;
        }
        Reader(const Reader&) = delete;
        Reader& operator=(const Reader&) = delete;
        ~Reader()
        {
            if (reader_ != nullptr)
            {
                fw_reader_close(reader_);
            }
        }

        /// The next batch, or an empty optional at the end.
        Result<std::optional<Table>> next()
        {
            fw_table table{};
            const int32_t status = fw_reader_next(reader_, &table);
            if (status == FW_EOF)
            {
                return std::optional<Table>{};
            }
            if (auto ok = detail::check(status); !ok)
            {
                return std::unexpected(ok.error());
            }
            return std::optional<Table>(Table(table));
        }

    private:
        explicit Reader(fw_reader* reader) : reader_(reader) {}
        fw_reader* reader_;
    };

    namespace detail
    {
        inline fw_write_options write_options(const WriteOptions& options)
        {
            return {
                .struct_size = sizeof(fw_write_options),
                .encoding = options.encoding,
                .newline = bytes(options.newline),
                .newline_len = static_cast<int32_t>(options.newline.size()),
                .degree_of_parallelism = options.degree_of_parallelism,
                .batch_rows = options.batch_rows,
            };
        }
    }

    inline Result<void> write_file(std::string_view path, const fw_table& table, const Layout& layout, const WriteOptions& options = {})
    {
        const fw_write_options raw = detail::write_options(options);
        return detail::check(fw_write_file(detail::bytes(path), static_cast<int32_t>(path.size()), &table, layout.get(), &raw));
    }

    inline Result<void> write_file(std::string_view path, const Table& table, const Layout& layout, const WriteOptions& options = {})
    {
        return write_file(path, table.get(), layout, options);
    }

    inline Result<std::vector<uint8_t>> write_memory(const fw_table& table, const Layout& layout, const WriteOptions& options = {})
    {
        const fw_write_options raw = detail::write_options(options);
        fw_buffer buffer{};
        if (auto ok = detail::check(fw_write_memory(&table, layout.get(), &raw, &buffer)); !ok)
        {
            return std::unexpected(ok.error());
        }
        std::vector<uint8_t> bytes(buffer.data, buffer.data + buffer.length);
        fw_free_buffer(&buffer);
        return bytes;
    }

    inline Result<std::vector<uint8_t>> write_memory(const Table& table, const Layout& layout, const WriteOptions& options = {})
    {
        return write_memory(table.get(), layout, options);
    }
}
