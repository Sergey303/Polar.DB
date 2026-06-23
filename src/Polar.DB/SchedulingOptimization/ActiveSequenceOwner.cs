using Polar.Universal;

namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// Единая точка изменений активной USequence.
/// USequence не знает про ротацию эпох и подписки.
/// </summary>
public sealed class ActiveSequenceOwner : IDisposable
{
    private readonly object _mutationLock = new();
    private USequence _active;
    private AppendCollector? _collector;
    private bool _disposed;

    public ActiveSequenceOwner(USequence active)
    {
        _active = active ?? throw new ArgumentNullException(nameof(active));
    }

    public USequence Active
    {
        get
        {
            lock (_mutationLock)
            {
                ThrowIfDisposed();
                return _active;
            }
        }
    }

    public void AppendElement(object element)
    {
        lock (_mutationLock)
        {
            ThrowIfDisposed();
            _active.AppendElement(element);
            _collector?.Add(element);
        }
    }

    public ActiveSequenceRotation BeginRotation()
    {
        lock (_mutationLock)
        {
            ThrowIfDisposed();
            if (_collector != null)
                throw new InvalidOperationException("Epoch rotation is already active.");

            var collector = new AppendCollector();
            _collector = collector;
            return new ActiveSequenceRotation(_active, collector);
        }
    }

    public USequence CompleteRotation(
        ActiveSequenceRotation rotation,
        USequence newActive,
        Action markReady)
    {
        if (rotation == null) throw new ArgumentNullException(nameof(rotation));
        if (newActive == null) throw new ArgumentNullException(nameof(newActive));
        if (markReady == null) throw new ArgumentNullException(nameof(markReady));

        lock (_mutationLock)
        {
            ThrowIfDisposed();
            EnsureCurrentRotation(rotation);
            _collector = null;

            foreach (var element in rotation.Collector.TakeSnapshot())
                newActive.AppendElement(element);

            newActive.Flush();
            markReady();

            var oldActive = _active;
            _active = newActive;
            return oldActive;
        }
    }

    public void CancelRotation(ActiveSequenceRotation rotation)
    {
        if (rotation == null) throw new ArgumentNullException(nameof(rotation));

        lock (_mutationLock)
        {
            if (ReferenceEquals(_collector, rotation.Collector))
                _collector = null;
        }
    }

    public void Dispose()
    {
        lock (_mutationLock)
        {
            if (_disposed) return;
            _disposed = true;
            _collector = null;
            _active.Dispose();
        }
    }

    private void EnsureCurrentRotation(ActiveSequenceRotation rotation)
    {
        if (!ReferenceEquals(_collector, rotation.Collector))
            throw new InvalidOperationException("Epoch rotation is not current.");
        if (!ReferenceEquals(_active, rotation.Source))
            throw new InvalidOperationException("Active sequence was changed during rotation.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ActiveSequenceOwner));
    }
}

public sealed class ActiveSequenceRotation
{
    internal ActiveSequenceRotation(USequence source, AppendCollector collector)
    {
        Source = source;
        Collector = collector;
    }

    public USequence Source { get; }
    internal AppendCollector Collector { get; }
}
