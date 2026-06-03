using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Polar.DB;

namespace Polar.Universal
{
    public class USequence
    {
        // У универсальной последовательности нет динамической части. Все элементы доступны через методы.
        // Однако элемент может быть пустым. 
        private UniversalSequenceBase sequence;
        internal Func<object, bool> isEmpty;
        internal Func<object, IComparable> keyFunc;
        private UKeyIndex primaryKeyIndex;
        //internal HashSet<IComparable> changedIdSet = new HashSet<IComparable>();
        internal bool ElementChanged(IComparable key) { return primaryKeyIndex.ElementChanged(key); }
        public IUIndex[] uindexes { get; set; } = new IUIndex[0];
        private bool optimise = true;
        private string? stateFileName;

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
            FileStream statefile = new (stateFileName, FileMode.OpenOrCreate, FileAccess.Read);
            BinaryReader reader = new (statefile);
            long statenelements = reader.ReadInt64(); //old sequence.Count();
            long elementoffset = reader.ReadInt64(); // sequence.ElementOffset();
            statefile.Close();
            // А текущий размер:
            long nelements = sequence.Count();
            if (nelements > statenelements)
            {
                // State marks the boundary that is already persisted into index files.
                // Rebuild directly from the full storage sequence instead of first
                // replaying the tail into dynamic dictionaries: replay + rebuild would
                // expose the same tail record twice (dynamic + static).
                Build();
                return;
            }

            SaveState();
        }

        public void Clear() { sequence.Clear(); primaryKeyIndex.Clear(); if (uindexes != null) foreach (var ui in uindexes) ui.Clear(); SaveState(); }
        public void Flush() { sequence.Flush(); primaryKeyIndex.Flush(); if (uindexes != null) foreach (var ui in uindexes) ui.Flush(); }
        public void Close() { sequence.Close(); primaryKeyIndex.Close(); if (uindexes != null) foreach (var ui in uindexes) ui.Close(); }

        public void Refresh()
        {
            sequence.Refresh();
            primaryKeyIndex.Refresh();
            if (uindexes != null)
            {
                foreach (var ui in uindexes) ui.Refresh();
            }
        }

        public void Load(IEnumerable<object> flow)
        {
            Clear();
            foreach (var element in flow)
            {
                if (!isEmpty(element)) sequence.AppendElement(element);
            }
            Flush();
            SaveState();
        }

            if (stateFileName != null)
            {
                // =========== Зафиксируем состояние в файле. Запомним текущее число элементов и офсет следующего ====
                FileStream statefile = new (stateFileName, FileMode.OpenOrCreate, FileAccess.Write);
                BinaryWriter writer = new (statefile);
                writer.Write(sequence.Count());
                writer.Write(sequence.ElementOffset());
                statefile.Close();
            }

            foreach (var pair in pairs)
            {
                var key = keyFunc(pair.Item2);
                if (latestByKey.TryGetValue(key, out var latestOffset) &&
                    latestOffset == pair.Item1 &&
                    !isEmpty(pair.Item2))
                {
                    yield return pair;
                }
            }
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
            return handler(off, ob);
        }

        return true;
    });
}

        public long AppendElement(object element)
        {
            long off = sequence.AppendElement(element);
            primaryKeyIndex.OnAppendElement(element, off);
            if (uindexes != null) foreach (var uind in uindexes) uind.OnAppendElement(element, off);
            return off;
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
            if (uindexes[nom] is UVectorIndex uind)
            {
                return uind.GetAllByValue(value)
                    .Where(obof => IsOriginalAndNotEmpty(obof.obj, obof.off))
                    .Select(obof => obof.obj);
            }
            if (uindexes[nom] is UVecIndex uvind)
            {
                IComparable normalizedValue = ignorecase && value is string s ? s.ToUpper() : value;
                return uvind.GetAllByValue(normalizedValue)
                    .Where(obof => keysFunc(obof.obj)
                        .Select(w => ignorecase && w is string ws ? ws.ToUpper() : w)
                        .Any(w => w.CompareTo(normalizedValue) == 0))
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
            if (uind is SVectorIndex sind)
            {
                return sind.GetAllByLike((string)sample)
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
            SaveState();
        }

        public IEnumerable<object> GetAllByKey(IComparable keysample) => primaryKeyIndex.GetAllByKey(keysample);
        public IReadOnlyList<long> GetOffsetsByKey(IComparable keysample) => primaryKeyIndex.GetOffsetsByKey(keysample);
        public int CountByKey(IComparable keysample) => primaryKeyIndex.CountByKey(keysample);
        public bool TryGetExactlyOneOffsetByKey(IComparable keysample, out long offset) => primaryKeyIndex.TryGetExactlyOneOffsetByKey(keysample, out offset);
        public long GetExactlyOneOffsetByKey(IComparable keysample) => primaryKeyIndex.GetExactlyOneOffsetByKey(keysample);
        public object GetExactlyOneByKey(IComparable keysample) => primaryKeyIndex.GetExactlyOneByKey(keysample);

        private bool TryReadState(out long count, out long appendOffset)
        {
            count = 0L;
            appendOffset = 8L;
            if (stateFileName == null || !File.Exists(stateFileName)) return false;

            try
            {
                using FileStream statefile = new FileStream(stateFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (statefile.Length < 16L) return false;
                using BinaryReader reader = new BinaryReader(statefile);
                count = reader.ReadInt64();
                appendOffset = reader.ReadInt64();
                if (count < 0L || appendOffset < 8L) return false;
                return true;
            }
            catch
            {
                count = 0L;
                appendOffset = 8L;
                return false;
            }
        }

        private void SaveState()
        {
            if (stateFileName == null) return;
            string? dir = Path.GetDirectoryName(stateFileName);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using FileStream statefile = new FileStream(stateFileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using BinaryWriter writer = new BinaryWriter(statefile);
            writer.Write(sequence.Count());
            writer.Write(sequence.ElementOffset());
        }

        public long ElementOffset()
        {
            return sequence.AppendOffset;
        }

        public long Count()
        {
            return sequence.Count();
        }

        public object GetElementExactOneByExactOffset(long offset)
        {
           return sequence.GetElement(offset);
        }
    }
}
