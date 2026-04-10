using System.Reflection;
using Xunit;

namespace Polar.DB.Tests;

public class TextFlowReaderNegativeTests
{
    [Fact]
    public void ReadByte_With_Invalid_Token_Throws()
    {
        var flow = CreateTextFlow("999");
        Assert.ThrowsAny<Exception>(() => Invoke<object>(flow, "ReadByte"));
    }

    [Fact]
    public void ReadInt32_With_Invalid_Token_Throws()
    {
        var flow = CreateTextFlow("abc");
        Assert.ThrowsAny<Exception>(() => Invoke<object>(flow, "ReadInt32"));
    }

    [Fact]
    public void ReadInt64_With_Invalid_Token_Throws()
    {
        var flow = CreateTextFlow("abc");
        Assert.ThrowsAny<Exception>(() => Invoke<object>(flow, "ReadInt64"));
    }

    [Fact]
    public void ReadDouble_With_Invalid_Token_Throws()
    {
        var flow = CreateTextFlow("abc");
        Assert.ThrowsAny<Exception>(() => Invoke<object>(flow, "ReadDouble"));
    }

    [Fact]
    public void ReadString_With_Unterminated_Quoted_Text_Throws()
    {
        var flow = CreateTextFlow("\"abc");
        Assert.ThrowsAny<Exception>(() => Invoke<object>(flow, "ReadString"));
    }

    [Fact]
    public void ReadString_With_Unfinished_Escape_Throws()
    {
        var flow = CreateTextFlow("\"abc\\");
        Assert.ThrowsAny<Exception>(() => Invoke<object>(flow, "ReadString"));
    }

    private static object CreateTextFlow(string text)
    {
        var type = typeof(TextFlow);
        var ctor = type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 1 && typeof(TextReader).IsAssignableFrom(ps[0].ParameterType);
            });

        if (ctor == null)
            throw new InvalidOperationException("Could not find TextFlow(TextReader) constructor.");

        return ctor.Invoke(new object[] { new StringReader(text) });
    }

    private static T Invoke<T>(object target, string methodName)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (method == null)
            throw new MissingMethodException(target.GetType().FullName, methodName);

        try
        {
            object? result = method.Invoke(target, Array.Empty<object>());
            return result is null ? default! : (T)result;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }
}
