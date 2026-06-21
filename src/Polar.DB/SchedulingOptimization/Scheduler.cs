namespace Polar.DB.SchedulingOptimization;

public static class Scheduler
{
    public static async Task RunAsync(Func<CancellationToken, Task> @do, TimeSpan checkEvery, bool runImmediately, CancellationToken token)
    {
        if(runImmediately)
            await @do(token);
        
        // Простой шедулер без Quartz/Hangfire
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(checkEvery, token);
            await @do(token);
        }
    }
}
