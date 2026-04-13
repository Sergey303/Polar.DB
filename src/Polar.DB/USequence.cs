// USequence.cs

namespace Polar.DB
{
    /// <summary>
    /// High-level facade over <see cref="UniversalSequenceBase"/> with a primary-key index and optional secondary indexes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The class maintains two kinds of state:
    /// </para>
    /// <list type="bullet">
    /// <item><description>static built state stored in the sequence and index streams;</description></item>
    /// <item><description>dynamic tail appended after the last saved state point.</description></item>
    /// </list>
    /// <para>
    /// <see cref="Refresh"/> reloads static state and restores primary-key traversal consistency by replaying the
    /// dynamic tail into the primary index only. This is enough for methods such as <see cref="ElementValues"/>
    /// and <see cref="Scan"/> which rely on primary-key originality filtering.
    /// </para>
    /// <para>
    /// <see cref="RestoreDynamic"/> is a higher-level recovery step intended for reopened instances. It restores
    /// primary-key originality for the unsynchronized tail and then rebuilds persisted indexes before advancing the
    /// sidecar state point. This prevents repeated restart cycles from "forgetting" dynamically appended keys that were
    /// replayed only in memory.
    /// </para>
    /// <para>
    /// The two public methods must not call each other; both are built on top of private helpers in order to
    /// avoid accidental recursion and to keep responsibilities explicit.
    /// </para>
    /// </remarks>
    public class USequence
    {
        // У универсальной последовательности нет динамической части. Все элементы доступны через методы.
        // Однако элемент может быть пустым.
        private UniversalSequenceBase sequence;
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
        /// Файл для сохранения параметров состояния. Команда сохранения выполняется в конце Load()
        /// Имя файла может быть null, тогда это означает, что состояние не фиксируется и не восстанавливается
        /// Sidecar state file. When present, it stores the sequence count and append offset of the last synchronized point.
        /// </summary>
        private readonly string? stateFileName;

        /// <summary>
        /// Saves the current synchronized state into the sidecar state file.
        /// </summary>
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

