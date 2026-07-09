using System.Linq.Expressions;
using Polar.DB;
using Polar.DB.ExternalKey;

namespace Polar.Universal
{
    public class USequence : IDisposable
    {
        private UniversalSequenceBase sequence;
        internal Func<object, bool> isEmpty;
        private UKeyIndex primaryKeyIndex;
        private IPrimaryKeyAccessor? _primaryKeyAccessor;
        internal bool ElementChanged(IComparable key) { return primaryKeyIndex.ElementChanged(key); }
        public IUIndex[] uindexes { get; set; } = Array.Empty<IUIndex>();
        private bool optimise = true;
        private string? stateFileName;
        private BuildEntry[]? loadedPrimaryBuildEntries;
        private bool disposed;

        public USequence(PType tp_el, string? stateFileName, Func<Stream> streamGen,
            Func<object, bool> isEmpty, bool optimise = true)
        {
            sequence = new UniversalSequenceBase(tp_el, streamGen());
            this.isEmpty = isEmpty;
            this.optimise = optimise;
            this.stateFileName = stateFileName;

            primaryKeyIndex = new UKeyIndex(streamGen, this, optimise);
        }

        [Obsolete("Use the USequence constructor without primary-key delegates and call SetPrimaryKey<TKey>.")]
        public USequence(PType tp_el, string? stateFileName, Func<Stream> streamGen, Func<object, bool> isEmpty,
            Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey, bool optimise = true)
            : this(tp_el, stateFileName, streamGen, isEmpty, optimise)
        {
            ConfigurePrimaryKey(new DelegatePrimaryKeyAccessor(keyFunc, hashOfKey));
        }

        public void SetPrimaryKey<TKey>(
            Expression<Func<object, TKey>> keyExpression,
            Func<TKey, int>? hashOfKey = null)
            where TKey : IComparable, IComparable<TKey>, IEquatable<TKey>
        {
            if (_primaryKeyAccessor != null)
                throw new InvalidOperationException("Primary key is already configured for this sequence.");

            ConfigurePrimaryKey(new TypedPrimaryKeyAccessor<TKey>(keyExpression, hashOfKey));
        }

        private IPrimaryKeyAccessor PrimaryKeyAccessor =>
            _primaryKeyAccessor ?? throw new InvalidOperationException(
                "Primary key is not configured. Call SetPrimaryKey before using primary-key operations.");

        internal IComparable GetPrimaryKey(object value) => PrimaryKeyAccessor.GetKey(value);

        private void ConfigurePrimaryKey(IPrimaryKeyAccessor accessor)
        {
            _primaryKeyAccessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            primaryKeyIndex.SetPrimaryKeyAccessor(accessor);
        }

        public void RestoreDynamic()
        {
            RefreshCore(persistRecoveredIndexes: true);
        }

        public void Clear()
        {
            sequence.Clear();
            primaryKeyIndex.Clear();
            loadedPrimaryBuildEntries = null;
            if (uindexes != null) foreach (var ui in uindexes) ui.Clear();
        }

        public void Flush()
        {
            sequence.Flush();
            primaryKeyIndex.Flush();
            if (uindexes != null) foreach (var ui in uindexes) ui.Flush();
        }

        public void Close()
        {
            if (disposed) return;
            sequence.Close();
            primaryKeyIndex.Close();
            if (uindexes != null) foreach (var ui in uindexes) ui.Close();
            disposed = true;
        }

        public void Refresh()
        {
            sequence.Flush();
            RefreshCore(persistRecoveredIndexes: false);
        }

        private void RefreshCore(bool persistRecoveredIndexes)
        {
            EnsurePrimaryKeyConfigured();
            sequence.Refresh();
            primaryKeyIndex.Refresh();
            loadedPrimaryBuildEntries = null;
            if (uindexes != null) foreach (var ui in uindexes) ui.Refresh();

            if (persistRecoveredIndexes)
            {
                Build();
                return;
            }

            if (!TryReplayDynamicTailFromState())
                Build();
        }

