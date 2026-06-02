using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using FixedWidthParser.Attributes;
using FixedWidthParser.Processors;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Parsers
{
    public sealed class FixedWidthParser<TModel> where TModel : new(), allows ref struct
    {
        private static readonly Func<TModel> _modelFactory = BuildModelFactory();
        private static readonly IColumnProcessor<TModel>[] _processors = BuildProcessors();
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
        private static IColumnProcessor<TModel>[] BuildProcessors()
        {
            var properties = typeof(TModel).GetProperties();
            var fields = typeof(TModel).GetFields();
            List<IColumnProcessor<TModel>> processors = new(properties.Length + fields.Length);
            foreach (var prop in properties)
            {
                if (CreateColumnProcessor(prop, out var processor))
                {
                    processors.Add(processor);
                }
            }
            foreach (var field in fields)
            {
                if (CreateColumnProcessor(field, out var processor))
                {
                    processors.Add(processor);
                }
            }
            return processors.ToArray();
        }

        private static bool CreateColumnProcessor(MemberInfo member, [NotNullWhen(true)] out IColumnProcessor<TModel>? processor)
        {
            var attribute = member.GetCustomAttribute<FixedColumnAttribute>();
            if (attribute is null)
            {
                processor = null;
                return false;
            }
            var targetExpr = Expression.Parameter(typeof(TModel).MakeByRefType(), "model");
            var (memberType, memberExpr) = member switch
            {
                PropertyInfo p => (p.PropertyType, Expression.Property(targetExpr, p)),
                FieldInfo f => (f.FieldType, Expression.Field(targetExpr, f)),
                _ => throw new ArgumentException($"Membro não suportado: {member.GetType().Name}", nameof(member))
            };
            var valueExpr = Expression.Parameter(memberType, "value");
            var assignExpr = Expression.Assign(memberExpr, valueExpr);
            var actionType = typeof(RefAction<,>).MakeGenericType(typeof(TModel), memberType);
            var setter = Expression.Lambda(actionType, assignExpr, targetExpr, valueExpr).Compile();
            var processorType = memberType switch
            {
                Type t when t == typeof(string) => typeof(StringColumnProcessor<>).MakeGenericType(typeof(TModel)),
                Type t when t == typeof(double) => typeof(DoubleColumnProcessor<>).MakeGenericType(typeof(TModel)),
                Type t when t == typeof(float) => typeof(FloatColumnProcessor<>).MakeGenericType(typeof(TModel)),
                _ => typeof(ColumnProcessor<,>).MakeGenericType(typeof(TModel), memberType)
            };
            processor = (IColumnProcessor<TModel>)Activator.CreateInstance(processorType, attribute.Start, attribute.Length, setter)!;
            return true;
        }

        public bool TryParse(ReadOnlySpan<char> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
        {
            model = _modelFactory();
            foreach (ref readonly var processor in _processors.AsSpan())
            {
                if (!processor.TryProcess(ref model, formatProvider, line, stringPool))
                {
                    return false;
                }
            }
            return true;
        }
    }
}