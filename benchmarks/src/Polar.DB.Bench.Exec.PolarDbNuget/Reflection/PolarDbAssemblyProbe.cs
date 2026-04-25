using System.Reflection;
using Polar.DB.Bench.Exec.PolarDbNuget.Contracts;

namespace Polar.DB.Bench.Exec.PolarDbNuget.Reflection;

internal static class PolarDbAssemblyProbe
{
    public static readonly string[] CandidateTypeNames =
    [
        "Polar.PTypeEnumeration",
        "Polar.DB.PTypeEnumeration",
        "Polar.PType",
        "Polar.DB.PType",
        "Polar.NamedType",
        "Polar.DB.NamedType",
        "Polar.PTypeRecord",
        "Polar.DB.PTypeRecord",
        "Polar.PTypeSequence",
        "Polar.DB.PTypeSequence",
        "Polar.Universal.USequence",
        "Polar.USequence",
        "Polar.DB.USequence",
        "Polar.Universal.UniversalSequenceBase",
        "Polar.Universal.UKeyIndex",
        "Polar.UKeyIndex",
        "Polar.DB.UKeyIndex"
    ];

    public static ProbeReport Create(Assembly assembly)
    {
        var report = new ProbeReport();
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .OrderBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => a.FullName ?? a.GetName().Name ?? "unknown")
            .ToArray();

        report.Assemblies.AddRange(loadedAssemblies);

        foreach (var candidate in CandidateTypeNames)
        {
            var type = assembly.GetType(candidate, throwOnError: false, ignoreCase: false);
            if (type == null)
            {
                type = TypeResolver.SafeGetTypes(assembly).FirstOrDefault(t => string.Equals(t.Name, candidate.Split('.').Last(), StringComparison.Ordinal));
            }

            var item = new TypeProbe
            {
                Candidate = candidate,
                Found = type != null,
                AssemblyQualifiedName = type?.AssemblyQualifiedName
            };

            if (type != null)
            {
                item.Constructors.AddRange(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(FormatConstructor)
                    .OrderBy(x => x, StringComparer.Ordinal));

                item.Methods.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => !m.IsSpecialName)
                    .Select(FormatMethod)
                    .Distinct()
                    .OrderBy(x => x, StringComparer.Ordinal));
            }

            report.CandidateTypes.Add(item);
        }

        return report;
    }

    private static string FormatConstructor(ConstructorInfo constructor)
    {
        return $".ctor({string.Join(", ", constructor.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})";
    }

    private static string FormatMethod(MethodInfo method)
    {
        return $"{method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})";
    }
}
