namespace Polar.DB
{
    /// <summary>
    /// High-level facade over <see cref="UniversalSequenceBase"/> with primary-key semantics and optional secondary indexes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sequence has persisted static state and an append-only dynamic tail. The static part is loaded from streams;
    /// the dynamic part is reconstructed from offsets after the sidecar state point when needed.
    /// </para>
    /// <para>
    /// <see cref="Refresh"/> restores traversal consistency by replaying only primary-key dynamic state.
    /// <see cref="RestoreDynamic"/> is a stronger recovery operation that rebuilds persisted indexes before advancing
    /// sidecar state, preventing restart-time key visibility drift.
    /// </para>
    /// </remarks>
    public class USequence
    {
        private readonly UniversalSequenceBase sequence;
        private readonly Func<object, bool> isEmpty;
        private readonly Func<object, IComparable> keyFunc;
        private readonly UKeyIndex primaryKeyIndex;
        private readonly bool optimise;

        /// <summary>
        /// Gets or sets secondary indexes attached to this sequence.
        /// </summary>
        /// <remarks>
        /// Index order is used by lookup helpers that accept an integer index parameter.
        /// </remarks>
        public IUIndex[] uindexes { get; set; } = Array.Empty<IUIndex>();

        /// <summary>
        /// Creates a keyed append-oriented sequence.
        /// </summary>
        /// <param name="tp_el">Element schema descriptor.</param>
        /// <param name="stateFileName">Optional sidecar state file path; <see langword="null"/> disables sidecar state.</param>
        /// <param name="streamGen">Factory for streams used by sequence and indexes.</param>
        /// <param name="isEmpty">Predicate identifying logically empty/tombstone elements.</param>
        /// <param name="keyFunc">Primary-key extractor.</param>
        /// <param name="hashOfKey">Hash selector for primary-key index bucketing.</param>
        /// <param name="optimise">Whether primary index should cache hash keys in memory.</param>
        public USequence(
            PType tp_el,
            string? stateFileName,
            Func<Stream> streamGen,
            Func<object, bool> isEmpty,
            Func<object, IComparable> keyFunc,
            Func<IComparable, int> hashOfKey,
            bool optimise = true)
        {
            _ = tp_el ?? throw new ArgumentNullException(nameof(tp_el));
            _ = streamGen ?? throw new ArgumentNullException(nameof(streamGen));
            _ = isEmpty ?? throw new ArgumentNullException(nameof(isEmpty));
            _ = keyFunc ?? throw new ArgumentNullException(nameof(keyFunc));
            _ = hashOfKey ?? throw new ArgumentNullException(nameof(hashOfKey));
            sequence = new UniversalSequenceBase(tp_el, streamGen());
            this.isEmpty = isEmpty;
            this.keyFunc = keyFunc;
            this.optimise = optimise;
            this.stateFileName = stateFileName;
            primaryKeyIndex = new UKeyIndex(streamGen, this, keyFunc, hashOfKey, optimise);
        }

        /// <summary>
        /// Optional sidecar state file storing the last synchronized count and append offset.
        /// </summary>
        private readonly string? stateFileName;

