using System.Reflection;

namespace Polar.DB.Bench.Exec.PolarDbNuget.Reflection;

internal sealed class TypeResolver
{
    private readonly Assembly _assembly;

    public TypeResolver(Assembly assembly)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
    }

    public Type Require(params string[] candidates)
    {
        return TryResolve(candidates)
            ?? throw new ReflectionBindingException("Cannot resolve required type. Tried: " + string.Join(", ", candidates));
    }

    public Type? TryResolve(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var type = _assembly.GetType(candidate, throwOnError: false, ignoreCase: false);
            if (type != null)
            {
                return type;
            }
        }

        var allTypes = SafeGetTypes(_assembly);
        foreach (var candidate in candidates)
        {
            var shortName = candidate.Split('.').Last();
            var type = allTypes.FirstOrDefault(t => string.Equals(t.Name, shortName, StringComparison.Ordinal));
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    public static IReadOnlyList<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).Cast<Type>().ToArray();
        }
    }
}
