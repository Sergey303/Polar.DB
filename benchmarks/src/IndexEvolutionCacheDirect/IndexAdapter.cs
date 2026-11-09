using Polar.DB;

namespace IndexEvolutionDirect;

internal interface IIndexAdapter : IDisposable
{
    void Load(IEnumerable<object> records);
    void PreparePrerequisites();
    void BuildAndFlush();
    IEnumerable<object> Lookup(int externalKey);
}

internal sealed class SequentialStreams : IDisposable
{
    private readonly string _directory;
    private int _next;
    private readonly List<FileStream> _opened = new();

    internal SequentialStreams(string directory) => _directory = directory;

    internal Stream OpenNext()
    {
        string path = Path.Combine(_directory, $"file-{_next++}.bin");
        var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.None, bufferSize: 64 * 1024);
        _opened.Add(file);
        return file;
    }

    public void Dispose()
    {
        foreach (var file in _opened) file.Dispose();
    }
}