        private void SaveState()
        {
            if (stateFileName == null) return;

            string? dir = Path.GetDirectoryName(stateFileName);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var statefile = new FileStream(stateFileName, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var writer = new BinaryWriter(statefile);
            writer.Write(sequence.Count());
            writer.Write(sequence.AppendOffset);
        }

        private (long Count, long AppendOffset) ReadStateOrDefault()
        {
            if (stateFileName == null) return (0L, 8L);
            if (!File.Exists(stateFileName)) return (0L, 8L);

            var info = new FileInfo(stateFileName);
            if (info.Length < sizeof(long) * 2) return (0L, 8L);

            using var statefile = new FileStream(stateFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(statefile);

            long statenelements = reader.ReadInt64();
            long elementoffset = reader.ReadInt64();
            if (statenelements < 0L || elementoffset < 8L) return (0L, 8L);

            return (statenelements, elementoffset);
        }

        private void RefreshStaticState()
        {
            sequence.Refresh();
            primaryKeyIndex.Refresh();
            foreach (var ui in uindexes) ui.Refresh();
        }

        private void ReplayDynamicTailFromState(bool applyToPrimary, bool applyToSecondary, bool updateStateFile)
        {
            if (stateFileName == null) return;

            var state = ReadStateOrDefault();
            long synchronizedCount = state.Count;
            long synchronizedAppendOffset = state.AppendOffset;

            long currentCount = sequence.Count();
            if (currentCount <= synchronizedCount)
            {
                if (updateStateFile) SaveState();
                return;
            }

            long additionalCount = currentCount - synchronizedCount;
            var additional = sequence.ElementOffsetValuePairs(synchronizedAppendOffset, additionalCount);
            foreach (var pair in additional)
            {
                if (applyToPrimary)
                    primaryKeyIndex.OnAppendElement(pair.Item2, pair.Item1);

                if (applyToSecondary)
                {
                    foreach (var uind in uindexes)
                        uind.OnAppendElement(pair.Item2, pair.Item1);
                }
            }

            if (updateStateFile) SaveState();
        }

        /// <summary>
        /// Restores reopened-instance consistency from sidecar state.
        /// </summary>
        /// <remarks>
        /// The method refreshes static state, replays unsynchronized primary-key dynamics, rebuilds persisted indexes and
        /// only then advances sidecar state. This keeps restart behavior stable across repeated crashes/restarts.
        /// </remarks>
        public void RestoreDynamic()
        {
            if (stateFileName == null)
            {
                RefreshStaticState();
                return;
            }

            RefreshStaticState();

            var state = ReadStateOrDefault();
            bool hasUnsynchronizedTail = sequence.Count() > state.Count;
            if (!hasUnsynchronizedTail)
            {
                SaveState();
                return;
            }

            ReplayDynamicTailFromState(applyToPrimary: true, applyToSecondary: false, updateStateFile: false);
            Build();
        }

        /// <summary>
        /// Clears sequence storage, primary index and all secondary indexes.
        /// </summary>
        public void Clear()
        {
            sequence.Clear();
            primaryKeyIndex.Clear();
            foreach (var ui in uindexes) ui.Clear();
            SaveState();
        }

        /// <summary>
        /// Flushes sequence and index streams.
        /// </summary>
        public void Flush()
        {
            sequence.Flush();
            primaryKeyIndex.Flush();
            foreach (var ui in uindexes) ui.Flush();
        }

        /// <summary>
        /// Flushes and closes sequence and index streams.
        /// </summary>
        public void Close()
        {
            sequence.Close();
            primaryKeyIndex.Close();
            foreach (var ui in uindexes) ui.Close();
        }

        /// <summary>
        /// Reloads static state and replays unsynchronized dynamic tail into the primary-key index.
        /// </summary>
        /// <remarks>
        /// Secondary index dynamic replay is intentionally omitted here to avoid duplicate in-memory entries.
        /// Use <see cref="RestoreDynamic"/> when full recovery plus persisted synchronization is required.
        /// </remarks>
        public void Refresh()
        {
            RefreshStaticState();
            ReplayDynamicTailFromState(applyToPrimary: true, applyToSecondary: false, updateStateFile: false);
        }

        /// <summary>
        /// Replaces sequence contents with provided flow while skipping logically empty elements.
        /// </summary>
        /// <param name="flow">Elements to append in order.</param>
        public void Load(IEnumerable<object> flow)
        {
            _ = flow ?? throw new ArgumentNullException(nameof(flow));
            Clear();

            foreach (var element in flow)
            {
                if (!isEmpty(element))
                    sequence.AppendElement(element);
            }

            Flush();
            SaveState();
        }

        internal bool IsOriginalAndNotEmpty(object element, long off)
        {
            _ = element ?? throw new ArgumentNullException(nameof(element));
            return primaryKeyIndex.IsOriginal(keyFunc(element), off) && !isEmpty(element);
        }

        /// <summary>
        /// Enumerates current logical elements where each key contributes only its latest non-empty record.
        /// </summary>
        /// <returns>Logical view of sequence elements after primary-key originality filtering.</returns>
        public IEnumerable<object> ElementValues()
        {
            return sequence.ElementOffsetValuePairs()
                .Where(pair => IsOriginalAndNotEmpty(pair.Item2, pair.Item1))
                .Select(pair => pair.Item2);
        }

        /// <summary>
        /// Scans logical elements in physical order and invokes a callback for each one.
        /// </summary>
        /// <param name="handler">Callback returning <see langword="true"/> to continue or <see langword="false"/> to stop.</param>
        public void Scan(Func<long, object, bool> handler)
        {
            _ = handler ?? throw new ArgumentNullException(nameof(handler));
            sequence.Scan((off, ob) => IsOriginalAndNotEmpty(ob, off) ? handler(off, ob) : true);
        }

        /// <summary>
        /// Appends one element to sequence and updates primary/secondary dynamic indexes.
        /// </summary>
        /// <param name="element">Element to append.</param>
        /// <returns>Physical stream offset where the element was written.</returns>
        public long AppendElement(object element)
        {
            _ = element ?? throw new ArgumentNullException(nameof(element));
            long off = sequence.AppendElement(element);
            primaryKeyIndex.OnAppendElement(element, off);
            foreach (var uind in uindexes) uind.OnAppendElement(element, off);
            return off;
        }

        /// <summary>
        /// Replays one already-written element into dynamic index state.
        /// </summary>
        /// <param name="off">Physical stream offset of the element to replay.</param>
        public void CorrectOnAppendElement(long off)
        {
            object element = sequence.GetElement(off);
            primaryKeyIndex.OnAppendElement(element, off);
            foreach (var uind in uindexes) uind.OnAppendElement(element, off);
        }

        /// <summary>
        /// Gets the latest logical record for a primary key sample.
        /// </summary>
        /// <param name="keysample">Primary key sample.</param>
        /// <returns>Matching logical record, or <see langword="null"/> when no match exists.</returns>
        public object GetByKey(IComparable keysample)
        {
            _ = keysample ?? throw new ArgumentNullException(nameof(keysample));
            return primaryKeyIndex.GetByKey(keysample)!;
        }

        internal object? GetByOffset(long off)
        {
            return sequence.GetElement(off);
        }

        /// <summary>
        /// Retrieves candidates by one secondary index and returns logical non-empty originals.
        /// </summary>
        /// <param name="nom">Secondary index position in <see cref="uindexes"/>.</param>
        /// <param name="value">Lookup sample.</param>
        /// <param name="keysFunc">Exact key extractor used to filter hash-only indexes.</param>
        /// <param name="ignorecase">Whether to normalize string comparison to uppercase.</param>
        /// <returns>Logical records that match the sample for the selected secondary index.</returns>
        public IEnumerable<object> GetAllByValue(
            int nom,
            IComparable value,
            Func<object, IEnumerable<IComparable>> keysFunc,
            bool ignorecase = false)
        {
            _ = value ?? throw new ArgumentNullException(nameof(value));
            _ = keysFunc ?? throw new ArgumentNullException(nameof(keysFunc));
            if (uindexes[nom] is SVectorIndex sind)
            {
                return sind.GetAllByValue((string)value)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj!, obof.off))
                    .Select(obof => obof.obj!);
            }

            if (uindexes[nom] is UVectorIndex uind)
            {
                return uind.GetAllByValue(value)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj!, obof.off))
                    .Select(obof => obof.obj!);
            }

