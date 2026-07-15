using System.Linq.Expressions;
using System.Runtime.InteropServices;
using FixedWidthParser.Attributes;

namespace FixedWidthParser.Parsers
{
    /// <summary>
    /// One column of a built parser layout: the column's range plus the compiled parse-and-assign
    /// delegate (<typeparamref name="TParser"/> is <see cref="Processors.ColumnParser{TModel}"/> for
    /// the char parser or <see cref="Processors.Utf8ColumnParser{TModel}"/> for the byte parser).
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct ColumnParserInfo<TParser>(int Start, int Length, TParser Parse);

    /// <summary>
    /// Shared static-build logic for <see cref="FixedWidthParser{TModel}"/> and
    /// <see cref="Utf8FixedWidthParser{TModel}"/>: the compiled model factory, the per-column setter
    /// compilation, and the required line length. The two parsers differ only in the factory that
    /// turns a (member type, compiled setter) pair into a column parser delegate, so that factory is
    /// passed into <see cref="BuildProcessors{TModel, TParser}"/> as a delegate.
    /// </summary>
    internal static class ParserBuilder
    {
#if NET9_0_OR_GREATER
        public static Func<TModel> BuildModelFactory<TModel>() where TModel : new(), allows ref struct
#else
        public static Func<TModel> BuildModelFactory<TModel>() where TModel : new()
#endif
        {
            var lambda = Expression.Lambda<Func<TModel>>(Expression.New(typeof(TModel)));
            return lambda.Compile();
        }

        public static ColumnParserInfo<TParser>[] BuildProcessors<TModel, TParser>(Func<Type, Delegate, TParser> createParser)
#if NET9_0_OR_GREATER
            where TModel : allows ref struct
#endif
        {
            List<ColumnParserInfo<TParser>> processors = [];
            ModelColumns.ForEachColumn(typeof(TModel), (member, attribute) =>
            {
                var (memberType, model, access) = ModelColumns.MemberAccess(typeof(TModel), member);
                var value = Expression.Parameter(memberType, "value");
                var actionType = typeof(RefAction<,>).MakeGenericType(typeof(TModel), memberType);
                var setter = Expression.Lambda(actionType, Expression.Assign(access, value), model, value).Compile();
                processors.Add(new(attribute.Start, attribute.Length, createParser(memberType, setter)));
            });
            return [.. processors];
        }

        public static int ComputeRequiredLineLength<TParser>(ReadOnlySpan<ColumnParserInfo<TParser>> processors)
        {
            int required = 0;
            foreach (ref readonly var processor in processors)
            {
                int end = processor.Start + processor.Length;
                if (end > required)
                {
                    required = end;
                }
            }
            return required;
        }
    }
}
