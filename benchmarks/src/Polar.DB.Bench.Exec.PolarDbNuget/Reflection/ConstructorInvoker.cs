using System.Linq.Expressions;
using System.Reflection;

namespace Polar.DB.Bench.Exec.PolarDbNuget.Reflection;

internal sealed class ConstructorInvoker
{
    private readonly Func<object?[], object> _call;

    private ConstructorInvoker(Func<object?[], object> call)
    {
        _call = call;
    }

    public object Invoke(params object?[] args)
    {
        return _call(args);
    }

    public static ConstructorInvoker Create(ConstructorInfo constructor)
    {
        var argsParameter = Expression.Parameter(typeof(object[]), "args");
        var parameters = constructor.GetParameters();
        var callArgs = new Expression[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var index = Expression.ArrayIndex(argsParameter, Expression.Constant(i));
            callArgs[i] = Expression.Convert(index, parameters[i].ParameterType);
        }

        var body = Expression.New(constructor, callArgs);
        var lambda = Expression.Lambda<Func<object?[], object>>(Expression.Convert(body, typeof(object)), argsParameter);
        return new ConstructorInvoker(lambda.Compile());
    }
}
