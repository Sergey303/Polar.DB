namespace Polar.DB.Bench.Exec.PolarDbNuget.Reflection;

internal sealed class ReflectionBindingException : InvalidOperationException
{
    public ReflectionBindingException(string message)
        : base(message)
    {
    }
}
