namespace Polar.DB.Typed.Concurrency;

internal sealed class DbSetGate : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly object _sequenceReadLock = new();

    public TResult Read<TResult>(Func<TResult> read)
    {
        _lock.EnterReadLock();
        try { return read(); }
        finally { _lock.ExitReadLock(); }
    }

    public TResult SequenceRead<TResult>(Func<TResult> read)
    {
        _lock.EnterReadLock();
        try
        {
            lock (_sequenceReadLock)
            {
                return read();
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Write(Action write, Action? rollback = null)
    {
        _lock.EnterWriteLock();
        try { write(); }
        catch
        {
            rollback?.Invoke();
            throw;
        }
        finally { _lock.ExitWriteLock(); }
    }

    public void Dispose() => _lock.Dispose();
}
