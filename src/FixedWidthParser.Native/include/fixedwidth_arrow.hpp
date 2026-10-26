// Arrow C Data Interface helpers for fixedwidth.hpp: RAII owners of the arrays and streams the library
// publishes. Hand get() to any Arrow consumer, which then takes ownership (set its release to null).
#pragma once

#include "fixedwidth.hpp"
#include "fixedwidth_arrow.h"

#include <utility>

namespace fw
{
    /// A struct array and its schema, one child per layout column; released on destruction.
    class ArrowResult
    {
    public:
        ArrowResult() noexcept : array_{}, schema_{} {}
        ArrowResult(ArrowResult&& other) noexcept : array_(std::exchange(other.array_, {})), schema_(std::exchange(other.schema_, {})) {}
        ArrowResult& operator=(ArrowResult&& other) noexcept
        {
            std::swap(array_, other.array_);
            std::swap(schema_, other.schema_);
            return *this;
        }
        ArrowResult(const ArrowResult&) = delete;
        ArrowResult& operator=(const ArrowResult&) = delete;
        ~ArrowResult()
        {
            if (array_.release != nullptr)
            {
                array_.release(&array_);
            }
            if (schema_.release != nullptr)
            {
                schema_.release(&schema_);
            }
        }

        ArrowArray& array() noexcept { return array_; }
        ArrowSchema& schema() noexcept { return schema_; }

    private:
        ArrowArray array_;
        ArrowSchema schema_;
    };

    inline Result<ArrowResult> read_arrow_file(std::string_view path, const Layout& layout, const ReadOptions& options = {})
    {
        ArrowResult result;
        const fw_read_options raw = options.to_c();
        if (auto ok = detail::check(fw_read_arrow_file(detail::bytes(path), static_cast<int32_t>(path.size()), layout.get(), &raw,
                                                       &result.array(), &result.schema(), nullptr));
            !ok)
        {
            return std::unexpected(ok.error());
        }
        return result;
    }

    inline Result<ArrowResult> read_arrow_memory(std::string_view data, const Layout& layout, const ReadOptions& options = {})
    {
        ArrowResult result;
        const fw_read_options raw = options.to_c();
        if (auto ok = detail::check(fw_read_arrow_memory(detail::bytes(data), static_cast<int64_t>(data.size()), layout.get(), &raw,
                                                         &result.array(), &result.schema(), nullptr));
            !ok)
        {
            return std::unexpected(ok.error());
        }
        return result;
    }

    /// An ArrowArrayStream over a sequential read; released on destruction.
    class ArrowStream
    {
    public:
        ArrowStream() noexcept : stream_{} {}
        ArrowStream(ArrowStream&& other) noexcept : stream_(std::exchange(other.stream_, {})) {}
        ArrowStream& operator=(ArrowStream&& other) noexcept
        {
            std::swap(stream_, other.stream_);
            return *this;
        }
        ArrowStream(const ArrowStream&) = delete;
        ArrowStream& operator=(const ArrowStream&) = delete;
        ~ArrowStream()
        {
            if (stream_.release != nullptr)
            {
                stream_.release(&stream_);
            }
        }

        ArrowArrayStream* get() noexcept { return &stream_; }

    private:
        ArrowArrayStream stream_;
    };

    inline Result<ArrowStream> read_arrow_stream_file(std::string_view path, const Layout& layout, const ReaderOptions& options = {})
    {
        ArrowStream stream;
        const fw_reader_options raw = options.to_c();
        if (auto ok = detail::check(fw_read_arrow_stream_file(detail::bytes(path), static_cast<int32_t>(path.size()), layout.get(), &raw, stream.get()));
            !ok)
        {
            return std::unexpected(ok.error());
        }
        return stream;
    }

    /// Reads data lazily: it must outlive the stream.
    inline Result<ArrowStream> read_arrow_stream_memory(std::string_view data, const Layout& layout, const ReaderOptions& options = {})
    {
        ArrowStream stream;
        const fw_reader_options raw = options.to_c();
        if (auto ok = detail::check(fw_read_arrow_stream_memory(detail::bytes(data), static_cast<int64_t>(data.size()), layout.get(), &raw, stream.get()));
            !ok)
        {
            return std::unexpected(ok.error());
        }
        return stream;
    }

    /// Reads `in` lazily, without seeking: it must outlive the stream.
    inline Result<ArrowStream> read_arrow_stream(std::istream& in, const Layout& layout, const ReaderOptions& options = {})
    {
        ArrowStream stream;
        const fw_reader_options raw = options.to_c();
        const fw_stream source = detail::istream_source(in);
        if (auto ok = detail::check(fw_read_arrow_stream_stream(&source, layout.get(), &raw, stream.get())); !ok)
        {
            return std::unexpected(ok.error());
        }
        return stream;
    }
}
