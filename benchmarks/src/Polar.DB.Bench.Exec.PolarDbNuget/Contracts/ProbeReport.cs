namespace Polar.DB.Bench.Exec.PolarDbNuget.Contracts;

internal sealed class ProbeReport
{
    public List<string> Assemblies { get; set; } = new();
    public List<TypeProbe> CandidateTypes { get; set; } = new();
}

internal sealed class TypeProbe
{
    public string Candidate { get; set; } = "";
    public bool Found { get; set; }
    public string? AssemblyQualifiedName { get; set; }
    public List<string> Constructors { get; set; } = new();
    public List<string> Methods { get; set; } = new();
}
