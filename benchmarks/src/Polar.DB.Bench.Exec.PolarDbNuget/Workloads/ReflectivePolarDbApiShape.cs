using System.Reflection;
using Polar.DB.Bench.Exec.PolarDbNuget.Reflection;

namespace Polar.DB.Bench.Exec.PolarDbNuget.Workloads;

internal sealed class ReflectivePolarDbApiShape
{
    private readonly TypeResolver _resolver;
    private readonly Type _pTypeEnumerationType;
    private readonly Type _pTypeType;
    private readonly Type _namedTypeType;
    private readonly Type _pTypeRecordType;
    private readonly Type _sequenceType;

    private FastMethodInvoker? _append;
    private FastMethodInvoker? _build;
    private FastMethodInvoker? _refresh;
    private FastMethodInvoker? _lookup;
    private FastMethodInvoker? _dispose;

    public ReflectivePolarDbApiShape(Assembly polarAssembly)
    {
        _resolver = new TypeResolver(polarAssembly);

        _pTypeEnumerationType = _resolver.Require(
            "Polar.PTypeEnumeration",
            "Polar.DB.PTypeEnumeration");

        _pTypeType = _resolver.Require(
            "Polar.PType",
            "Polar.DB.PType");

        _namedTypeType = _resolver.Require(
            "Polar.NamedType",
            "Polar.DB.NamedType");

        _pTypeRecordType = _resolver.Require(
            "Polar.PTypeRecord",
            "Polar.DB.PTypeRecord");

        _sequenceType = _resolver.Require(
            "Polar.Universal.USequence",
            "Polar.USequence",
            "Polar.DB.USequence",
            "Polar.Universal.UniversalSequenceBase");
    }

    public object CreateRecordType()
    {
        var integerType = CreatePrimitiveType("integer", "Integer", "int", "Int32");
        var stringType = CreatePrimitiveType("sstring", "SString", "string", "String");

        var idField = ReflectionHelpers.InvokeBestConstructor(_namedTypeType, "id", integerType);
        var nameField = ReflectionHelpers.InvokeBestConstructor(_namedTypeType, "name", stringType);

        var fieldsArray = Array.CreateInstance(_namedTypeType, 2);
        fieldsArray.SetValue(idField, 0);
        fieldsArray.SetValue(nameField, 1);

        return ReflectionHelpers.InvokeBestConstructor(_pTypeRecordType, fieldsArray);
    }

    public object CreateSequence(object recordType, string dataPath, string statePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dataPath)) ?? ".");

        var attempts = new List<object?[]>
        {
            new object?[] { recordType, dataPath },
            new object?[] { dataPath, recordType },
            new object?[] { recordType, dataPath, statePath },
            new object?[] { dataPath, statePath, recordType },
            new object?[] { dataPath, recordType, false },
            new object?[] { recordType, dataPath, false },
            new object?[] { dataPath, statePath, recordType, false },
            new object?[] { recordType, dataPath, statePath, false }
        };

        var failures = new List<string>();

        foreach (var args in attempts)
        {
            try
            {
                return ReflectionHelpers.InvokeBestConstructor(_sequenceType, args);
            }
            catch (Exception ex)
            {
                failures.Add($"({string.Join(", ", args.Select(a => a?.GetType().Name ?? "null"))}): {ex.Message}");
            }
        }

        throw new ReflectionBindingException(
            $"Cannot create sequence type {_sequenceType.FullName}. Tried common constructor shapes:\n" + string.Join("\n", failures));
    }

    public void AppendRecord(object sequence, object[] record)
    {
        _append ??= FastMethodInvoker.Create(ReflectionHelpers.RequireMethod(
            sequence.GetType(),
            ["AppendElement", "Append", "Add", "AppendValue"],
            parameterCount: 1));

        _append.Invoke(sequence, record);
    }

    public void Build(object sequence)
    {
        _build ??= FastMethodInvoker.Create(ReflectionHelpers.RequireMethod(
            sequence.GetType(),
            ["Build"],
            parameterCount: 0));

        _build.Invoke(sequence);
    }

    public void Refresh(object sequence)
    {
        var method = ReflectionHelpers.TryFindMethod(sequence.GetType(), ["Refresh", "Reload", "Reopen"], parameterCount: 0);
        if (method == null)
        {
            return;
        }

        _refresh ??= FastMethodInvoker.Create(method);
        _refresh.Invoke(sequence);
    }

    public object? Lookup(object sequence, int key)
    {
        var method = ReflectionHelpers.TryFindMethod(sequence.GetType(), ["GetByKey", "GetById", "Get"], parameterCount: 1);
        if (method == null)
        {
            throw new ReflectionBindingException(
                $"Cannot find lookup method on {sequence.GetType().FullName}. Tried: GetByKey(key), GetById(key), Get(key). " +
                "If this version exposes lookup through UKeyIndex, adapt ReflectivePolarDbApiShape.Lookup().");
        }

        _lookup ??= FastMethodInvoker.Create(method);
        return _lookup.Invoke(sequence, key);
    }

    public void DisposeIfSupported(object sequence)
    {
        if (sequence is IDisposable disposable)
        {
            disposable.Dispose();
            return;
        }

        var method = ReflectionHelpers.TryFindMethod(sequence.GetType(), ["Close", "Flush"], parameterCount: 0);
        if (method == null)
        {
            return;
        }

        _dispose ??= FastMethodInvoker.Create(method);
        _dispose.Invoke(sequence);
    }

    private object CreatePrimitiveType(params string[] enumNames)
    {
        var value = ReflectionHelpers.ParseEnumValue(_pTypeEnumerationType, enumNames);
        return ReflectionHelpers.InvokeBestConstructor(_pTypeType, value);
    }
}
