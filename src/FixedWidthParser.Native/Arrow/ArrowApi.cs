using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using FixedWidthParser.Native.Layouts;
using FixedWidthParser.Native.Reading;

namespace FixedWidthParser.Native.Arrow
{
    /// <summary>
    /// <c>fw_read_arrow_*</c>: a columnar read handed over through the Arrow C Data Interface as a struct
    /// array, one child per column. The column buffers move into the arrays without a copy; only DECIMAL
    /// widens its int64 values to Arrow's 128-bit decimals.
    /// </summary>
    internal static unsafe partial class ArrowApi
    {
        private const int DecimalBytes = 16;

        internal static int ReadFile(ReadOnlySpan<byte> path, NativeLayout layout, NativeReadOptionsRaw* options, ArrowArray* outArray, ArrowSchema* outSchema, NativeReadResultRaw* outResult)
        {
            NativeTableRaw table = default;
            int status = ReadApi.ReadFile(path, layout, options, &table, outResult);
            return Export(status, layout, &table, outArray, outSchema);
        }

        internal static int ReadMemory(byte* data, long length, NativeLayout layout, NativeReadOptionsRaw* options, ArrowArray* outArray, ArrowSchema* outSchema, NativeReadResultRaw* outResult)
        {
            NativeTableRaw table = default;
            int status = ReadApi.ReadMemory(data, length, layout, options, &table, outResult);
            return Export(status, layout, &table, outArray, outSchema);
        }

        private static int Export(int status, NativeLayout layout, NativeTableRaw* table, ArrowArray* outArray, ArrowSchema* outSchema)
        {
            *outArray = default;
            *outSchema = default;
            if (status != NativeStatus.Ok)
            {
                return status;
            }
            try
            {
                *outSchema = BuildSchema(layout);
                *outArray = BuildArray(table, layout);
                return NativeStatus.Ok;
            }
            catch (Exception ex)
            {
                NativeApi.SetLastError(ex.Message);
                ReleaseArrowSchema((IntPtr)outSchema);
                ReleaseArrowArray((IntPtr)outArray);
                *outArray = default;
                *outSchema = default;
                return NativeStatus.Error;
            }
            finally
            {
                NativeTable.Free(table);
            }
        }

        internal static void ReleaseArrowSchema(IntPtr schemaPtr)
        {
            var schema = (ArrowSchema*)schemaPtr;
            if (schema is null || schema->Release == IntPtr.Zero)
            {
                return;
            }
            ReleaseChildren(schema->Children, schema->NChildren, &ReleaseArrowSchema);
            ReleaseDictionary(schema->Dictionary, &ReleaseArrowSchema);
            FreeIfSet(schema->Format);
            FreeIfSet(schema->Name);
            schema->Release = IntPtr.Zero;
        }

        internal static void ReleaseArrowArray(IntPtr arrayPtr)
        {
            var array = (ArrowArray*)arrayPtr;
            if (array is null || array->Release == IntPtr.Zero)
            {
                return;
            }
            ReleaseChildren(array->Children, array->NChildren, &ReleaseArrowArray);
            ReleaseDictionary(array->Dictionary, &ReleaseArrowArray);
            var buffers = (IntPtr*)array->Buffers;
            for (long i = 0; i < array->NBuffers; i++)
            {
                FreeIfSet(buffers[i]);
            }
            FreeIfSet(array->Buffers);
            array->Release = IntPtr.Zero;
        }

        private static void ReleaseChildren(IntPtr children, long count, delegate*<IntPtr, void> release)
        {
            var pointers = (IntPtr*)children;
            for (long i = 0; i < count; i++)
            {
                release(pointers[i]);
                Marshal.FreeHGlobal(pointers[i]);
            }
            FreeIfSet(children);
        }

        private static void ReleaseDictionary(IntPtr dictionary, delegate*<IntPtr, void> release)
        {
            if (dictionary != IntPtr.Zero)
            {
                release(dictionary);
                Marshal.FreeHGlobal(dictionary);
            }
        }

