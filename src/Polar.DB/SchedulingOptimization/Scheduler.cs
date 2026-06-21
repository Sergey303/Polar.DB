namespace Polar.DB.SchedulingOptimization;

public static class Scheduler
{
    public static async Task RunAsync(Action<CancellationToken> @do, TimeSpan checkEvery, CancellationToken token)
    {
        // Простой шедулер без Quartz/Hangfire
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(checkEvery, token);
            @do(token);
            
        }
    }
}
