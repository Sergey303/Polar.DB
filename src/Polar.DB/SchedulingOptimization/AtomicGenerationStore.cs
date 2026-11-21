using System.Text;

namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// Atomic publication of immutable storage generations on one filesystem.
/// The caller must fully flush and close data files before validating the candidate.
/// This class owns the publication pointer, not record-level transaction semantics.
/// </summary>
public sealed class AtomicGenerationStore
{
    public const string CurrentFileName = "CURRENT";
    private const string WriterLockFileName = ".generation-writer.lock";

    private readonly string _root;
    private readonly EpochFolderManager _epochs;

    public AtomicGenerationStore(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Generation root is required.", nameof(rootPath));

        _root = Path.GetFullPath(rootPath);
        _epochs = new EpochFolderManager(_root);
    }

    /// <summary>
    /// Returns only the committed generation. Unreferenced ready directories
    /// are intentionally ignored, including after an interrupted commit.
    /// If the published generation is corrupt, fail closed rather than
    /// silently selecting an uncommitted or stale generation.
    /// </summary>
    public string? Recover(Func<string, bool>? validate = null)
    {
        string pointer = Path.Combine(_root, CurrentFileName);
        if (!File.Exists(pointer))
            return null; // Existing legacy storage can remain active.

        string name = File.ReadAllText(pointer, Encoding.UTF8).Trim();
        if (string.IsNullOrWhiteSpace(name) ||
            name == "." || name == ".." ||
            name != Path.GetFileName(name) ||
            name.IndexOfAny(new[] { '/', '\\' }) >= 0)
            throw new InvalidDataException("Invalid committed generation pointer.");

        string path = Path.Combine(_root, name);
        if (!Directory.Exists(path) ||
            !File.Exists(Path.Combine(path, EpochFolderManager.ReadyMarkerFileName)))
            throw new InvalidDataException("Committed generation is missing or not ready.");

        if (validate != null && !validate(path))
            throw new InvalidDataException("Committed generation failed validation.");

        return path;
    }

    /// <summary>
    /// Acquires a cross-process single-writer lease and prepares an isolated
    /// generation. Dispose without Commit to abort. Do not edit the old generation.
    /// </summary>
    public PendingGeneration Prepare(Action<string> build)
    {
        if (build == null) throw new ArgumentNullException(nameof(build));
        Directory.CreateDirectory(_root);

        var lease = new FileStream(
            Path.Combine(_root, WriterLockFileName),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            Recover(); // Reject invalid current state before preparing a replacement.
            string candidate = _epochs.CreateBuilding();
            try
            {
                build(candidate);
                return new PendingGeneration(this, lease, candidate);
            }
            catch
            {
                TryDiscard(candidate);
                throw;
            }
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal void Publish(string path)
    {
        _epochs.MarkReady(path);
        string temporary = Path.Combine(_root, CurrentFileName + "." +
            Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(Path.GetFileName(path));
                file.Write(bytes, 0, bytes.Length);
                file.Flush(true);
            }

            string current = Path.Combine(_root, CurrentFileName);
            if (File.Exists(current))
                File.Replace(temporary, current, null);
            else
                File.Move(temporary, current);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void TryDiscard(string path)
    {
        // A failed preparation is not externally visible: CURRENT never names it.
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public sealed class PendingGeneration : IDisposable
    {
        private readonly AtomicGenerationStore _store;
        private readonly FileStream _lease;
        private bool _validated;
        private bool _committed;
        private bool _disposed;

        internal PendingGeneration(AtomicGenerationStore store, FileStream lease, string path)
        {
            _store = store;
            _lease = lease;
            Path = path;
        }

        public string Path { get; }

        public void Validate(Func<string, bool> validator)
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            ThrowIfDisposed();
            if (!validator(Path))
                throw new InvalidDataException("Prepared generation failed validation.");
            _validated = true;
        }

        public string Commit()
        {
            ThrowIfDisposed();
            if (!_validated)
                throw new InvalidOperationException("Validate must succeed before Commit.");
            if (_committed)
                throw new InvalidOperationException("Generation has already been committed.");

            _store.Publish(Path);
            _committed = true;
            return Path;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (!_committed) TryDiscard(Path);
            }
            finally { _lease.Dispose(); }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PendingGeneration));
        }
    }
}