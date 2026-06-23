using Polar.DB;
using Polar.DB.ExternalKey;

namespace Polar.Universal
{
    public class USequence: IDisposable
    {
        private UniversalSequenceBase sequence;
        internal Func<object, bool> isEmpty;
        internal Func<object, IComparable> keyFunc;
        private UKeyIndex primaryKeyIndex;
        internal bool ElementChanged(IComparable key) { return primaryKeyIndex.ElementChanged(key); }
        public IUIndex[] uindexes { get; set; } = new IUIndex[0];
        private bool optimise = true;
        private string? stateFileName;
        private bool disposed;

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

        public void RestoreDynamic()
        {
            FileStream statefile = new(stateFileName, FileMode.OpenOrCreate, FileAccess.Read);
            BinaryReader reader = new(statefile);
            long statenelements = reader.ReadInt64();
            long elementoffset = reader.ReadInt64();
            statefile.Close();
            long nelements = sequence.Count();
            Console.WriteLine($"{nelements - statenelements} elements added");
            if (nelements > statenelements)
            {
                var additional = sequence.ElementOffsetValuePairs(elementoffset, nelements - statenelements);
                foreach (var pair in additional)
                {
                    primaryKeyIndex.OnAppendElement(pair.Item2, pair.Item1);
                    if (uindexes != null) foreach (var uind in uindexes) uind.OnAppendElement(pair.Item2, pair.Item1);
                }
            }
        }

        public void Clear() { sequence.Clear(); primaryKeyIndex.Clear(); if (uindexes != null) foreach (var ui in uindexes) ui.Clear(); }
        public void Flush() { sequence.Flush(); primaryKeyIndex.Flush(); if (uindexes != null) foreach (var ui in uindexes) ui.Flush(); }
        public void Close()
        {
            Dispose();
        }
        public void Refresh() { sequence.Refresh(); primaryKeyIndex.Refresh(); if (uindexes != null) foreach (var ui in uindexes) ui.Refresh(); }

        public void Load(IEnumerable<object> flow)
        {
            Clear();
            foreach (var element in flow)
            {
                if (!isEmpty(element)) sequence.AppendElement(element);
            }
            Flush();

            if (stateFileName != null)
            {
                FileStream statefile = new(stateFileName, FileMode.OpenOrCreate, FileAccess.Write);
                BinaryWriter writer = new(statefile);
                writer.Write(sequence.Count());
                writer.Write(sequence.ElementOffset());
                statefile.Close();
            }
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
                {
                    bool ok = handler(off, ob);
                    return ok;
                }
                return true;
            });
        }

        public void AppendElement(object element)
        {
            long off = sequence.AppendElement(element);
            primaryKeyIndex.OnAppendElement(element, off);
            if (uindexes != null) foreach (var uind in uindexes) uind.OnAppendElement(element, off);
        }

        public void CorrectOnAppendElement(long off)
        {
            object element = sequence.GetElement(off);
            primaryKeyIndex.OnAppendElement(element, off);
            if (uindexes != null) foreach (var uind in uindexes) uind.OnAppendElement(element, off);
        }

        public object GetByKey(IComparable keysample) => primaryKeyIndex.GetByKey(keysample);

        internal object GetByOffset(long off) => sequence.GetElement(off);

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
                        .Any(W => W.CompareTo(value) == 0))
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
            primaryKeyIndex.Build();
            foreach (var ind in uindexes) ind.Build();
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
