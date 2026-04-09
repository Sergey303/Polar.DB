using System.Reflection;

namespace Polar.DB.Tests;

internal static class UKeyIndexTestHelpers
{
    internal static readonly PTypeRecord RecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)));

    internal sealed class SequenceScope : IDisposable
    {
        private readonly string _tempDir;
        private int _fileNo;

        public SequenceScope()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            Sequence = new USequence(
                RecordType,
                Path.Combine(_tempDir, "state.bin"),
                StreamGen,
                _ => false,
                value => (int)((object[])value)[0],
                key => (int)key,
                optimise: false);
        }

        public USequence Sequence { get; }

        public Stream StreamGen()
        {
            return new FileStream(
                Path.Combine(_tempDir, $"f{_fileNo++}.bin"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
        }

        public void Dispose()
        {
            try { Sequence.Close(); } catch { }
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }
    }

    internal static object[] Row(int id, string name) => new object[] { id, name };

    internal static int IdOf(object record) => (int)((object[])record)[0];

    internal static string NameOf(object record) => (string)((object[])record)[1];

    internal static UKeyIndexAdapter CreateIndex(
        SequenceScope scope,
        Func<object, IComparable> keyFunc,
        Func<IComparable, int> hashFunc,
        bool keysInMemory)
    {
        return new UKeyIndexAdapter(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);
    }

    internal static void LoadAndBuild(SequenceScope scope, UKeyIndexAdapter index, params object[][] rows)
    {
        scope.Sequence.Load(rows.Cast<object>().ToArray());
        index.Build();
    }

    internal sealed class UKeyIndexAdapter
    {
        private static readonly Type UKeyIndexType =
            typeof(USequence).Assembly.GetType("Polar.DB.UKeyIndex", throwOnError: true)!;

        private readonly object _instance;
        private readonly MethodInfo _build;
        private readonly MethodInfo _refresh;
        private readonly MethodInfo _getByKey;
        private readonly MethodInfo _onAppendElement;
        private readonly MethodInfo _isOriginal;
        private readonly MethodInfo _clear;
        private readonly MethodInfo _flush;
        private readonly MethodInfo _close;

        public UKeyIndexAdapter(
            Func<Stream> streamGen,
            USequence sequence,
            Func<object, IComparable> keyFunc,
            Func<IComparable, int> hashFunc,
            bool keysInMemory)
        {
            _instance = Activator.CreateInstance(
                UKeyIndexType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { streamGen, sequence, keyFunc, hashFunc, keysInMemory },
                culture: null)!;

            _build = GetMethod("Build");
            _refresh = GetMethod("Refresh");
            _getByKey = GetMethod("GetByKey");
            _onAppendElement = GetMethod("OnAppendElement");
            _isOriginal = GetMethod("IsOriginal");
            _clear = GetMethod("Clear");
            _flush = GetMethod("Flush");
            _close = GetMethod("Close");
        }

        public void Build() => _build.Invoke(_instance, Array.Empty<object?>());

        public void Refresh() => _refresh.Invoke(_instance, Array.Empty<object?>());

        public object? GetByKey(IComparable key) => _getByKey.Invoke(_instance, new object?[] { key });

        public void OnAppendElement(object element, long offset) =>
            _onAppendElement.Invoke(_instance, new object?[] { element, offset });

        public bool IsOriginal(IComparable key, long offset) =>
            (bool)_isOriginal.Invoke(_instance, new object?[] { key, offset })!;

        public void Clear() => _clear.Invoke(_instance, Array.Empty<object?>());

        public void Flush() => _flush.Invoke(_instance, Array.Empty<object?>());

        public void Close() => _close.Invoke(_instance, Array.Empty<object?>());

        private static MethodInfo GetMethod(string name) =>
            UKeyIndexType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(UKeyIndexType.FullName, name);
    }
}
