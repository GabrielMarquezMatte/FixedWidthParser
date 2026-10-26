// Header-only C++23 wrapper over fixedwidth.h: RAII handles, options structs with defaults,
// std::expected results, and structs mapped to columns (FixedWidthMapper). Link against the native
// library (fw::fixedwidth in cpp/CMakeLists.txt).
#pragma once

#include "fixedwidth.h"

#include <algorithm>
#include <array>
#include <chrono>
#include <compare>
#include <cstddef>
#include <cstdint>
#include <expected>
#include <istream>
#include <ostream>
#include <iterator>
#include <limits>
#include <optional>
#include <ranges>
#include <span>
#include <string>
#include <string_view>
#include <tuple>
#include <type_traits>
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

        // fw_stream.read over a std::istream: a short count at the end, -1 once the stream is bad. An exception
        // must not unwind into the library, so it fails the read too.
        inline int64_t read_istream(void* user_data, uint8_t* buffer, int64_t length) noexcept
        {
            constexpr std::string_view failed = "the std::istream failed";
            try
            {
                auto& in = *static_cast<std::istream*>(user_data);
                in.read(reinterpret_cast<char*>(buffer), static_cast<std::streamsize>(length));
                if (!in.bad())
                {
                    return static_cast<int64_t>(in.gcount());
                }
            }
            catch (...)
            {
            }
            fw_set_source_error(bytes(failed), static_cast<int32_t>(failed.size()));
            return -1;
        }

        /// An fw_stream reading `in`, which the caller keeps alive; nothing to release.
        inline fw_stream istream_source(std::istream& in)
        {
            return {.struct_size = sizeof(fw_stream), .user_data = &in, .read = &read_istream, .release = nullptr};
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
        /// A String column reaches Arrow dictionary-encoded (int32 indices into its distinct values); fw::Table keeps strings.
        bool dictionary = false;
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
                    .dictionary = column.dictionary ? 1 : 0,
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

        /// Reads `in` forward, a batch at a time, without seeking: it must outlive the reader.
        static Result<Reader> open_stream(std::istream& in, const Layout& layout, const ReaderOptions& options = {})
        {
            const fw_reader_options raw = options.to_c();
            const fw_stream stream = detail::istream_source(in);
            fw_reader* reader = nullptr;
            if (auto ok = detail::check(fw_reader_open_stream(&stream, layout.get(), &raw, &reader)); !ok)
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

    namespace detail
    {
        // fw_sink.write over a std::ostream: -1 once the stream fails, or if it throws.
        inline int32_t write_ostream(void* user_data, const uint8_t* buffer, int64_t length) noexcept
        {
            constexpr std::string_view failed = "the std::ostream failed";
            try
            {
                auto& out = *static_cast<std::ostream*>(user_data);
                out.write(reinterpret_cast<const char*>(buffer), static_cast<std::streamsize>(length));
                if (!out.fail())
                {
                    return 0;
                }
            }
            catch (...)
            {
            }
            fw_set_source_error(bytes(failed), static_cast<int32_t>(failed.size()));
            return -1;
        }
    }

    /// Writes to `out` in order, a batch at a time, while batches format in parallel; memory stays at a batch per thread.
    inline Result<void> write_stream(std::ostream& out, const fw_table& table, const Layout& layout, const WriteOptions& options = {})
    {
        const fw_write_options raw = detail::write_options(options);
        const fw_sink sink{.struct_size = sizeof(fw_sink), .user_data = &out, .write = &detail::write_ostream};
        return detail::check(fw_write_stream(&table, layout.get(), &raw, &sink));
    }

    inline Result<void> write_stream(std::ostream& out, const Table& table, const Layout& layout, const WriteOptions& options = {})
    {
        return write_stream(out, table.get(), layout, options);
    }

    // ---- Struct mapping ----------------------------------------------------------------------------
    //
    // Specialize FixedWidthMapper<T> with a `static constexpr auto get_bindings()` returning a std::tuple of
    // make_field results; read_file<T>, read_memory<T>, TypedReader<T>, write_file and write_memory all use
    // that one mapping. A field's type picks its column type: std::string or std::string_view -> String,
    // an integer -> Int64 (Decimal, unscaled, when the field sets a scale), float or double -> Float64,
    // std::chrono::sys_days or year_month_day -> Date, std::chrono::microseconds or
    // hh_mm_ss<microseconds> -> Time, std::chrono::system_clock::time_point -> Timestamp. std::optional<T>
    // makes the column nullable.

    /// The optional parts of a mapped column; see ColumnSpec.
    struct FieldOptions
    {
        int32_t scale = 0;
        const char* format = nullptr;
        char trim_char = 0;
        Trim trim_mode = Trim::Both;
        Align alignment = Align::Default;
        char padding = 0;
        Overflow overflow = Overflow::Throw;
    };

    namespace detail
    {
        template <class Class, class T>
        struct FieldBinding
        {
            const char* name;
            int32_t start;
            int32_t length;
            T Class::*member;
            FieldOptions options;
            using FieldType = T;
        };
    }

    /// Binds a member to the column at bytes [start, start + length).
    template <class Class, class T>
    constexpr detail::FieldBinding<Class, T> make_field(const char* name, int32_t start, int32_t length, T Class::*member,
                                                        FieldOptions options = {})
    {
        return {name, start, length, member, options};
    }

    /// Maps a row type to columns: specialize with `static constexpr auto get_bindings()`.
    template <class T>
    struct FixedWidthMapper;

    namespace detail
    {
        template <class T>
        struct IsOptional : std::false_type
        {
            using Inner = T;
        };

        template <class T>
        struct IsOptional<std::optional<T>> : std::true_type
        {
            using Inner = T;
        };

        template <class T>
        using unwrap_optional_t = typename IsOptional<T>::Inner;

        template <class T>
        inline constexpr bool always_false = false;

        template <class T>
        using standard_integer_t = std::conditional_t<std::is_signed_v<T>, std::make_signed_t<T>, std::make_unsigned_t<T>>;

        using micros = std::chrono::microseconds;

        template <class Field>
        constexpr Type type_of(int32_t scale)
        {
            if constexpr (std::is_same_v<Field, std::string> || std::is_same_v<Field, std::string_view>)
            {
                return Type::String;
            }
            else if constexpr (std::is_integral_v<Field> && !std::is_same_v<Field, bool>)
            {
                return scale > 0 ? Type::Decimal : Type::Int64;
            }
            else if constexpr (std::is_floating_point_v<Field>)
            {
                return Type::Float64;
            }
            else if constexpr (std::is_same_v<Field, std::chrono::sys_days> || std::is_same_v<Field, std::chrono::year_month_day>)
            {
                return Type::Date;
            }
            else if constexpr (std::is_same_v<Field, micros> || std::is_same_v<Field, std::chrono::hh_mm_ss<micros>>)
            {
                return Type::Time;
            }
            else if constexpr (std::is_same_v<Field, std::chrono::system_clock::time_point>)
            {
                return Type::Timestamp;
            }
            else
            {
                static_assert(always_false<Field>, "no fixed-width column type for this field type");
            }
        }

        template <class T>
        constexpr auto bindings_of()
        {
            return FixedWidthMapper<T>::get_bindings();
        }

        template <class T>
        inline constexpr size_t field_count = std::tuple_size_v<decltype(bindings_of<T>())>;

        template <class Class, class T>
        ColumnSpec spec_of(const FieldBinding<Class, T>& binding)
        {
            const FieldOptions& options = binding.options;
            return ColumnSpec{
                .name = binding.name,
                .start = binding.start,
                .length = binding.length,
                .type = type_of<unwrap_optional_t<T>>(options.scale),
                .nullable = IsOptional<T>::value,
                .scale = options.scale,
                .format = options.format == nullptr ? std::nullopt : std::optional<std::string>(options.format),
                .trim_char = options.trim_char,
                .trim_mode = options.trim_mode,
                .alignment = options.alignment,
                .padding = options.padding,
                .overflow = options.overflow,
            };
        }

        template <class Field>
        Field value_at(const Table& table, int32_t column, int64_t row)
        {
            if constexpr (std::is_same_v<Field, std::string> || std::is_same_v<Field, std::string_view>)
            {
                return Field(table.string(column, row));
            }
            else if constexpr (std::is_integral_v<Field>)
            {
                return static_cast<Field>(table.values<int64_t>(column)[row]);
            }
            else if constexpr (std::is_floating_point_v<Field>)
            {
                return static_cast<Field>(table.values<double>(column)[row]);
            }
            else if constexpr (std::is_same_v<Field, std::chrono::sys_days> || std::is_same_v<Field, std::chrono::year_month_day>)
            {
                return Field(std::chrono::sys_days(std::chrono::days(table.values<int32_t>(column)[row])));
            }
            else if constexpr (std::is_same_v<Field, micros> || std::is_same_v<Field, std::chrono::hh_mm_ss<micros>>)
            {
                return Field(micros(table.values<int64_t>(column)[row]));
            }
            else
            {
                return std::chrono::time_point_cast<std::chrono::system_clock::duration>(
                    std::chrono::sys_time<micros>(micros(table.values<int64_t>(column)[row])));
            }
        }

        template <class T, size_t... Is>
        T row_at(const Table& table, int64_t row, std::index_sequence<Is...>)
        {
            static constexpr auto bindings = bindings_of<T>();
            T instance{};
            // A null leaves a plain field at its default and an optional one empty.
            (..., (table.is_valid(static_cast<int32_t>(Is), row)
                       ? void(instance.*(std::get<Is>(bindings).member) =
                                  value_at<unwrap_optional_t<typename std::tuple_element_t<Is, std::remove_cv_t<decltype(bindings)>>::FieldType>>(
                                      table, static_cast<int32_t>(Is), row))
                       : void()));
            return instance;
        }

        /// The int64 a narrower or unsigned integer field cannot hold is refused once, when the table is
        /// handed over, instead of wrapping on every read.
        template <class T, size_t... Is>
        Result<void> check_ranges(const Table& table, std::index_sequence<Is...>)
        {
            static constexpr auto bindings = bindings_of<T>();
            Result<void> result;
            auto check = [&](auto index)
            {
                constexpr size_t I = decltype(index)::value;
                using Field = unwrap_optional_t<typename std::tuple_element_t<I, std::remove_cv_t<decltype(bindings)>>::FieldType>;
                if constexpr (std::is_integral_v<Field> && !std::is_same_v<Field, int64_t>)
                {
                    const auto values = table.values<int64_t>(static_cast<int32_t>(I));
                    for (int64_t row = 0; row < table.row_count(); ++row)
                    {
                        if (table.is_valid(static_cast<int32_t>(I), row) && !std::in_range<standard_integer_t<Field>>(values[row]))
                        {
                            result = std::unexpected(Error{
                                .status = FW_INVALID_ARGUMENT,
                                .message = "column '" + std::string(std::get<I>(bindings).name) + "' holds " + std::to_string(values[row]) +
                                           " at row " + std::to_string(row) + ", which its field's type cannot represent",
                            });
                            return false;
                        }
                    }
                }
                return true;
            };
            (void)(... && check(std::integral_constant<size_t, Is>{}));
            return result;
        }
    }

    /// The layout a FixedWidthMapper<T> describes.
    template <class T>
    Result<Layout> layout_for(const LayoutOptions& options = {})
    {
        static constexpr auto bindings = detail::bindings_of<T>();
        const auto columns = std::apply([](const auto&... binding) { return std::array<ColumnSpec, sizeof...(binding)>{detail::spec_of(binding)...}; },
                                        bindings);
        return Layout::create(columns, options);
    }

    /// The rows of a read, held in the library's columnar memory and converted to T on access; a
    /// random-access range of T. A std::string_view field points into this view.
    template <class T>
    class TableView
    {
    public:
        class iterator
        {
        public:
            using iterator_category = std::random_access_iterator_tag;
            using iterator_concept = std::random_access_iterator_tag;
            using value_type = T;
            using difference_type = std::ptrdiff_t;
            using pointer = void;
            using reference = T;

            iterator() = default;

            T operator*() const { return (*view_)[row_]; }
            T operator[](difference_type n) const { return (*view_)[row_ + n]; }

            iterator& operator++() { ++row_; return *this; }
            iterator operator++(int) { iterator copy = *this; ++row_; return copy; }
            iterator& operator--() { --row_; return *this; }
            iterator operator--(int) { iterator copy = *this; --row_; return copy; }
            iterator& operator+=(difference_type n) { row_ += n; return *this; }
            iterator& operator-=(difference_type n) { row_ -= n; return *this; }

            friend iterator operator+(iterator it, difference_type n) { return it += n; }
            friend iterator operator+(difference_type n, iterator it) { return it += n; }
            friend iterator operator-(iterator it, difference_type n) { return it -= n; }
            friend difference_type operator-(const iterator& lhs, const iterator& rhs) { return lhs.row_ - rhs.row_; }
            friend bool operator==(const iterator& lhs, const iterator& rhs) { return lhs.row_ == rhs.row_; }
            friend std::strong_ordering operator<=>(const iterator& lhs, const iterator& rhs) { return lhs.row_ <=> rhs.row_; }

        private:
            friend class TableView;
            iterator(const TableView* view, int64_t row) : view_(view), row_(row) {}

            const TableView* view_ = nullptr;
            int64_t row_ = 0;
        };

        /// Takes over a table read through layout_for<T>(), checking that every integer fits its field.
        static Result<TableView> adopt(Table table)
        {
            if (auto ok = detail::check_ranges<T>(table, std::make_index_sequence<detail::field_count<T>>{}); !ok)
            {
                return std::unexpected(ok.error());
            }
            return TableView(std::move(table));
        }

        iterator begin() const { return iterator(this, 0); }
        iterator end() const { return iterator(this, size()); }
        int64_t size() const noexcept { return table_.row_count(); }
        bool empty() const noexcept { return size() == 0; }

        /// Row `row`, unchecked; at() is the checked read.
        T operator[](int64_t row) const { return detail::row_at<T>(table_, row, std::make_index_sequence<detail::field_count<T>>{}); }

        std::optional<T> at(int64_t row) const
        {
            if (row < 0 || row >= size())
            {
                return std::nullopt;
            }
            return (*this)[row];
        }

        /// The columns underneath, with the read's line counts.
        const Table& table() const noexcept { return table_; }

    private:
        explicit TableView(Table table) noexcept : table_(std::move(table)) {}
        Table table_;
    };

    /// Reads a whole file as T.
    template <class T>
    Result<TableView<T>> read_file(std::string_view path, const ReadOptions& options = {})
    {
        return layout_for<T>()
            .and_then([&](const Layout& layout) { return read_file(path, layout, options); })
            .and_then([](Table table) { return TableView<T>::adopt(std::move(table)); });
    }

    /// Reads a whole buffer as T. data need not outlive the call, but std::string_view fields point into the view.
    template <class T>
    Result<TableView<T>> read_memory(std::string_view data, const ReadOptions& options = {})
    {
        return layout_for<T>()
            .and_then([&](const Layout& layout) { return read_memory(data, layout, options); })
            .and_then([](Table table) { return TableView<T>::adopt(std::move(table)); });
    }

    /// A sequential read as T in views of at most batch_rows rows. Use from one thread at a time.
    template <class T>
    class TypedReader
    {
    public:
        static Result<TypedReader> open_file(std::string_view path, const ReaderOptions& options = {})
        {
            return layout_for<T>()
                .and_then([&](const Layout& layout) { return Reader::open_file(path, layout, options); })
                .transform([](Reader reader) { return TypedReader(std::move(reader)); });
        }

        /// Reads data in place: it must outlive the reader.
        static Result<TypedReader> open_memory(std::string_view data, const ReaderOptions& options = {})
        {
            return layout_for<T>()
                .and_then([&](const Layout& layout) { return Reader::open_memory(data, layout, options); })
                .transform([](Reader reader) { return TypedReader(std::move(reader)); });
        }

        /// Reads `in` forward without seeking: it must outlive the reader.
        static Result<TypedReader> open_stream(std::istream& in, const ReaderOptions& options = {})
        {
            return layout_for<T>()
                .and_then([&](const Layout& layout) { return Reader::open_stream(in, layout, options); })
                .transform([](Reader reader) { return TypedReader(std::move(reader)); });
        }

        /// The next batch, or an empty optional at the end.
        Result<std::optional<TableView<T>>> next()
        {
            auto batch = reader_.next();
            if (!batch)
            {
                return std::unexpected(std::move(batch.error()));
            }
            if (!batch->has_value())
            {
                return std::optional<TableView<T>>{};
            }
            return TableView<T>::adopt(std::move(**batch)).transform([](TableView<T> view) { return std::optional<TableView<T>>(std::move(view)); });
        }

    private:
        explicit TypedReader(Reader reader) : reader_(std::move(reader)) {}
        Reader reader_;
    };

    namespace detail
    {
        /// One field's column on the way out: the values as the library stores them, and a validity
        /// bitmap once a null appears.
        template <class T>
        struct ColumnBuilder
        {
            using Field = unwrap_optional_t<T>;
            static constexpr bool is_string = std::is_same_v<Field, std::string> || std::is_same_v<Field, std::string_view>;
            using Stored = std::conditional_t<std::is_floating_point_v<Field>, double,
                                              std::conditional_t<std::is_same_v<Field, std::chrono::sys_days> ||
                                                                     std::is_same_v<Field, std::chrono::year_month_day>,
                                                                 int32_t, int64_t>>;

            std::vector<Stored> values;
            std::vector<int32_t> offsets{0};
            std::string data;
            std::vector<uint8_t> validity;
            int64_t rows = 0;
            bool any_null = false;
            bool out_of_range = false;

            void push(const T& value)
            {
                if (validity.size() * 8 <= static_cast<size_t>(rows))
                {
                    validity.push_back(0);
                }
                if constexpr (IsOptional<T>::value)
                {
                    if (!value.has_value())
                    {
                        any_null = true;
                        if constexpr (is_string)
                        {
                            offsets.push_back(offsets.back());
                        }
                        else
                        {
                            values.push_back(Stored{});
                        }
                        ++rows;
                        return;
                    }
                    append(*value);
                }
                else
                {
                    append(value);
                }
                validity[static_cast<size_t>(rows >> 3)] |= static_cast<uint8_t>(1 << (rows & 7));
                ++rows;
            }

            void append(const Field& value)
            {
                if constexpr (is_string)
                {
                    data.append(value);
                    offsets.push_back(static_cast<int32_t>(std::min<size_t>(data.size(), std::numeric_limits<int32_t>::max())));
                }
                else if constexpr (std::is_integral_v<Field>)
                {
                    out_of_range |= !std::in_range<int64_t>(static_cast<standard_integer_t<Field>>(value));
                    values.push_back(static_cast<int64_t>(value));
                }
                else if constexpr (std::is_floating_point_v<Field>)
                {
                    values.push_back(static_cast<double>(value));
                }
                else if constexpr (std::is_same_v<Field, std::chrono::sys_days> || std::is_same_v<Field, std::chrono::year_month_day>)
                {
                    values.push_back(static_cast<int32_t>(std::chrono::sys_days(value).time_since_epoch().count()));
                }
                else if constexpr (std::is_same_v<Field, micros>)
                {
                    values.push_back(value.count());
                }
                else if constexpr (std::is_same_v<Field, std::chrono::hh_mm_ss<micros>>)
                {
                    values.push_back(value.to_duration().count());
                }
                else
                {
                    values.push_back(std::chrono::time_point_cast<micros>(value).time_since_epoch().count());
                }
            }

            bool overflowed() const { return is_string && data.size() > static_cast<size_t>(std::numeric_limits<int32_t>::max()); }

            fw_column column(const ColumnSpec& spec) const
            {
                return fw_column{
                    .type = static_cast<int32_t>(spec.type),
                    .scale = spec.scale,
                    .length = rows,
                    .values = is_string ? static_cast<const void*>(offsets.data()) : static_cast<const void*>(values.data()),
                    .validity = any_null ? validity.data() : nullptr,
                    .data = is_string && !data.empty() ? reinterpret_cast<const uint8_t*>(data.data()) : nullptr,
                    .data_len = is_string ? static_cast<int64_t>(data.size()) : 0,
                };
            }
        };

        template <class T, size_t... Is>
        auto make_builders(std::index_sequence<Is...>)
        {
            static constexpr auto bindings = bindings_of<T>();
            return std::tuple<ColumnBuilder<typename std::tuple_element_t<Is, std::remove_cv_t<decltype(bindings)>>::FieldType>...>{};
        }

        /// Builds the columns of rows, then hands write the table and its layout; both live only for the call.
        template <std::ranges::input_range R, class Write>
        Result<void> write_rows(R&& rows, Write&& write)
        {
            using T = std::remove_cvref_t<std::ranges::range_value_t<R>>;
            static constexpr auto bindings = bindings_of<T>();
            constexpr auto indices = std::make_index_sequence<field_count<T>>{};
            auto layout = layout_for<T>();
            if (!layout)
            {
                return std::unexpected(std::move(layout.error()));
            }
            auto builders = make_builders<T>(indices);
            for (const auto& row : rows)
            {
                std::apply([&](auto&... builder)
                           { std::apply([&](const auto&... binding) { (..., builder.push(row.*(binding.member))); }, bindings); },
                           builders);
            }
            const bool overflowed = std::apply([](const auto&... builder) { return (... || builder.overflowed()); }, builders);
            if (overflowed)
            {
                return std::unexpected(Error{.status = FW_INVALID_ARGUMENT, .message = "a string column holds more than 2 GiB, which int32 offsets cannot address"});
            }
            const bool out_of_range = std::apply([](const auto&... builder) { return (... || builder.out_of_range); }, builders);
            if (out_of_range)
            {
                return std::unexpected(Error{.status = FW_INVALID_ARGUMENT, .message = "an integer field holds a value above INT64_MAX, which an int64 column cannot represent"});
            }
            auto columns = std::apply(
                [&](const auto&... builder)
                {
                    return std::apply([&](const auto&... binding) { return std::array<fw_column, sizeof...(builder)>{builder.column(spec_of(binding))...}; },
                                      bindings);
                },
                builders);
            const fw_table table{.column_count = static_cast<int32_t>(columns.size()), .row_count = std::get<0>(builders).rows, .columns = columns.data()};
            return write(table, *layout);
        }
    }

    template <class R>
    concept mapped_range = std::ranges::input_range<R> && requires { FixedWidthMapper<std::remove_cvref_t<std::ranges::range_value_t<R>>>::get_bindings(); };

    /// Writes rows, a range of a type with a FixedWidthMapper, to a file. An empty std::optional writes as
    /// its column filled with trim_char.
    template <mapped_range R>
    Result<void> write_file(std::string_view path, R&& rows, const WriteOptions& options = {})
    {
        return detail::write_rows(std::forward<R>(rows), [&](const fw_table& table, const Layout& layout) { return write_file(path, table, layout, options); });
    }

    /// write_file into memory.
    template <mapped_range R>
    Result<std::vector<uint8_t>> write_memory(R&& rows, const WriteOptions& options = {})
    {
        std::vector<uint8_t> bytes;
        auto written = detail::write_rows(std::forward<R>(rows),
                                          [&](const fw_table& table, const Layout& layout) -> Result<void>
                                          {
                                              auto result = write_memory(table, layout, options);
                                              if (!result)
                                              {
                                                  return std::unexpected(std::move(result.error()));
                                              }
                                              bytes = std::move(*result);
                                              return {};
                                          });
        if (!written)
        {
            return std::unexpected(std::move(written.error()));
        }
        return bytes;
    }
}
