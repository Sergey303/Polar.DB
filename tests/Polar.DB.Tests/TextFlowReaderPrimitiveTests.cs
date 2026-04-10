using System.Reflection;
using Xunit;

namespace Polar.DB.Tests;

public class TextFlowReaderPrimitiveTests
{
    [Fact]
    public void Skip_Skips_Leading_Whitespace_Before_ReadInt32()
    {
        string text = " \t\r\n" + SerializeToText(123, new PType(PTypeEnumeration.integer));
        object flow = CreateTextFlow(text);

        InvokeVoid(flow, "Skip");
        int value = Invoke<int>(flow, "ReadInt32");

        Assert.Equal(123, value);
    }

    [Fact]
    public void ReadBoolean_Parses_Serialized_Boolean()
    {
        object flow = CreateTextFlow(SerializeToText(true, new PType(PTypeEnumeration.boolean)));
        bool value = Invoke<bool>(flow, "ReadBoolean");
        Assert.True(value);
    }

    [Fact]
    public void ReadByte_Parses_Serialized_Byte()
    {
        object flow = CreateTextFlow(SerializeToText((byte)0xAB, new PType(PTypeEnumeration.@byte)));
        byte value = Invoke<byte>(flow, "ReadByte");
        Assert.Equal((byte)0xAB, value);
    }

    [Fact]
    public void ReadChar_Parses_Serialized_Character()
    {
        object flow = CreateTextFlow(SerializeToText('Q', new PType(PTypeEnumeration.character)));
        char value = Invoke<char>(flow, "ReadChar");
        Assert.Equal('Q', value);
    }

    [Fact]
    public void ReadInt32_Parses_Serialized_Integer()
    {
        object flow = CreateTextFlow(SerializeToText(4567, new PType(PTypeEnumeration.integer)));
        int value = Invoke<int>(flow, "ReadInt32");
        Assert.Equal(4567, value);
    }

    [Fact]
    public void ReadInt64_Parses_Serialized_LongInteger()
    {
        object flow = CreateTextFlow(SerializeToText(9876543210123L, new PType(PTypeEnumeration.longinteger)));
        long value = Invoke<long>(flow, "ReadInt64");
        Assert.Equal(9876543210123L, value);
    }

    [Fact]
    public void ReadDouble_Parses_Serialized_Real()
    {
        object flow = CreateTextFlow(SerializeToText(1234.5678, new PType(PTypeEnumeration.real)));
        double value = Invoke<double>(flow, "ReadDouble");
        Assert.Equal(1234.5678, value, 10);
    }

    [Fact]
    public void ReadString_Parses_Serialized_Escaped_String()
    {
        const string original = "line1\nline2\t\\quote:\"";
        object flow = CreateTextFlow(SerializeToText(original, new PType(PTypeEnumeration.sstring)));

        string value = Invoke<string>(flow, "ReadString");

        Assert.Equal(original, value);
    }

    private static string SerializeToText(object value, PType type)
    {
        using var writer = new StringWriter();
        TextFlow.Serialize(writer, value, type);
        return writer.ToString();
    }

    private static object CreateTextFlow(string text)
    {
        Type tfType = typeof(TextFlow);

        ConstructorInfo? ctor =
            tfType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(c =>
                {
                    ParameterInfo[] p = c.GetParameters();
                    return p.Length == 1 && typeof(TextReader).IsAssignableFrom(p[0].ParameterType);
                });

        if (ctor == null)
            throw new InvalidOperationException("Could not find TextFlow(TextReader) constructor.");

        return ctor.Invoke(new object[] { new StringReader(text) });
    }

    private static T Invoke<T>(object instance, string methodName)
    {
        MethodInfo method =
            instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);

        object? result = method.Invoke(instance, Array.Empty<object>());
        return Assert.IsType<T>(result);
    }

    private static void InvokeVoid(object instance, string methodName)
    {
        MethodInfo method =
            instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);

        method.Invoke(instance, Array.Empty<object>());
    }
}
