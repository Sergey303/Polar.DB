namespace Polar.DB.Bench.Exec.ExternalNuget;

internal sealed record ExternalProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Success => ExitCode == 0;
}
