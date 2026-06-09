using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using FixedWidthParser.Attributes;
using FixedWidthParser.Processors;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Parsers
{
    public sealed class FixedWidthParser<TModel> where TModel : new(), allows ref struct
    {
        private static readonly Func<TModel> _modelFactory;
        private static readonly (int Start, int Length, ColumnParser<TModel> Parse)[] _processors;
        private static readonly int _requiredLineLength;
        private static readonly ExceptionDispatchInfo? _buildError;

        // The build is static (once per type). Any layout/configuration error is captured and
        // rethrown from the instance constructor, so the caller gets a clean exception instead of
        // a TypeInitializationException on first use.
        static FixedWidthParser()
        {
            try
            {
                _modelFactory = BuildModelFactory();
                _processors = BuildProcessors();
                _requiredLineLength = ComputeRequiredLineLength(_processors);
                _buildError = null;
            }
            catch (Exception ex)
            {
                _modelFactory = null!;
                _processors = [];
                _requiredLineLength = 0;
                _buildError = ExceptionDispatchInfo.Capture(ex);
            }
        }

        public FixedWidthParser() => _buildError?.Throw();

        private static Func<TModel> BuildModelFactory()
        {
            var ctor = typeof(TModel).GetConstructor(Type.EmptyTypes);
            if (ctor is null)
            {
                throw new InvalidOperationException($"Type {typeof(TModel).FullName} must have a parameterless constructor.");
            }
            var lambda = Expression.Lambda<Func<TModel>>(Expression.New(ctor));
            return lambda.Compile();
        }
        private static (int Start, int Length, ColumnParser<TModel> Parse)[] BuildProcessors()
        {
            var processors = new List<(int Start, int Length, ColumnParser<TModel> Parse)>();
            ModelColumns.ForEachColumn(typeof(TModel), (member, attribute) =>
                processors.Add((attribute.Start, attribute.Length, CreateColumnParser(member))));
            return processors.ToArray();
        }

        private static int ComputeRequiredLineLength(ReadOnlySpan<(int Start, int Length, ColumnParser<TModel> Parse)> processors)
        {
            int required = 0;
            foreach (var (Start, Length, _) in processors)
            {
                int end = Start + Length;
                if (end > required)
                {
                    required = end;
                }
            }
            return required;
        }

        private static ColumnParser<TModel> CreateColumnParser(MemberInfo member)
        {
            var (memberType, model, access) = ModelColumns.MemberAccess(typeof(TModel), member);
            var value = Expression.Parameter(memberType, "value");
            var actionType = typeof(RefAction<,>).MakeGenericType(typeof(TModel), memberType);
            var setter = Expression.Lambda(actionType, Expression.Assign(access, value), model, value).Compile();
            return ColumnParserFactory.Create<TModel>(memberType, setter);
        }

        public bool TryParse(ReadOnlySpan<char> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
        {
            if (line.Length < _requiredLineLength)
            {
                model = default!;
                return false;
            }

            model = _modelFactory();
            foreach (ref readonly var processor in _processors.AsSpan())
            {
                var column = line.Slice(processor.Start, processor.Length);
                if (!processor.Parse(column, formatProvider, stringPool, ref model))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
