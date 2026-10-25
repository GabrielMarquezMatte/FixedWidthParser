using System.Runtime.InteropServices;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Native.Arrow
{
    // The stream's GCHandle owns its state, and the state owns the reader, until the release callback runs.
#pragma warning disable CA2000, IDISP001, IDISP004, IDISP007
    internal static unsafe partial class ArrowApi
    {
        private const int ArrowErrno = 5;

        /// <summary>One <c>ArrowArrayStream</c>: a batched reader and the message its last failure left.</summary>
        internal sealed class ArrowStreamState(NativeTableReader reader) : IDisposable
        {
            internal NativeTableReader Reader { get; } = reader;

            internal IntPtr LastErrorUtf8 { get; private set; }

            internal void SetError(string message)
            {
                FreeIfSet(LastErrorUtf8);
                LastErrorUtf8 = AllocUtf8Z(message);
            }

            public void Dispose()
            {
                Reader.Dispose();
                FreeIfSet(LastErrorUtf8);
                LastErrorUtf8 = IntPtr.Zero;
            }
        }

        internal static int OpenStreamFile(ReadOnlySpan<byte> path, NativeLayout layout, NativeReaderOptionsRaw* options, ArrowArrayStream* outStream)
        {
            *outStream = default;
            int status = ReaderApi.CreateFile(path, layout, options, out var reader);
            return status == NativeStatus.Ok ? Publish(reader!, outStream) : status;
        }

        /// <summary>Reads <paramref name="data"/> lazily: the caller keeps it alive until the stream is released.</summary>
        internal static int OpenStreamMemory(byte* data, long length, NativeLayout layout, NativeReaderOptionsRaw* options, ArrowArrayStream* outStream)
        {
            *outStream = default;
            int status = ReaderApi.CreateMemory(data, length, layout, options, out var reader);
            return status == NativeStatus.Ok ? Publish(reader!, outStream) : status;
        }

        /// <summary>Reads <paramref name="source"/> lazily and owns it: it is disposed when this fails or the stream is released.</summary>
        internal static int OpenStreamStream(Stream source, NativeLayout layout, NativeReaderOptionsRaw* options, ArrowArrayStream* outStream)
        {
            *outStream = default;
            int status = ReaderApi.CreateStream(source, layout, options, out var reader);
            return status == NativeStatus.Ok ? Publish(reader!, outStream) : status;
        }

        private static int Publish(NativeTableReader reader, ArrowArrayStream* outStream)
        {
            *outStream = new ArrowArrayStream
            {
                GetSchema = (IntPtr)(delegate* unmanaged<ArrowArrayStream*, ArrowSchema*, int>)&Exports.ArrowStreamGetSchema,
                GetNext = (IntPtr)(delegate* unmanaged<ArrowArrayStream*, ArrowArray*, int>)&Exports.ArrowStreamGetNext,
                GetLastError = (IntPtr)(delegate* unmanaged<ArrowArrayStream*, IntPtr>)&Exports.ArrowStreamGetLastError,
                Release = (IntPtr)(delegate* unmanaged<ArrowArrayStream*, void>)&Exports.ArrowStreamRelease,
                PrivateData = GCHandle.ToIntPtr(GCHandle.Alloc(new ArrowStreamState(reader))),
            };
            return NativeStatus.Ok;
        }

        private static ArrowStreamState? StateOf(ArrowArrayStream* stream)
        {
            if (stream is null || stream->PrivateData == IntPtr.Zero)
            {
                return null;
            }
            return GCHandle.FromIntPtr(stream->PrivateData).Target as ArrowStreamState;
        }

        internal static int StreamGetSchema(ArrowArrayStream* stream, ArrowSchema* outSchema)
        {
            var state = StateOf(stream);
            if (state is null || outSchema is null)
            {
                return ArrowErrno;
            }
            try
            {
                *outSchema = BuildSchema(state.Reader.Layout);
                return 0;
            }
            catch (Exception ex)
            {
                state.SetError(ex.Message);
                *outSchema = default;
                return ArrowErrno;
            }
        }

        internal static int StreamGetNext(ArrowArrayStream* stream, ArrowArray* outArray)
        {
            var state = StateOf(stream);
            if (state is null || outArray is null)
            {
                return ArrowErrno;
            }
            *outArray = default;
            NativeTableRaw table = default;
            int status = ReaderApi.NextBatch(state.Reader, &table);
            if (status == NativeStatus.Eof)
            {
                return 0;
            }
            if (status != NativeStatus.Ok)
            {
                state.SetError(NativeApi.LastErrorText());
                return ArrowErrno;
            }
            try
            {
                *outArray = BuildArray(&table);
                return 0;
            }
            catch (Exception ex)
            {
                state.SetError(ex.Message);
                *outArray = default;
                return ArrowErrno;
            }
            finally
            {
                NativeTable.Free(&table);
            }
        }

        internal static IntPtr StreamGetLastError(ArrowArrayStream* stream)
        {
            return StateOf(stream)?.LastErrorUtf8 ?? IntPtr.Zero;
        }

        internal static void StreamRelease(ArrowArrayStream* stream)
        {
            if (stream is null || stream->Release == IntPtr.Zero)
            {
                return;
            }
            IntPtr privateData = stream->PrivateData;
            stream->PrivateData = IntPtr.Zero;
            stream->Release = IntPtr.Zero;
            if (privateData == IntPtr.Zero)
            {
                return;
            }
            var handle = GCHandle.FromIntPtr(privateData);
            try
            {
                (handle.Target as ArrowStreamState)?.Dispose();
            }
            finally
            {
                handle.Free();
            }
        }
    }
#pragma warning restore CA2000, IDISP001, IDISP004, IDISP007
}
