public static class ArrayHelper
{
    public static int GrowCapacity(int currentCapacity)
    {
        if (currentCapacity == int.MaxValue)
            throw new InvalidOperationException("UKeyIndex.Build cannot materialize more than Int32.MaxValue physical records.");

        if (currentCapacity == 0) return 4;
        return currentCapacity <= int.MaxValue / 2 ? currentCapacity * 2 : int.MaxValue;
    }

    public static int GetBuildCapacityUpperBound(long count)
    {
        if (count > int.MaxValue)
            throw new InvalidOperationException("UKeyIndex.Build cannot materialize more than Int32.MaxValue physical records.");
        return count > 0 ? (int)count : 0;
    }
}