using System.Linq.Expressions;
using System.Runtime.InteropServices;
using FixedWidthParser.Attributes;
using FixedWidthParser.Processors;

namespace FixedWidthParser.Parsers
{
    /// <summary>One column of a built parser layout: the column's range and name plus its compiled parse-and-assign delegate.</summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct ColumnParserInfo<TModel>(int Start, int Length, string Name, ColumnParser<TModel> Parse)
        where TModel : allows ref struct;

    /// <summary>Static-build logic for <see cref="FixedWidthParser{TModel}"/>: the compiled model factory, the per-column setters and the required line length.</summary>
    internal static class ParserBuilder
    {
        public static Func<TModel> BuildModelFactory<TModel>() where TModel : new(), allows ref struct
        {
            var lambda = Expression.Lambda<Func<TModel>>(Expression.New(typeof(TModel)));
            return lambda.Compile();
        }

        public static ColumnParserInfo<TModel>[] BuildProcessors<TModel>() where TModel : allows ref struct
        {
            List<ColumnParserInfo<TModel>> processors = [];
            ModelColumns.ForEachColumn(typeof(TModel), (member, attribute) =>
            {
                var (memberType, model, access) = ModelColumns.MemberAccess(typeof(TModel), member);
                var value = Expression.Parameter(memberType, "value");
                var actionType = typeof(ColumnSetter<,>).MakeGenericType(typeof(TModel), memberType);
                var setter = Expression.Lambda(actionType, Expression.Assign(access, value), model, value).Compile();
                var parse = ColumnParserFactory.Create<TModel>(memberType, setter, attribute.Converter, member.Name, attribute.TrimChar, attribute.TrimMode, attribute.Format);
                processors.Add(new(attribute.Start, attribute.Length, member.Name, parse));
            });
            return [.. processors];
        }

        public static int ComputeRequiredLineLength<TModel>(ReadOnlySpan<ColumnParserInfo<TModel>> processors) where TModel : allows ref struct
        {
            int required = 0;
            foreach (ref readonly var processor in processors)
            {
                required = Math.Max(required, processor.Start + processor.Length);
            }
            return required;
        }
    }
}
