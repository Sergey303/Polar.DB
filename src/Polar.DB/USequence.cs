namespace Polar.DB
{
    public class USequence
    {
        // У универсальной последовательности нет динамической части. Все элементы доступны через методы.
        // Однако элемент может быть пустым. 
        public UniversalSequenceBase sequence;
        private Func<object, bool> isEmpty;
        private Func<object, IComparable> keyFunc;
        private UKeyIndex primaryKeyIndex;
        public IUIndex[] uindexes { get; set; } = new IUIndex[0];
        private bool optimise = true;
        
        public USequence(PType tp_el, string? stateFileName, Func<Stream> streamGen, Func<object, bool> isEmpty,
            Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey, bool optimise = true)
        {
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
            Clear();

            sequence.AppendElements(flow.Where(element => !isEmpty(element)));

            Flush();

            if (stateFileName != null)
            {
                // =========== Зафиксируем состояние в файле. Запомним текущее число элементов и офсет следующего ====
                FileStream statefile = new FileStream(stateFileName, FileMode.OpenOrCreate, FileAccess.Write);
                BinaryWriter writer = new BinaryWriter(statefile);
                writer.Write(sequence.Count());
                writer.Write(sequence.ElementOffset());
                statefile.Close();
            }
        }
        internal bool IsOriginalAndNotEmpty(object element, long off) =>
            primaryKeyIndex.IsOriginal(keyFunc(element), off) && !isEmpty(element); // сначала на оригинал, потом на пустое, может можно и иначе 


        public IEnumerable<object> ElementValues()
        {
            return sequence.ElementOffsetValuePairs()
                .Where(pair => IsOriginalAndNotEmpty(pair.Item2, pair.Item1))
                .Select(pair => pair.Item2);
        }
        public void Scan(Func<long, object, bool> handler)
        {
            _ = handler ?? throw new ArgumentNullException(nameof(handler));
            sequence.Scan((off, ob) => IsOriginalAndNotEmpty(ob, off) ? handler(off, ob) : true);
        }

        internal LogicalBuildEntry[] CreateLogicalBuildSnapshot()
        {
            long count = Count;
            int capacity = count > int.MaxValue ? int.MaxValue : (int)count;
            List<LogicalBuildEntry> snapshot = new List<LogicalBuildEntry>(capacity);

            Scan((off, obj) =>
            {
                snapshot.Add(new LogicalBuildEntry(off, obj));
                return true;
            });

            return snapshot.ToArray();
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
        /// Appends a batch of elements to the dynamic tail using a sequential write path, then updates dynamic indexes.
        /// </summary>
        /// <param name="flow">Elements to append in order.</param>
        /// <remarks>
        /// This method intentionally does not advance the sidecar state file. The sidecar state describes the last
        /// persisted index synchronization point, not merely the last data byte. Call <see cref="Build"/> when the
        /// dynamic tail must be persisted into indexes and synchronized state.
        /// </remarks>
        public void AppendElements(IEnumerable<object> flow)
        {
            _ = flow ?? throw new ArgumentNullException(nameof(flow));

            sequence.AppendElements(
                flow.Where(element => !isEmpty(element)),
                (element, off) =>
                {
                    primaryKeyIndex.OnAppendElement(element, off);
                    foreach (var uind in uindexes)
                        uind.OnAppendElement(element, off);
                });
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
            return primaryKeyIndex.GetByKey(keysample);
        }

        internal object GetByOffset(long off)
        {
            object? vv = Sequence.GetElement(off);
            return vv == null || isEmpty(vv) ? null : vv;
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
            if (uindexes[nom] is SVectorIndex)
            {
                var sind = (SVectorIndex)uindexes[nom];
                IEnumerable<object> query = sind.GetAllByValue((string)value)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj)
                    //.Select(ob => ConvertNaming(ob))
                    ;
                return query;
            }

            if (uindexes[nom] is UVectorIndex uind)
            {
                var uind = (UVectorIndex)uindexes[nom];
                IEnumerable<object> query = uind.GetAllByValue((IComparable)value)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj)
                    ;
                return query;
            }

            if (uindexes[nom] is UVecIndex uvind)
            {
                var uvind = (UVecIndex)uindexes[nom];

                IEnumerable<object> query = uvind.GetAllByValue(value)
                    .Where(obof => keysFunc(obof.obj)
                        .Select(w => ignorecase ? ((string)w).ToUpper() : w)
                        .Any(W => W.CompareTo(value) == 0))
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj)
                    .ToArray();
                return query;
            }
            else throw new Exception("93394");
        }

        /// <summary>
        /// Retrieves candidates by a sample object using <see cref="UIndex"/> at the selected slot.
        /// </summary>
        /// <param name="nom">Secondary index position in <see cref="uindexes"/>.</param>
        /// <param name="osample">Sample object for value comparer.</param>
        /// <returns>Logical records that match the sample.</returns>
        public IEnumerable<object> GetAllBySample(int nom, object osample)
        {
            if (uindexes[nom] is UIndex)
            {
                var uind = (UIndex)uindexes[nom];
                IEnumerable<object> query = uind.GetAllBySample(osample)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj);
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
            var uind = uindexes[nom];
            if (uind is SVectorIndex)
            {
                var query = ((SVectorIndex)uind).GetAllByLike((string)sample)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj) // 
                    //.Select(ob => ConvertNaming(ob))
                    ;
                return query;
            }
            throw new NotImplementedException("Err: 292121");
        }

        public void Build()
        {
            sequence.Flush();

            LogicalBuildEntry[] snapshot = CreateLogicalBuildSnapshot();

            primaryKeyIndex.BuildFromSnapshot(snapshot);
            foreach (var ind in uindexes)
            {
                switch (ind)
                {
                    case UVectorIndex uVectorIndex:
                        uVectorIndex.BuildFromSnapshot(snapshot);
                        break;
                    case UVecIndex uVecIndex:
                        uVecIndex.BuildFromSnapshot(snapshot);
                        break;
                    case SVectorIndex sVectorIndex:
                        sVectorIndex.BuildFromSnapshot(snapshot);
                        break;
                    case UIndex uIndex:
                        uIndex.BuildFromSnapshot(snapshot);
                        break;
                    default:
                        ind.Build();
                        break;
                }
            }

            primaryKeyIndex.Flush();
            foreach (var ind in uindexes) ind.Flush();

            SaveState();
        }
    }
}
