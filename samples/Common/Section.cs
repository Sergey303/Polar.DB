namespace Common;

public static class Section
{
    public static void Run(string title, Action action)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Section title is required.", nameof(title));
        if (action == null) throw new ArgumentNullException(nameof(action));

        Console.WriteLine();
        Console.WriteLine(new string('=', title.Length));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));

        action();
    }
}