        /// <summary>
        /// Reads the last synchronized state from the sidecar file.
        /// </summary>
        /// <remarks>
        /// Missing, too-short or obviously invalid state files are treated as "no synchronized state" and mapped to (0, 8).
        /// </remarks>
        private (long Count, long AppendOffset) ReadStateOrDefault()
        {
            if (stateFileName == null)
                return (0L, 8L);

            if (!File.Exists(stateFileName))
                return (0L, 8L);

            var info = new FileInfo(stateFileName);
            if (info.Length < sizeof(long) * 2)
                return (0L, 8L);

            using var statefile = new FileStream(stateFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(statefile);

            long statenelements = reader.ReadInt64();
            long elementoffset = reader.ReadInt64();

            if (statenelements < 0L || elementoffset < 8L)
                return (0L, 8L);

            return (statenelements, elementoffset);
        }

        /// <summary>
        /// Reloads static state from the underlying streams without replaying the dynamic tail.
        /// </summary>
        private void RefreshStaticState()
        {
            sequence.Refresh();
            primaryKeyIndex.Refresh();

            if (uindexes != null)
            {
                foreach (var ui in uindexes)
                    ui.Refresh();
            }
        }

        /// <summary>
        /// Replays the dynamic tail located after the last saved state point.
        /// </summary>
        /// <param name="applyToPrimary">
        /// Whether to replay the tail into the primary key index.
        /// </param>
        /// <param name="applyToSecondary">
        /// Whether to replay the tail into configured secondary indexes.
        /// </param>
        /// <param name="updateStateFile">
        /// Whether to save the current sequence count and append offset after replay.
        /// </param>
        /// <remarks>
        /// <para>
        /// This helper never calls <see cref="Refresh"/> or <see cref="RestoreDynamic"/>. The caller is responsible for
        /// preparing static state first.
        /// </para>
        /// <para>
        /// Replaying into the primary index is idempotent enough for practical use because the primary dynamic map keeps
        /// only the latest offset per key.
        /// </para>
        /// <para>
        /// Advancing the state file after an in-memory replay is only safe if the caller also makes the corresponding
        /// recovered index state durable. Otherwise future restarts can lose visibility of keys that were replayed only in memory.
        /// </para>
        /// </remarks>
        private void ReplayDynamicTailFromState(bool applyToPrimary, bool applyToSecondary, bool updateStateFile)
        {
            if (stateFileName == null)
                return;

            var state = ReadStateOrDefault();
            long synchronizedCount = state.Count;
            long synchronizedAppendOffset = state.AppendOffset;

            long currentCount = sequence.Count();
            if (currentCount <= synchronizedCount)
            {
                if (updateStateFile)
                    SaveState();
                return;
            }

            long additionalCount = currentCount - synchronizedCount;
            var additional = sequence.ElementOffsetValuePairs(synchronizedAppendOffset, additionalCount);

            foreach (var pair in additional)
            {
                if (applyToPrimary)
                    primaryKeyIndex.OnAppendElement(pair.Item2, pair.Item1);

                if (applyToSecondary && uindexes != null)
                {
                    foreach (var uind in uindexes)
                        uind.OnAppendElement(pair.Item2, pair.Item1);
                }
            }

            if (updateStateFile)
                SaveState();
        }

        /// <summary>
        /// Следующий метод актуален только если statefile != null
        /// Restores reopened-instance consistency from the last saved state point.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The previous implementation replayed the dynamic tail into in-memory indexes and then advanced the sidecar
        /// state file immediately. That made the state file claim that the recovered tail was synchronized even though
        /// the rebuilt index state had never been persisted. Across repeated restart cycles this could make earlier
        /// appended keys disappear from lookup because future recoveries replayed only the most recent suffix.
        /// </para>
        /// <para>
        /// The current implementation therefore uses a two-step strategy:
        /// </para>
        /// <list type="number">
        /// <item><description>refresh static state and replay the unsynchronized tail into the primary index to restore originality semantics;</description></item>
        /// <item><description>rebuild and persist indexes via <see cref="Build"/> before moving the sidecar state point forward.</description></item>
        /// </list>
        /// <para>
        /// This prefers correctness and restart stability over startup cost.
        /// </para>
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

            // Replay into the primary index first so Build() can preserve original/shadowing semantics
            // when it scans the sequence and persists a new synchronized index state.
            ReplayDynamicTailFromState(applyToPrimary: true, applyToSecondary: false, updateStateFile: false);

            // Build() flushes the sequence, rebuilds static indexes from the now-correct logical view,
            // and then saves the state file. This makes the new synchronized point honest across future restarts.
            Build();
        }

        public void Clear()
        {
            sequence.Clear();
            primaryKeyIndex.Clear();

            if (uindexes != null)
            {
                foreach (var ui in uindexes)
                    ui.Clear();
            }

            SaveState();
        }

        public void Flush()
        {
            sequence.Flush();
            primaryKeyIndex.Flush();

            if (uindexes != null)
            {
                foreach (var ui in uindexes)
                    ui.Flush();
            }
        }

        public void Close()
        {
            sequence.Close();
            primaryKeyIndex.Close();

            if (uindexes != null)
            {
                foreach (var ui in uindexes)
                    ui.Close();
            }
        }

        /// <summary>
        /// Reloads static state and restores primary-key traversal consistency.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When a sidecar state file is configured, this method also replays the dynamic tail into the primary index.
        /// That makes traversal methods consistent after reopen because they rely on primary-key originality filtering.
        /// </para>
        /// <para>
        /// Secondary indexes are not replayed here on purpose. Their dynamic containers currently do not expose
        /// a "reset dynamic part only" operation, so automatic replay here could duplicate dynamic entries on a live
        /// instance whose state file still points to an earlier synchronized point.
        /// </para>
        /// <para>
        /// If the caller needs full reopened-instance recovery including persisted index synchronization, use
        /// <see cref="RestoreDynamic"/> instead.
        /// </para>
        /// </remarks>
        public void Refresh()
        {
            RefreshStaticState();
            ReplayDynamicTailFromState(applyToPrimary: true, applyToSecondary: false, updateStateFile: false);
        }

