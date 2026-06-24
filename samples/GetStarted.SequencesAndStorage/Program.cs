using System.Text;

namespace GetStarted.SequencesAndStorage;

internal static class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Run("Scheduling optimization", SchedulingOptimizationExample.Run);
    }

    private static void Run(string title, Action run)
    {
        Console.WriteLine(new string('=', 100));
        Console.WriteLine(title);
        Console.WriteLine(new string('-', 100));
        run();
        Console.WriteLine();
    }
}
