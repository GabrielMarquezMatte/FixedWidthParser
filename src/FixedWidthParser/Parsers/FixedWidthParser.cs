using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using FixedWidthParser.Attributes;
using FixedWidthParser.Processors;
using CommunityToolkit.HighPerformance.Buffers;
using System.Runtime.InteropServices;

namespace FixedWidthParser.Parsers
{
    public sealed class FixedWidthParser<TModel> where TModel : new(), allows ref struct
    {
        private static readonly Func<TModel> _modelFactory;
        private static readonly ColumnParser<TModel>[] _processors;
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
            }
            catch (Exception ex)
            {
                _modelFactory = null!;
                _processors = [];
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
        private static ColumnParser<TModel>[] BuildProcessors()
        {
            var properties = typeof(TModel).GetProperties();
            var fields = typeof(TModel).GetFields();
            List<ColumnParser<TModel>> processors = new(properties.Length + fields.Length);
            List<(int Start, int Length, string Name)> columns = new(properties.Length + fields.Length);

            foreach (var prop in properties) Add(prop);
            foreach (var field in fields) Add(field);

            ColumnLayoutValidator.Validate(CollectionsMarshal.AsSpan(columns), typeof(TModel));
            return processors.ToArray();

            void Add(MemberInfo member)
            {
                var attribute = member.GetCustomAttribute<FixedColumnAttribute>();
                if (attribute is null) return;
                columns.Add((attribute.Start, attribute.Length, member.Name));
                processors.Add(CreateColumnParser(member, attribute));
            }
        }

        private static ColumnParser<TModel> CreateColumnParser(MemberInfo member, FixedColumnAttribute attribute)
        {
            var targetExpr = Expression.Parameter(typeof(TModel).MakeByRefType(), "model");
            var (memberType, memberExpr) = member switch
            {
                PropertyInfo p => (p.PropertyType, Expression.Property(targetExpr, p)),
                FieldInfo f => (f.FieldType, Expression.Field(targetExpr, f)),
                _ => throw new ArgumentException($"Unsupported member: {member.GetType().Name}", nameof(member))
            };
            var valueExpr = Expression.Parameter(memberType, "value");
            var assignExpr = Expression.Assign(memberExpr, valueExpr);
            var actionType = typeof(RefAction<,>).MakeGenericType(typeof(TModel), memberType);
            var setter = Expression.Lambda(actionType, assignExpr, targetExpr, valueExpr).Compile();
            return ColumnParserFactory.Create<TModel>(attribute.Start, attribute.Length, memberType, setter);
        }

        public bool TryParse(ReadOnlySpan<char> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
        {
            model = _modelFactory();
            foreach (ref readonly var processor in _processors.AsSpan())
            {
                if (!processor(line, formatProvider, stringPool, ref model))
                {
                    return false;
                }
            }
            return true;
        }
    }
}