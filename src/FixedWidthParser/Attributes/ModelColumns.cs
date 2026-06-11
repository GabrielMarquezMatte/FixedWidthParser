using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FixedWidthParser.Attributes
{
    /// <summary>
    /// Shared reflection over a model's <see cref="FixedColumnAttribute"/> members, used by both the
    /// parser and the writer: enumerating the columns (with layout validation) and building the
    /// strongly-typed member-access expression. Non-generic (operates on <see cref="Type"/>) so it
    /// serves models with and without the <c>allows ref struct</c> constraint alike.
    /// </summary>
    internal static class ModelColumns
    {
        /// <summary>
        /// Invokes <paramref name="visit"/> for every property/field annotated with
        /// <see cref="FixedColumnAttribute"/> (properties first, then fields), then validates the
        /// overall column layout.
        /// </summary>
        public static void ForEachColumn(Type modelType, Action<MemberInfo, FixedColumnAttribute> visit)
        {
            var columns = new List<(int Start, int Length, string Name)>();
            foreach (var property in modelType.GetProperties())
            {
                Visit(property);
            }
            foreach (var field in modelType.GetFields())
            {
                Visit(field);
            }
            ColumnLayoutValidator.Validate(CollectionsMarshal.AsSpan(columns), modelType);
            void Visit(MemberInfo member)
            {
                var attribute = member.GetCustomAttribute<FixedColumnAttribute>();
                if (attribute is null)
                {
                    return;
                }
                columns.Add((attribute.Start, attribute.Length, member.Name));
                visit(member, attribute);
            }
        }

        /// <summary>
        /// Builds the by-ref model parameter and the member access for <paramref name="member"/>,
        /// returning the member's type too. The caller turns this into a setter (an assignment) or a
        /// getter (a read).
        /// </summary>
        public static (Type MemberType, ParameterExpression Model, MemberExpression Access) MemberAccess(Type modelType, MemberInfo member)
        {
            var model = Expression.Parameter(modelType.MakeByRefType(), "model");
            return member switch
            {
                PropertyInfo p => (p.PropertyType, model, Expression.Property(model, p)),
                FieldInfo f => (f.FieldType, model, Expression.Field(model, f)),
                _ => throw new ArgumentException($"Unsupported member: {member.GetType().Name}", nameof(member))
            };
        }
    }
}
