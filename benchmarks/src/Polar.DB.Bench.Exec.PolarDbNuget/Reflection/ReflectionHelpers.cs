using System.Reflection;

namespace Polar.DB.Bench.Exec.PolarDbNuget.Reflection;

internal static class ReflectionHelpers
{
    public static MethodInfo RequireMethod(Type type, IReadOnlyList<string> names, int? parameterCount = null)
    {
        var method = TryFindMethod(type, names, parameterCount);
        if (method == null)
        {
            var countText = parameterCount.HasValue ? $" with {parameterCount.Value} parameters" : "";
            throw new ReflectionBindingException($"Cannot find method on {type.FullName}{countText}. Tried: {string.Join(", ", names)}");
        }

        return method;
    }

    public static MethodInfo? TryFindMethod(Type type, IReadOnlyList<string> names, int? parameterCount = null)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        foreach (var name in names)
        {
            var candidates = type.GetMethods(flags)
                .Where(m => string.Equals(m.Name, name, StringComparison.Ordinal))
                .Where(m => !parameterCount.HasValue || m.GetParameters().Length == parameterCount.Value)
                .OrderByDescending(m => m.IsPublic)
                .ThenBy(m => m.GetParameters().Length)
                .ToArray();

            if (candidates.Length > 0)
            {
                return candidates[0];
            }
        }

        return null;
    }

    public static ConstructorInfo RequireConstructor(Type type, params Type[] exactParameterTypes)
    {
        var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, binder: null, exactParameterTypes, modifiers: null);
        if (ctor != null)
        {
            return ctor;
        }

        throw new ReflectionBindingException(
            $"Cannot find constructor {type.FullName}({string.Join(", ", exactParameterTypes.Select(t => t.Name))}).");
    }

    public static ConstructorInfo? TryFindCompatibleConstructor(Type type, IReadOnlyList<object?> args)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return type.GetConstructors(flags)
            .Where(c => c.GetParameters().Length == args.Count)
            .Where(c => ParametersCanAccept(c.GetParameters(), args))
            .OrderByDescending(c => c.IsPublic)
            .FirstOrDefault();
    }

    public static object InvokeBestConstructor(Type type, params object?[] args)
    {
        var ctor = TryFindCompatibleConstructor(type, args);
        if (ctor == null)
        {
            var argTypes = args.Select(a => a?.GetType().FullName ?? "null");
            throw new ReflectionBindingException($"Cannot find compatible constructor for {type.FullName}. Args: {string.Join(", ", argTypes)}");
        }

        return ConstructorInvoker.Create(ctor).Invoke(args);
    }

    private static bool ParametersCanAccept(ParameterInfo[] parameters, IReadOnlyList<object?> args)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            var arg = args[i];

            if (arg == null)
            {
                if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                {
                    return false;
                }

                continue;
            }

            if (!parameterType.IsInstanceOfType(arg))
            {
                return false;
            }
        }

        return true;
    }

    public static object ParseEnumValue(Type enumType, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var valueName in Enum.GetNames(enumType))
            {
                if (string.Equals(valueName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Enum.Parse(enumType, valueName);
                }
            }
        }

        throw new ReflectionBindingException($"Cannot resolve enum value for {enumType.FullName}. Tried: {string.Join(", ", names)}");
    }
}