            if (uindexes[nom] is UVecIndex uvind)
            {
                IComparable normalizedValue = value;
                if (ignorecase && value is string s)
                    normalizedValue = s.ToUpper();

                IEnumerable<object> query = uvind.GetAllByValue(normalizedValue)
                    .Where(obof => keysFunc(obof.obj!)
                        .Select(w => ignorecase && w is string ws ? (IComparable)ws.ToUpper() : w)
                        .Any(w => w.CompareTo(normalizedValue) == 0))
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj!, obof.off))
                    .GroupBy(obof => obof.off)
                    .Select(g => g.First().obj!)
                    .ToArray();

                return query;
            }

            throw new Exception("93394");
        }

        /// <summary>
        /// Retrieves candidates by a sample object using <see cref="UIndex"/> at the selected slot.
        /// </summary>
        /// <param name="nom">Secondary index position in <see cref="uindexes"/>.</param>
        /// <param name="osample">Sample object for value comparer.</param>
        /// <returns>Logical records that match the sample.</returns>
        public IEnumerable<object> GetAllBySample(int nom, object osample)
        {
            _ = osample ?? throw new ArgumentNullException(nameof(osample));
            if (uindexes[nom] is UIndex uind)
            {
                return uind.GetAllBySample(osample)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj!, obof.off))
                    .Select(obof => obof.obj!);
            }

            throw new Exception("93394");
        }

        /// <summary>
        /// Retrieves string-prefix matches using <see cref="SVectorIndex"/> at the selected slot.
        /// </summary>
        /// <param name="nom">Secondary index position in <see cref="uindexes"/>.</param>
        /// <param name="sample">Prefix sample string.</param>
        /// <returns>Logical records that match the prefix.</returns>
        public IEnumerable<object> GetAllByLike(int nom, object sample)
        {
            _ = sample ?? throw new ArgumentNullException(nameof(sample));
            var uind = uindexes[nom];
            if (uind is SVectorIndex sindex)
            {
                return sindex.GetAllByLike((string)sample)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj!, obof.off))
                    .Select(obof => obof.obj!);
            }

            throw new NotImplementedException("Err: 292121");
        }

        /// <summary>
        /// Flushes sequence, rebuilds all indexes from current logical data and persists synchronization state.
        /// </summary>
        public void Build()
        {
            sequence.Flush();

            primaryKeyIndex.Build();
            foreach (var ind in uindexes) ind.Build();

            primaryKeyIndex.Flush();
            foreach (var ind in uindexes) ind.Flush();

            SaveState();
        }
    }
}