        private static void FreeIfSet(IntPtr pointer)
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        internal static ArrowSchema BuildSchema(NativeLayout layout)
        {
            var columns = layout.Columns;
            var children = (IntPtr*)Marshal.AllocHGlobal(columns.Length * IntPtr.Size);
            var schema = new ArrowSchema
            {
                Format = AllocUtf8Z("+s"),
                NChildren = 0,
                Children = (IntPtr)children,
                Release = SchemaReleaseCallback,
            };
            try
            {
                foreach (var column in columns)
                {
                    var child = (ArrowSchema*)Marshal.AllocHGlobal(sizeof(ArrowSchema));
                    *child = new ArrowSchema
                    {
                        Format = AllocUtf8Z(FormatCode(column)),
                        Name = AllocUtf8Z(column.Name),
                        Flags = column.Nullable ? ArrowFlags.Nullable : 0,
                        Dictionary = column.Dictionary ? DictionaryValuesSchema() : IntPtr.Zero,
                        Release = SchemaReleaseCallback,
                    };
                    children[schema.NChildren++] = (IntPtr)child;
                }
                return schema;
            }
            catch
            {
                ReleaseArrowSchema((IntPtr)(&schema));
                throw;
            }
        }

        private static IntPtr DictionaryValuesSchema()
        {
            var values = (ArrowSchema*)Marshal.AllocHGlobal(sizeof(ArrowSchema));
            *values = new ArrowSchema { Format = AllocUtf8Z("u"), Release = SchemaReleaseCallback };
            return (IntPtr)values;
        }

        /// <summary>Moves every column of <paramref name="table"/> into a struct array, leaving the table's buffers empty.</summary>
        internal static ArrowArray BuildArray(NativeTableRaw* table, NativeLayout layout)
        {
            var children = (IntPtr*)Marshal.AllocHGlobal(Math.Max(1, table->ColumnCount) * IntPtr.Size);
            var buffers = (IntPtr*)Marshal.AllocHGlobal(IntPtr.Size);
            buffers[0] = IntPtr.Zero;
            var array = new ArrowArray
            {
                Length = table->RowCount,
                NBuffers = 1,
                Buffers = (IntPtr)buffers,
                Children = (IntPtr)children,
                Release = ArrayReleaseCallback,
            };
            try
            {
                for (int i = 0; i < table->ColumnCount; i++)
                {
                    var child = (ArrowArray*)Marshal.AllocHGlobal(sizeof(ArrowArray));
                    *child = default;
                    children[array.NChildren++] = (IntPtr)child;
                }
                // Encoding a dictionary column hashes every row, so the columns are built side by side.
                nint columns = (nint)table->Columns;
                nint built = (nint)children;
                Parallel.For(0, table->ColumnCount, i =>
                    *((ArrowArray**)built)[i] = BuildChild((NativeColumnRaw*)columns + i, layout.Columns[i].Dictionary));
                return array;
            }
            catch (AggregateException ex)
            {
                ReleaseArrowArray((IntPtr)(&array));
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex.InnerExceptions[0]);
                throw;
            }
            catch
            {
                ReleaseArrowArray((IntPtr)(&array));
                throw;
            }
        }

        private static ArrowArray BuildChild(NativeColumnRaw* column, bool dictionary)
        {
            if (dictionary)
            {
                return BuildDictionaryChild(column);
            }
            bool isString = column->Type == NativeType.String;
            int bufferCount = isString ? 3 : 2;
            var buffers = (IntPtr*)Marshal.AllocHGlobal(bufferCount * IntPtr.Size);
            buffers[0] = Take(ref column->Validity);
            buffers[1] = column->Type == NativeType.Decimal ? WidenDecimals(column) : OrAllocate((IntPtr)column->Values, 8);
            column->Values = null;
            if (isString)
            {
                buffers[2] = OrAllocate((IntPtr)column->Data, 1);
                column->Data = null;
            }
            return new ArrowArray
            {
                Length = column->Length,
                NullCount = CountUnset(buffers[0], column->Length),
                NBuffers = bufferCount,
                Buffers = (IntPtr)buffers,
                Release = ArrayReleaseCallback,
            };
        }

