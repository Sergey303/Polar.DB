namespace GetStarted.IndexesAndSearch;

internal sealed class ScenarioWorkspace
{
    private readonly string _root;

    public ScenarioWorkspace(string scenarioId)
    {
        _root = SamplePaths.File(scenarioId);
        Reset();
    }

    public string RootDirectory => _root;

    public void Reset()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        Directory.CreateDirectory(_root);
    }

    public string PathFor(string fileName) => Path.Combine(_root, fileName);

    public Func<Stream> CreateStreamFactory(string prefix)
    {
        var counter = 0;
        return () =>
        {
            var path = PathFor($"{prefix}-{counter++:00}.bin");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        };
    }

    public FileStream OpenStream(string fileName)
    {
        return new FileStream(PathFor(fileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
    }
}
