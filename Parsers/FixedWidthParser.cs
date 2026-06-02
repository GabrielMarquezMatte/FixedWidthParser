using System.Linq.Expressions;
using System.Reflection;
using Benchmarks.Attributes;
using Benchmarks.Processors;
using CommunityToolkit.HighPerformance.Buffers;

namespace Benchmarks.Parsers
{
    public sealed class FixedWidthParser<TModel> where TModel : new()
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
            List<IColumnProcessor<TModel>> processors = new(properties.Length);
            foreach (var prop in properties)
            {
                var attribute = prop.GetCustomAttribute<FixedColumnAttribute>();
                if (attribute is null) continue;

                var targetExpr = Expression.Parameter(typeof(TModel).MakeByRefType(), "model");
                var valueExpr = Expression.Parameter(prop.PropertyType, "value");
                var propExpr = Expression.Property(targetExpr, prop);
                var assignExpr = Expression.Assign(propExpr, valueExpr);
                var actionType = typeof(RefAction<,>).MakeGenericType(typeof(TModel), prop.PropertyType);
                var setter = Expression.Lambda(actionType, assignExpr, targetExpr, valueExpr).Compile();
                var processorType = prop.PropertyType switch
                {
                    Type t when t == typeof(string) => typeof(StringColumnProcessor<>).MakeGenericType(typeof(TModel)),
                    Type t when t == typeof(double) => typeof(DoubleColumnProcessor<>).MakeGenericType(typeof(TModel)),
                    Type t when t == typeof(float) => typeof(FloatColumnProcessor<>).MakeGenericType(typeof(TModel)),
                    _ => typeof(ColumnProcessor<,>).MakeGenericType(typeof(TModel), prop.PropertyType)
                };
                var processor = Activator.CreateInstance(processorType, attribute.Start, attribute.Length, setter)!;
                processors.Add((IColumnProcessor<TModel>)processor);
            }
            return processors.ToArray();
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