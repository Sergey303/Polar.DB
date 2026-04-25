using System.Linq.Expressions;
using System.Reflection;

namespace Polar.DB.Bench.Exec.PolarDbNuget.Reflection;

internal sealed class FastMethodInvoker
{
    private readonly Func<object?, object?[], object?> _call;

    private FastMethodInvoker(Func<object?, object?[], object?> call)
    {
        _call = call;
    }

    public object? Invoke(object? instance, params object?[] args)
    {
        return _call(instance, args);
    }

    public static FastMethodInvoker Create(MethodInfo method)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));

        var instanceParameter = Expression.Parameter(typeof(object), "instance");
        var argsParameter = Expression.Parameter(typeof(object[]), "args");

        var parameters = method.GetParameters();
        var callArgs = new Expression[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var index = Expression.ArrayIndex(argsParameter, Expression.Constant(i));
            callArgs[i] = Expression.Convert(index, parameters[i].ParameterType);
        }

        Expression? instance = null;
        if (!method.IsStatic)
        {
            instance = Expression.Convert(instanceParameter, method.DeclaringType!);
        }

        var call = Expression.Call(instance, method, callArgs);
        Expression body = method.ReturnType == typeof(void)
            ? Expression.Block(call, Expression.Constant(null, typeof(object)))
            : Expression.Convert(call, typeof(object));

        var lambda = Expression.Lambda<Func<object?, object?[], object?>>(body, instanceParameter, argsParameter);
        return new FastMethodInvoker(lambda.Compile());
    }
}
