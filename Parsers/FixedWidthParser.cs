using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Benchmarks.Attributes;
using Benchmarks.Processors;
using CommunityToolkit.HighPerformance.Buffers;

namespace Benchmarks.Parsers
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

        private static bool CreateColumnProcessor(PropertyInfo prop, [NotNullWhen(true)] out IColumnProcessor<TModel>? processor)
        {
            var attribute = prop.GetCustomAttribute<FixedColumnAttribute>();
            if (attribute is null)
            {
                processor = null;
                return false;
            }
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
            processor = (IColumnProcessor<TModel>)Activator.CreateInstance(processorType, attribute.Start, attribute.Length, setter)!;
            return true;
        }
        private static bool CreateColumnProcessor(FieldInfo field, [NotNullWhen(true)] out IColumnProcessor<TModel>? processor)
        {
            var attribute = field.GetCustomAttribute<FixedColumnAttribute>();
            if (attribute is null)
            {
                processor = null;
                return false;
            }
            var targetExpr = Expression.Parameter(typeof(TModel).MakeByRefType(), "model");
            var valueExpr = Expression.Parameter(field.FieldType, "value");
            var fieldExpr = Expression.Field(targetExpr, field);
            var assignExpr = Expression.Assign(fieldExpr, valueExpr);
            var actionType = typeof(RefAction<,>).MakeGenericType(typeof(TModel), field.FieldType);
            var setter = Expression.Lambda(actionType, assignExpr, targetExpr, valueExpr).Compile();
            var processorType = field.FieldType switch
            {
                Type t when t == typeof(string) => typeof(StringColumnProcessor<>).MakeGenericType(typeof(TModel)),
                Type t when t == typeof(double) => typeof(DoubleColumnProcessor<>).MakeGenericType(typeof(TModel)),
                Type t when t == typeof(float) => typeof(FloatColumnProcessor<>).MakeGenericType(typeof(TModel)),
                _ => typeof(ColumnProcessor<,>).MakeGenericType(typeof(TModel), field.FieldType)
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