        public void Load(IEnumerable<object> flow)
        {
            Clear();

            foreach (var element in flow)
            {
                if (!isEmpty(element))
                    sequence.AppendElement(element);
            }

            Flush();
            SaveState();
        }

        internal bool IsOriginalAndNotEmpty(object element, long off) =>
            primaryKeyIndex.IsOriginal(keyFunc(element), off) && !isEmpty(element);

        public IEnumerable<object> ElementValues()
        {
            return sequence.ElementOffsetValuePairs()
                .Where(pair => IsOriginalAndNotEmpty(pair.Item2, pair.Item1))
                .Select(pair => pair.Item2);
        }

        public void Scan(Func<long, object, bool> handler)
        {
            sequence.Scan((off, ob) =>
            {
                if (IsOriginalAndNotEmpty(ob, off))
                    return handler(off, ob);

                return true;
            });
        }

        public long AppendElement(object element)
        {
            long off = sequence.AppendElement(element);

            primaryKeyIndex.OnAppendElement(element, off);

            if (uindexes != null)
            {
                foreach (var uind in uindexes)
                    uind.OnAppendElement(element, off);
            }

            return off;
        }

        public void CorrectOnAppendElement(long off)
        {
            object element = sequence.GetElement(off);

            primaryKeyIndex.OnAppendElement(element, off);

            if (uindexes != null)
            {
                foreach (var uind in uindexes)
                    uind.OnAppendElement(element, off);
            }
        }

        public object GetByKey(IComparable keysample)
        {
            return primaryKeyIndex.GetByKey(keysample);
        }

        internal object? GetByOffset(long off)
        {
            return sequence.GetElement(off);
        }

        public IEnumerable<object> GetAllByValue(
            int nom,
            IComparable value,
            Func<object, IEnumerable<IComparable>> keysFunc,
            bool ignorecase = false)
        {
            if (uindexes[nom] is SVectorIndex)
            {
                var sind = (SVectorIndex)uindexes[nom];
                return sind.GetAllByValue((string)value)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj);
            }

            if (uindexes[nom] is UVectorIndex)
            {
                var uind = (UVectorIndex)uindexes[nom];
                return uind.GetAllByValue(value)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj);
            }

            if (uindexes[nom] is UVecIndex)
            {
                var uvind = (UVecIndex)uindexes[nom];

                IComparable normalizedValue = value;
                if (ignorecase && value is string s)
                    normalizedValue = s.ToUpper();

                IEnumerable<object> query = uvind.GetAllByValue(normalizedValue)
                    .Where(obof => keysFunc(obof.obj)
                        .Select(w =>
                        {
                            if (ignorecase && w is string ws)
                                return (IComparable)ws.ToUpper();

                            return w;
                        })
                        .Any(w => w.CompareTo(normalizedValue) == 0))
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .GroupBy(obof => obof.off)
                    .Select(g => g.First().obj)
                    .ToArray();

                return query;
            }

            throw new Exception("93394");
        }

        public IEnumerable<object> GetAllBySample(int nom, object osample)
        {
            if (uindexes[nom] is UIndex)
            {
                var uind = (UIndex)uindexes[nom];
                return uind.GetAllBySample(osample)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj);
            }

            throw new Exception("93394");
        }

        public IEnumerable<object> GetAllByLike(int nom, object sample)
        {
            var uind = uindexes[nom];
            if (uind is SVectorIndex)
            {
                return ((SVectorIndex)uind).GetAllByLike((string)sample)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj);
            }

            throw new NotImplementedException("Err: 292121");
        }

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
