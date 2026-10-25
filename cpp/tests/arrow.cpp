#include "common.hpp"

#include <fixedwidth_arrow.hpp>

#include <cstring>
#include <sstream>
#include <string>

int main()
{
    auto layout = cotahist_layout();

    auto result = fw::read_arrow_memory(content, layout, skip_header_and_invalid(4));
    CHECK(result.has_value());
    const ArrowSchema& schema = result->schema();
    const ArrowArray& array = result->array();
    CHECK(std::strcmp(schema.format, "+s") == 0 && schema.n_children == 4);
    CHECK(std::strcmp(schema.children[2]->name, "preabe") == 0);
    CHECK(array.length == 3 && array.children[3]->null_count == 1);
    CHECK(static_cast<const double*>(array.children[2]->buffers[1])[1] == 678.9);

    auto stream = fw::read_arrow_stream_memory(content, layout, {.read = skip_header_and_invalid(), .batch_rows = 2});
    CHECK(stream.has_value());
    ArrowSchema stream_schema;
    CHECK(stream->get()->get_schema(stream->get(), &stream_schema) == 0);
    stream_schema.release(&stream_schema);
    int64_t rows = 0;
    ArrowArray batch;
    while (stream->get()->get_next(stream->get(), &batch) == 0 && batch.release != nullptr) {
        rows += batch.length;
        batch.release(&batch);
    }
    CHECK(rows == 3);

    std::istringstream in{std::string(content)};
    auto from_stream = fw::read_arrow_stream(in, layout, {.read = skip_header_and_invalid(), .batch_rows = 2});
    CHECK(from_stream.has_value());
    int64_t streamed_rows = 0;
    while (from_stream->get()->get_next(from_stream->get(), &batch) == 0 && batch.release != nullptr) {
        streamed_rows += batch.length;
        batch.release(&batch);
    }
    CHECK(streamed_rows == 3);

    std::puts("arrow ok");
    return 0;
}