        private bool TryReplayDynamicTailFromState()
        {
            if (!TryReadState(out long stateCount, out long stateAppendOffset))
                return false;

            long currentCount = sequence.Count();
            if (stateCount == currentCount)
                return stateAppendOffset == sequence.ElementOffset();

            try
            {
                long replayCount = currentCount - stateCount;
                long replayed = 0L;
                foreach (var pair in sequence.ElementOffsetValuePairs(stateAppendOffset, replayCount))
                {
                    primaryKeyIndex.OnAppendElement(pair.Item2, pair.Item1);
                    if (uindexes != null) foreach (var uind in uindexes) uind.OnAppendElement(pair.Item2, pair.Item1);
                    replayed++;
                }

                return replayed == replayCount && sequence.Media.Position == sequence.ElementOffset();
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private bool TryReadState(out long stateCount, out long stateAppendOffset)
        {
            stateCount = 0L;
            stateAppendOffset = 8L;

            if (stateFileName == null || !File.Exists(stateFileName))
                return false;

            try
            {
                using var statefile = new FileStream(
                    stateFileName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                if (statefile.Length < sizeof(long) * 2)
                    return false;

                using var reader = new BinaryReader(statefile);
                stateCount = reader.ReadInt64();
                stateAppendOffset = reader.ReadInt64();
            }
            catch (IOException)
            {
                return false;
            }

            long currentCount = sequence.Count();
            long logicalTail = sequence.ElementOffset();

            if (stateCount < 0L || stateCount > currentCount)
                return false;
            if (stateAppendOffset < 8L || stateAppendOffset > logicalTail)
                return false;
            if (stateCount == 0L && stateAppendOffset != 8L)
                return false;
            if (stateCount == currentCount && stateAppendOffset != logicalTail)
                return false;
            if (stateCount < currentCount && stateAppendOffset >= logicalTail)
                return false;

            return true;
        }

        public void Load(IEnumerable<object> flow)
        {
            EnsurePrimaryKeyConfigured();
            Clear();
            var loadedEntries = flow is ICollection<object> collection
                ? new List<BuildEntry>(collection.Count)
                : new List<BuildEntry>();

            var primaryKey = PrimaryKeyAccessor;
            foreach (var element in flow)
            {
                if (isEmpty(element)) continue;

                var offset = sequence.AppendElement(element);
                var key = primaryKey.GetKey(element);
                loadedEntries.Add(new BuildEntry(primaryKey.Hash(key), key, offset, isEmpty: false));
            }

            loadedPrimaryBuildEntries = loadedEntries.Count == 0
                ? Array.Empty<BuildEntry>()
                : loadedEntries.ToArray();

            Flush();
            SaveState();
        }

        private void SaveState()
        {
            if (stateFileName == null) return;

            using var statefile = new FileStream(
                stateFileName,
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite);
            using var writer = new BinaryWriter(statefile);
            writer.Write(sequence.Count());
            writer.Write(sequence.ElementOffset());
            writer.Flush();
        }

        private void EnsurePrimaryKeyConfigured()
        {
            _ = PrimaryKeyAccessor;
        }

        internal bool IsEmpty(object element) => isEmpty(element);

        internal void ScanPhysical(Func<long, object, bool> handler)
        {
            sequence.Scan(handler);
        }

        internal bool IsOriginalAndNotEmpty(object element, long off)
        {
            EnsurePrimaryKeyConfigured();
            return primaryKeyIndex.IsOriginal(GetPrimaryKey(element), off) && !isEmpty(element);
        }

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
                {
                    bool ok = handler(off, ob);
                    return ok;
                }
                return true;
            });
        }

        public void AppendElement(object element)
        {
            EnsurePrimaryKeyConfigured();
            loadedPrimaryBuildEntries = null;
            long off = sequence.AppendElement(element);
            primaryKeyIndex.OnAppendElement(element, off);
            if (uindexes != null) foreach (var uind in uindexes) uind.OnAppendElement(element, off);
        }

        public void CorrectOnAppendElement(long off)
        {
            EnsurePrimaryKeyConfigured();
            loadedPrimaryBuildEntries = null;
            object element = sequence.GetElement(off);
            primaryKeyIndex.OnAppendElement(element, off);
            if (uindexes != null) foreach (var uind in uindexes) uind.OnAppendElement(element, off);
        }

        public object GetByKey(IComparable keysample)
        {
            EnsurePrimaryKeyConfigured();
            return primaryKeyIndex.GetByKey(keysample);
        }

        internal object GetByOffset(long off)
        {
            var position = sequence.Media.Position;
            try
            {
                return sequence.GetElement(off);
            }
            finally
            {
                sequence.Media.Position = Math.Min(position, sequence.Media.Length);
            }
        }

        public IEnumerable<object> GetAllByValue(int nom, IComparable value,
            Func<object, IEnumerable<IComparable>> keysFunc, bool ignorecase = false)
        {
            if (uindexes[nom] is SVectorIndex sind)
            {
                return sind.GetAllByValue((string)value)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj);
            }
            if (uindexes[nom] is IExternalKeyIndex external) return external.GetManyByValue(value);
            if (uindexes[nom] is UVectorIndex uind)
            {
                return uind.GetAllByValue(value)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj);
            }
            if (uindexes[nom] is UVecIndex uvind)
            {
                return uvind.GetAllByValue(value)
                    .Where(obof => keysFunc(obof.obj)
                        .Select(w => ignorecase ? ((string)w).ToUpper() : w)
                        .Any(w => w.CompareTo(value) == 0))
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj)
                    .ToArray();
            }
            throw new Exception("93394");
        }

        public IEnumerable<object> GetAllBySample(int nom, object osample)
        {
            if (uindexes[nom] is UIndex uind)
            {
                return uind.GetAllBySample(osample)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj);
            }
            throw new Exception("93394");
        }

        public IEnumerable<object> GetAllByLike(int nom, object sample)
        {
            var uind = uindexes[nom];
            if (uind is SVectorIndex sVectorIndex)
            {
                return sVectorIndex.GetAllByLike((string)sample)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj);
            }
            throw new NotImplementedException("Err: 292121");
        }

        public void Build()
        {
            EnsurePrimaryKeyConfigured();
            sequence.Flush();

            var loadedEntries = loadedPrimaryBuildEntries;
            if (loadedEntries != null)
            {
                primaryKeyIndex.BuildFromLoadedEntries(loadedEntries, loadedEntries.Length);
                loadedPrimaryBuildEntries = null;
            }
            else
            {
                primaryKeyIndex.Build();
            }

            foreach (var ind in uindexes) ind.Build();

            primaryKeyIndex.Flush();
            foreach (var ind in uindexes) ind.Flush();
            SaveState();
        }

        public UIndexBuildProfile LastPrimaryBuildProfile => primaryKeyIndex.LastBuildProfile;

        public long Count() => sequence.Count();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || disposed) return;
            Flush();
            sequence.Dispose();
            primaryKeyIndex.Dispose();
            if (uindexes != null) foreach (var ui in uindexes) ui.Dispose();
            disposed = true;
        }
    }
}