        private static ArrowArray BuildDictionaryChild(NativeColumnRaw* column)
        {
            long rows = column->Length;
            int* indices = DictionaryEncoder.Encode((int*)column->Values, column->Data, rows,
                out int* valueOffsets, out byte* valueBytes, out long valueCount, out _);
            FreeIfSet((IntPtr)column->Values);
            FreeIfSet((IntPtr)column->Data);
            column->Values = null;
            column->Data = null;

            var valueBuffers = (IntPtr*)Marshal.AllocHGlobal(3 * IntPtr.Size);
            valueBuffers[0] = IntPtr.Zero;
            valueBuffers[1] = (IntPtr)valueOffsets;
            valueBuffers[2] = (IntPtr)valueBytes;
            var values = (ArrowArray*)Marshal.AllocHGlobal(sizeof(ArrowArray));
            *values = new ArrowArray { Length = valueCount, NBuffers = 3, Buffers = (IntPtr)valueBuffers, Release = ArrayReleaseCallback };

            var buffers = (IntPtr*)Marshal.AllocHGlobal(2 * IntPtr.Size);
            buffers[0] = Take(ref column->Validity);
            buffers[1] = (IntPtr)indices;
            return new ArrowArray
            {
                Length = rows,
                NullCount = CountUnset(buffers[0], rows),
                NBuffers = 2,
                Buffers = (IntPtr)buffers,
                Dictionary = (IntPtr)values,
                Release = ArrayReleaseCallback,
            };
        }

        private static IntPtr Take(ref byte* pointer)
        {
            IntPtr taken = (IntPtr)pointer;
            pointer = null;
            return taken;
        }

        // A buffer of size 0 may be NULL under the C Data Interface, but some importers want a pointer.
        private static IntPtr OrAllocate(IntPtr buffer, int minimum)
        {
            return buffer == IntPtr.Zero ? Marshal.AllocHGlobal(minimum) : buffer;
        }

        private static IntPtr WidenDecimals(NativeColumnRaw* column)
        {
            var values = new ReadOnlySpan<long>(column->Values, checked((int)column->Length));
            var wide = (long*)Marshal.AllocHGlobal((nint)DecimalBufferBytes(column->Length));
            for (int i = 0; i < values.Length; i++)
            {
                wide[2 * i] = values[i];
                wide[(2 * i) + 1] = values[i] < 0 ? -1 : 0;
            }
            Marshal.FreeHGlobal((IntPtr)column->Values);
            column->Values = null;
            return (IntPtr)wide;
        }

        /// <summary>The bytes of a decimal128 buffer for <paramref name="rows"/> values, in 64-bit arithmetic.</summary>
        internal static long DecimalBufferBytes(long rows)
        {
            return checked(Math.Max(1, rows) * DecimalBytes);
        }

        private static long CountUnset(IntPtr validity, long length)
        {
            if (validity == IntPtr.Zero)
            {
                return 0;
            }
            ReadOnlySpan<byte> bits = new((void*)validity, (int)((length + 7) / 8));
            long set = 0;
            for (long row = 0; row < length; row += 64)
            {
                int bytes = (int)Math.Min(8, bits.Length - (row / 8));
                ulong packed = 0;
                bits.Slice((int)(row / 8), bytes).CopyTo(MemoryMarshal.AsBytes(new Span<ulong>(ref packed)));
                packed &= length - row >= 64 ? ulong.MaxValue : (1UL << (int)(length - row)) - 1;
                set += BitOperations.PopCount(packed);
            }
            return length - set;
        }

        private static string FormatCode(NativeColumn column)
        {
            return column.Type switch
            {
                // A dictionary column's own format is its index type; the values' utf8 is in its dictionary schema.
                NativeType.String => column.Dictionary ? "i" : "u",
                NativeType.Int64 => "l",
                NativeType.Float64 => "g",
                NativeType.Decimal => string.Create(CultureInfo.InvariantCulture, $"d:18,{column.Scale}"),
                NativeType.Date => "tdD",
                NativeType.Time => "ttu",
                _ => "tsu:",
            };
        }

        private static IntPtr AllocUtf8Z(string value)
        {
            int length = Encoding.UTF8.GetByteCount(value);
            var block = (byte*)Marshal.AllocHGlobal(length + 1);
            Encoding.UTF8.GetBytes(value, new Span<byte>(block, length));
            block[length] = 0;
            return (IntPtr)block;
        }

        private static IntPtr SchemaReleaseCallback => (IntPtr)(delegate* unmanaged<ArrowSchema*, void>)&Exports.ReleaseArrowSchemaCallback;

        private static IntPtr ArrayReleaseCallback => (IntPtr)(delegate* unmanaged<ArrowArray*, void>)&Exports.ReleaseArrowArrayCallback;
    }
}
