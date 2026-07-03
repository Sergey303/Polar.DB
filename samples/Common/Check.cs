namespace Common;

public static class Check
{
    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}. Expected: {expected}; actual: {actual}.");
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        var expectedArray = expected.ToArray();
        var actualArray = actual.ToArray();

        if (!expectedArray.SequenceEqual(actualArray))
        {
            string expectedText = string.Join(", ", expectedArray);
            string actualText = string.Join(", ", actualArray);
            throw new InvalidOperationException($"{message}. Expected: [{expectedText}]; actual: [{actualText}].");
        }
    }
}
