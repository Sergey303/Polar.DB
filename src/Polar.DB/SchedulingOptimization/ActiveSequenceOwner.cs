using Polar.Universal;

namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// Single mutation gate for the active USequence.
/// USequence itself stays unaware of epochs, collectors and rotation.
/// </summary>
public sealed class ActiveSequenceOwner : IDisposable
{
    private readonly object _mutationLock = new();
    public USequence Active { get; private set; }
    private AppendCollector? _collector;
    private bool _disposed;

    public ActiveSequenceOwner(USequence active)
    {
        Active = active ?? throw new ArgumentNullException(nameof(active));
    }

    public void AppendElement(object element)
    {
        lock (_mutationLock)
        {
            ThrowIfDisposed();
            Active.AppendElement(element);
            _collector?.Add(element);
        }
    }

    public T Read<T>(Func<USequence, T> read)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));

        lock (_mutationLock)
        {
            ThrowIfDisposed();
            return read(Active);
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
            return new ActiveSequenceRotation(Active, collector);
        }
    }

    public T ReadForRotation<T>(ActiveSequenceRotation rotation, Func<USequence, T> read)
    {
        if (rotation == null) throw new ArgumentNullException(nameof(rotation));
        if (read == null) throw new ArgumentNullException(nameof(read));

        lock (_mutationLock)
        {
            ThrowIfDisposed();
            EnsureCurrentRotation(rotation);
            return read(Active);
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

            var oldActive = Active;
            Active = newActive;
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
            Active.Dispose();
        }
    }

    private void EnsureCurrentRotation(ActiveSequenceRotation rotation)
    {
        if (!ReferenceEquals(_collector, rotation.Collector))
            throw new InvalidOperationException("Epoch rotation is not current.");
        if (!ReferenceEquals(Active, rotation.Source))
            throw new InvalidOperationException("Active sequence changed during rotation.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ActiveSequenceOwner));
    }
}
