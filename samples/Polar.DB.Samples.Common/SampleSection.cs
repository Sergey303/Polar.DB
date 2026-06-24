namespace Polar.DB.Samples.Common;

public static class SampleSection
{
    public static void Run(string title, Action action)
    {
        Console.WriteLine(new string('=', 100));
        Console.WriteLine(title);
        Console.WriteLine(new string('-', 100));
        action();
        Console.WriteLine();
    }
}