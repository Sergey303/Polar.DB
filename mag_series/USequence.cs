using Polar.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace mag_series
{
    public class USequence
    {
        // У универсальной последовательности нет динамической части. Все элементы доступны через методы.
        // Однако элемент может быть пустым. 
        private USequenceBase sequence;
        internal Func<object, bool> isEmpty;
        internal Func<object, IComparable> keyFunc;
        internal Func<IComparable, int> hashOfKey;
        private UKeyIndex primaryKeyIndex;
        internal bool ElementChanged(IComparable key) { return primaryKeyIndex.ElementChanged(key); }
        public IUIndex[] uindexes { get; set; } = new IUIndex[0];
        //private bool optimise = true;

        public USequence(PType tp_el, string? stateFileName, Func<Stream> streamGen, Func<object, bool> isEmpty,
            Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey)
        {
            sequence = new USequenceBase(tp_el, streamGen());
            this.isEmpty = isEmpty;
            this.keyFunc = keyFunc;
            this.hashOfKey = hashOfKey;
            //this.optimise = optimise;
            this.stateFileName = stateFileName;
            primaryKeyIndex = new (streamGen, this, keyFunc, hashOfKey);
        }

        // Файл для сохранения параметров состояния. Команда сохранения выполняется в конце Load()
        // Имя файла может быть null, тогда это означает, что состояние не фиксируется и не восстанавливается
        private string? stateFileName;

        // Следующий метод актуален только если statefile != null
        public void RestoreDynamic()
        {
            FileStream statefile = new(stateFileName, FileMode.OpenOrCreate, FileAccess.Read);
            BinaryReader reader = new(statefile);
            long statenelements = reader.ReadInt64(); //old sequence.Count();
            long elementoffset = reader.ReadInt64(); // sequence.ElementOffset();
            statefile.Close();
            // А текущий размер:
            long nelements = sequence.Count();
            // Динамику надо воспроизводить только если размер увеличился
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

        public void Clear() { koh_list = new List<KOHTriple>(); sequence.Clear(); primaryKeyIndex.Clear(); if (uindexes != null) foreach (var ui in uindexes) ui.Clear(); }
        public void Flush() { sequence.Flush(); primaryKeyIndex.Flush(); if (uindexes != null) foreach (var ui in uindexes) ui.Flush(); }
        public void Close() { sequence.Close(); primaryKeyIndex.Close(); if (uindexes != null) foreach (var ui in uindexes) ui.Close(); }
        public void Refresh() { sequence.Refresh(); primaryKeyIndex.Refresh(); if (uindexes != null) foreach (var ui in uindexes) ui.Refresh(); }

        public struct KOHTriple { internal IComparable key; internal long offset; internal int hkey; };
        private List<KOHTriple> koh_list = new List<KOHTriple>();
        public void Load(IEnumerable<object> flow)
        {
            koh_list = new List<KOHTriple>();
            Clear();
            foreach (var element in flow)
            {
                long offset = sequence.AppendElement(element);
                IComparable key = keyFunc(element);
                int hkey = hashOfKey(key);
                koh_list.Add(new KOHTriple() { key = key, offset = offset, hkey = hkey });
            }
            Flush();

            if (stateFileName != null)
            {
                // =========== Зафиксируем состояние в файле. Запомним текущее число элементов и офсет следующего ====
                FileStream statefile = new(stateFileName, FileMode.OpenOrCreate, FileAccess.Write);
                BinaryWriter writer = new(statefile);
                writer.Write(sequence.Count());
                writer.Write(sequence.ElementOffset());
                statefile.Close();
            }
        }
        internal bool IsOriginalAndNotEmpty(object element, long off) =>
            primaryKeyIndex.IsOriginal(keyFunc(element), off) && !isEmpty(element); // сначала на оригинал, потом на пустое, может можно и иначе 

        public void Build()
        {
            this.primaryKeyIndex.Build(koh_list);

            foreach (var ind in uindexes) ind.Build();
        }

        public IEnumerable<object> ElementValues()
        {
            var pairs = sequence.ElementOffsetValuePairs();
            foreach (var pair in pairs)
            {
                var ioane = IsOriginalAndNotEmpty(pair.Item2, pair.Item1);
                if (ioane) yield return pair.Item2;
            }
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
                return true; // Реакция на не оригинал или пустой
            });
        }
        public void AppendElement(object element)
        {
            long off = sequence.AppendElement(element);
            // Корректировка индексов
            primaryKeyIndex.OnAppendElement(element, off);
            if (uindexes != null) foreach (var uind in uindexes) uind.OnAppendElement(element, off);
        }
        //public void CorrectOnAppendElement(long off)
        //{
        //    object element = sequence.GetElement(off);
        //    // Корректировка индексов
        //    primaryKeyIndex.OnAppendElement(element, off);
        //    if (uindexes != null) foreach (var uind in uindexes) uind.OnAppendElement(element, off);
        //}

        public object? GetByKey(IComparable keysample)
        {
            var val = primaryKeyIndex.GetByKey(keysample);
            return val == null || isEmpty(val) ? null : val;
        }

        internal object GetByOffset(long off)
        {
            return sequence.GetElement(off);
        }

        public IEnumerable<object> GetAllByValue(int nom, IComparable value,
            Func<object, IEnumerable<IComparable>> keysFunc)
        {
            throw new Exception("Unimplemented method GetAllByValue 93394");
        }
        public IEnumerable<object> GetAllBySample(int nom, object osample)
        {
            throw new Exception("Unimplemented method GetAllBySample 93362");
        }
        public IEnumerable<object> GetAllByLike(int nom, object sample)
        {
            throw new Exception("Unimplemented method GetAllByLike 93389");
        }



        public long Count()
        {
            throw new Exception("Unimplemented method Count 93303");
        }

    }

}
