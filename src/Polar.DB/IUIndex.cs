namespace Polar.DB
{
    /// <summary>
    /// Pair of a sequence element and its physical offset in the underlying append-oriented stream.
    /// </summary>
    /// <remarks>
    /// Offsets are stream positions understood by <see cref="UniversalSequenceBase"/> and related index implementations.
    /// The value can be <see langword="null"/> for schemas that legitimately store null-like payloads.
    /// </remarks>
    public struct ObjOff
    {
        /// <summary>
        /// Element value read from the sequence for <see cref="off"/>.
        /// </summary>
        public object obj;

        /// <summary>
        /// Physical offset of the element in the sequence stream.
        /// </summary>
        public long off;

        /// <summary>
        /// Creates a value/offset pair.
        /// </summary>
        /// <param name="obj">Element value for the offset.</param>
        /// <param name="off">Physical stream offset of the element.</param>
        public ObjOff(object obj, long off)
        {
            this.obj = obj;
            this.off = off;
        }
    }

    /// <summary>
    /// Common lifecycle contract for secondary indexes attached to <see cref="USequence"/>.
    /// </summary>
    /// <remarks>
    /// Implementations typically keep both persisted static state and in-memory dynamic state for newly appended records.
    /// <see cref="OnAppendElement(object,long)"/> updates only the dynamic part; <see cref="Build"/> rebuilds persisted state.
    /// </remarks>
    public interface IUIndex
    {
        /// <summary>
        /// Removes both persisted and dynamic index state.
        /// </summary>
        void Clear();

        /// <summary>
        /// Flushes current persisted state to the underlying streams.
        /// </summary>
        void Flush();

        /// <summary>
        /// Flushes and closes index streams.
        /// </summary>
        void Close();

        /// <summary>
        /// Reloads persisted static state from index streams.
        /// </summary>
        void Refresh();

        /// <summary>
        /// Rebuilds persisted index state from the current logical contents of the owner sequence.
        /// </summary>
        void Build();

        /// <summary>
        /// Incorporates an appended sequence element into the index dynamic state.
        /// </summary>
        /// <param name="element">Appended sequence element value.</param>
        /// <param name="offset">Physical stream offset where the element was appended.</param>
        void OnAppendElement(object element, long offset);
    }
}
