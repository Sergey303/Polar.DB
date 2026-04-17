namespace Polar.DB
{
    /// <summary>
    /// Half-open range descriptor used by scale helpers to narrow candidate key positions.
    /// </summary>
    public struct Diapason
    {
        /// <summary>
        /// Zero-based start index of the candidate range.
        /// </summary>
        public long start;

        /// <summary>
        /// Number of elements in the candidate range.
        /// </summary>
        public long numb;

        /// <summary>
        /// Represents an empty range.
        /// </summary>
        public static Diapason Empty => new Diapason { numb = 0, start = long.MinValue };

        /// <summary>
        /// Returns <see langword="true"/> when the range has no elements.
        /// </summary>
        public bool IsEmpty() => numb <= 0;
    }
}
