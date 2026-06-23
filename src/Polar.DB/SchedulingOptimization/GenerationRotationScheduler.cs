namespace GenerationPersonExternalKeySample;

public sealed class GenerationRotationScheduler
{
    private readonly PolarGenerationManager _manager;
    private readonly TimeSpan _checkEvery;

    public GenerationRotationScheduler(PolarGenerationManager manager, TimeSpan checkEvery)
    {
        _manager = manager;
        _checkEvery = checkEvery;
    }

    public bool RunOnce()
    {
        // Простой режим: проверили возраст активного поколения и вышли.
        return _manager.RotateIfDue(DateTime.UtcNow);
    }

    public async Task RunLoopAsync(CancellationToken token)
    {
        // Простой шедулер без Quartz/Hangfire: подходит для service/console demo.
        while (!token.IsCancellationRequested)
        {
            _manager.RotateIfDue(DateTime.UtcNow);
            await Task.Delay(_checkEvery, token);
        }
    }
}
