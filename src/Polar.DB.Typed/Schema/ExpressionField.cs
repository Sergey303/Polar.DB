using System.Linq.Expressions;

namespace Polar.DB.Typed.Schema;

internal static class ExpressionField
{
    public static string Name<T, TKey>(Expression<Func<T, TKey>> field)
    {
        if (field == null) throw new ArgumentNullException(nameof(field));
        Expression body = field.Body;

        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        if (body is MemberExpression member)
            return member.Member.Name;

        throw new ArgumentException("Use a simple member expression like x => x.Age.", nameof(field));
    }

    public static string Name(LambdaExpression field)
    {
        Expression body = field.Body;

        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        if (body is MemberExpression member)
            return member.Member.Name;

        throw new ArgumentException("Use a simple member expression like x => x.Id.", nameof(field));
    }
}
