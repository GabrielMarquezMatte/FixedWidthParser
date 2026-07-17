using System.Reflection;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Shared build-time orchestration for <see cref="ColumnParserFactory"/> (char) and
    /// <see cref="Utf8ColumnParserFactory"/> (byte): the identical "converter → string special-case →
    /// double/float (csFastFloat) special-case → <see cref="ISpanParsable{TSelf}"/>/<see cref="IUtf8SpanParsable{TSelf}"/>
    /// fallback → wrap setter+parser" flow. The element-specific pieces (string/double/float/converter
    /// builders and the reflected build-parsable method) are passed in, so the spans-of-different-element-type
    /// closures stay in each factory while the reflection plumbing lives in one place.
    /// </summary>
    internal static class ColumnParserFactoryShared
    {
        public static TColumnParser Create<TModel, TColumnParser>(
            Type valueType,
            Delegate setter,
            Type? converterType,
            string memberName,
            object trimChar,
            Attributes.TrimMode trimMode,
            string? format,
            Type converterInterfaceDefinition,
            MethodInfo buildParsableMethod,
            MethodInfo buildConverterMethod,
            MethodInfo buildDoubleMethod,
            MethodInfo buildFloatMethod,
            Func<RefAction<TModel, string>, object, Attributes.TrimMode, TColumnParser> buildString,
            Func<Type, Delegate, string, object, Attributes.TrimMode, TColumnParser> buildExact)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
            where TColumnParser : Delegate
        {
            if (converterType is not null)
            {
                var requiredInterface = converterInterfaceDefinition.MakeGenericType(valueType);
                if (!requiredInterface.IsAssignableFrom(converterType))
                {
                    throw new InvalidOperationException(
                        $"Converter '{converterType}' for column '{memberName}' must implement '{requiredInterface}'.");
                }
                var converterInstance = Activator.CreateInstance(converterType)
                    ?? throw new InvalidOperationException($"Could not instantiate converter '{converterType}' for column '{memberName}'.");
                return (TColumnParser)buildConverterMethod.MakeGenericMethod(typeof(TModel), valueType, converterType)
                                                          .Invoke(null, [setter, converterInstance, trimChar, trimMode])!;
            }

            if (format is not null && (valueType == typeof(DateTime) || valueType == typeof(DateOnly) || valueType == typeof(TimeOnly) || valueType == typeof(DateTimeOffset)))
            {
                return buildExact(valueType, setter, format, trimChar, trimMode);
            }

            if (valueType == typeof(string))
            {
                return buildString((RefAction<TModel, string>)setter, trimChar, trimMode);
            }

            // double/float take the csFastFloat fast path ahead of the ISpanParsable fallback (both
            // implement ISpanParsable/IUtf8SpanParsable too, but csFastFloat is faster).
            if (valueType == typeof(double))
            {
                return (TColumnParser)buildDoubleMethod.MakeGenericMethod(typeof(TModel)).Invoke(null, [setter, trimChar, trimMode])!;
            }
            if (valueType == typeof(float))
            {
                return (TColumnParser)buildFloatMethod.MakeGenericMethod(typeof(TModel)).Invoke(null, [setter, trimChar, trimMode])!;
            }

            return (TColumnParser)buildParsableMethod.MakeGenericMethod(typeof(TModel), valueType)
                                                     .Invoke(null, [setter, trimChar, trimMode])!;
        }
    }
}
