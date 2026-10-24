using System.Linq.Expressions;
using System.Reflection;
using FixedWidthParser.Attributes;
using FixedWidthParser.Processors;

namespace FixedWidthParser.Formatters
{
    /// <summary>Builds the attribute path's column formatters: a compiled getter per <c>[FixedColumn]</c> member, wrapped in the formatter its type needs.</summary>
    internal static class FormatterBuilder
    {
        public static IColumnFormatter<TModel>[] Build<TModel>()
            where TModel : allows ref struct
        {
            var formatters = new List<IColumnFormatter<TModel>>();
            ModelColumns.ForEachColumn(typeof(TModel), (member, attribute) => formatters.Add(CreateFormatter<TModel>(member, attribute)));
            return [.. formatters];
        }

        private static IColumnFormatter<TModel> CreateFormatter<TModel>(MemberInfo member, FixedColumnAttribute attribute)
            where TModel : allows ref struct
        {
            var (memberType, model, access) = ModelColumns.MemberAccess(typeof(TModel), member);
            FixedWidthRuntime.ToAsciiByte(attribute.Padding, member.Name, nameof(FixedColumnAttribute.Padding));
            byte fill = FixedWidthRuntime.ToAsciiByte(attribute.TrimChar, member.Name, nameof(FixedColumnAttribute.TrimChar));

            if (attribute.Overflow == OverflowBehavior.Truncate && memberType != typeof(string))
            {
                throw new InvalidOperationException(
                    $"OverflowBehavior.Truncate is not supported for non-string column '{member.Name}' of type '{memberType.Name}'.");
            }

            var getter = Expression.Lambda(typeof(ColumnGetter<,>).MakeGenericType(typeof(TModel), memberType), access, model).Compile();
            var options = new ColumnFormatOptions(attribute.Alignment, attribute.Padding, attribute.Format, ResolveOverflow(attribute.Overflow, memberType));

            var underlyingType = Nullable.GetUnderlyingType(memberType);
            if (underlyingType is null)
            {
                return BuildValueFormatter<TModel>(member, attribute, memberType, options, getter);
            }

            var inner = BuildValueFormatter<TModel>(member, attribute, underlyingType, options, BuildUnderlyingGetter<TModel>(underlyingType, getter));
            return (IColumnFormatter<TModel>)Activator.CreateInstance(
                typeof(NullableColumnFormatter<,>).MakeGenericType(typeof(TModel), underlyingType),
                attribute.Start, attribute.Length, fill, getter, inner)!;
        }

        // Default overflow resolves per type: string truncates, everything else throws.
        internal static OverflowBehavior ResolveOverflow(OverflowBehavior overflow, Type valueType)
        {
            if (overflow != OverflowBehavior.Default)
            {
                return overflow;
            }
            if (valueType == typeof(string))
            {
                return OverflowBehavior.Truncate;
            }
            return OverflowBehavior.Throw;
        }

        // Adapts a ColumnGetter<TModel, T?> into a ColumnGetter<TModel, T>; only called after HasValue is confirmed.
        private static Delegate BuildUnderlyingGetter<TModel>(Type underlyingType, Delegate nullableGetter)
            where TModel : allows ref struct
        {
            var modelParam = Expression.Parameter(typeof(TModel).MakeByRefType(), "model");
            var value = Expression.Property(Expression.Invoke(Expression.Constant(nullableGetter), modelParam), "Value");
            return Expression.Lambda(typeof(ColumnGetter<,>).MakeGenericType(typeof(TModel), underlyingType), value, modelParam).Compile();
        }

        private static IColumnFormatter<TModel> BuildValueFormatter<TModel>(
            MemberInfo member, FixedColumnAttribute attribute, Type valueType, ColumnFormatOptions options, Delegate getter)
            where TModel : allows ref struct
        {
            if (attribute.Converter is { } converterType)
            {
                var requiredInterface = typeof(IFixedWidthConverter<>).MakeGenericType(valueType);
                if (!requiredInterface.IsAssignableFrom(converterType))
                {
                    throw new InvalidOperationException(
                        $"Converter '{converterType}' for column '{member.Name}' must implement '{requiredInterface}'.");
                }
                var converter = Activator.CreateInstance(converterType)
                    ?? throw new InvalidOperationException($"Could not instantiate converter '{converterType}' for column '{member.Name}'.");
                return (IColumnFormatter<TModel>)Activator.CreateInstance(
                    typeof(ConverterColumnFormatter<,,>).MakeGenericType(typeof(TModel), valueType, converterType),
                    attribute.Start, attribute.Length, options, member.Name, getter, converter)!;
            }

            if (valueType == typeof(string))
            {
                return new StringColumnFormatter<TModel>(attribute.Start, attribute.Length, options, member.Name, (ColumnGetter<TModel, string>)getter);
            }

            if (!typeof(ISpanFormattable).IsAssignableFrom(valueType))
            {
                throw new InvalidOperationException(
                    $"Column '{member.Name}' has type '{valueType}', which is not string or ISpanFormattable; set a Converter for it.");
            }
            var formatterType = typeof(IUtf8SpanFormattable).IsAssignableFrom(valueType)
                ? typeof(Utf8SpanFormattableColumnFormatter<,>)
                : typeof(SpanFormattableColumnFormatter<,>);
            return (IColumnFormatter<TModel>)Activator.CreateInstance(
                formatterType.MakeGenericType(typeof(TModel), valueType),
                attribute.Start, attribute.Length, options, member.Name, getter)!;
        }
    }
}
