// UKeyIndex.cs

namespace Polar.DB
{
    /// <summary>
    /// Primary key index for <see cref="USequence"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The index consists of:
    /// </para>
    /// <list type="bullet">
    /// <item><description>a static built part persisted in <c>hkeys</c> and <c>offsets</c>;</description></item>
    /// <item><description>a dynamic in-memory map from key to the latest appended offset.</description></item>
    /// </list>
    /// <para>
    /// Traversal-time originality checks rely on the dynamic map. If a key is present there, only the element at the
    /// stored offset is treated as the current original. This is what allows appended records to shadow earlier built
    /// records with the same key.
    /// </para>
    /// <para>
    /// After reopen, callers are expected to refresh static state and, if needed, replay the dynamic tail from the
    /// sidecar state point back into this index.
    /// </para>
    /// </remarks>
    internal class UKeyIndex
    {
        private readonly USequence sequence;
        /// Ключом является объект, порождаемый ключевой функцией. Ключи можно сравнивать!
        private readonly Func<object, IComparable> keyFunc;
        private readonly Func<IComparable, int> hashOfKey;
        /// Статическая часть индекса
        private readonly UniversalSequenceBase hkeys;
        /// Статическая часть индекса
        private readonly UniversalSequenceBase offsets;

        /// <summary>
        /// Динамическая часть индекса
        /// Dynamic map: for every key seen after the last synchronized state point, store the latest offset.
        /// </summary>
        private readonly Dictionary<IComparable, long> keyoff_dic;

        private readonly bool keysinmemory;

        public UKeyIndex(
            Func<Stream> streamGen,
            USequence sequence,
            Func<object, IComparable> keyFunc,
            Func<IComparable, int> hashOfKey,
            bool keysinmemory = true)
        {
            this.sequence = sequence;
            this.keyFunc = keyFunc;
            this.hashOfKey = hashOfKey;
            this.keysinmemory = keysinmemory;

            hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());

            keyoff_dic = new Dictionary<IComparable, long>();
        }

        public void OnAppendElement(object element, long offset)
        {
            var key = keyFunc(element);
            if (keyoff_dic.ContainsKey(key))
                keyoff_dic.Remove(key);

            keyoff_dic.Add(key, offset);
        }

        /// <summary>
        /// Массив оптимизации поиска по значению хеша
        /// In-memory optimization array for static hash keys.
        /// </summary>
        private int[]? hkeys_arr = null;

        public void Clear()
        {
            hkeys.Clear();
            hkeys_arr = null;
            offsets.Clear();
            keyoff_dic.Clear();
        }

        public void Flush()
        {
            hkeys.Flush();
            offsets.Flush();
        }

        public void Close()
        {
            hkeys.Close();
            offsets.Close();
        }

        /// <summary>
        /// Reloads the static part of the primary index.
        /// </summary>
        /// <remarks>
        /// The dynamic dictionary is intentionally not cleared here. It represents appended state above the last
        /// synchronized point and is managed by the higher-level <see cref="USequence"/> lifecycle.
        /// </remarks>
        public void Refresh()
        {
            if (keysinmemory)
            {
                hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
            }
            else
            {
                hkeys_arr = null;
                hkeys.Refresh();
            }

            offsets.Refresh();
        }

        public void Build()
        {
            // сканируем опорную последовательность, формируем массивы
            List<int> hkeys_list = new List<int>();
            List<long> offsets_list = new List<long>();
            sequence.Scan((off, obj) =>
            {
                offsets_list.Add(off);
                hkeys_list.Add(hashOfKey(keyFunc(obj)));
                return true;
            });

            hkeys_arr = hkeys_list.ToArray();
            hkeys_list = null;
            long[] offsets_arr = offsets_list.ToArray();
            offsets_list = null;
            GC.Collect();

            Array.Sort(hkeys_arr, offsets_arr);

            hkeys.Clear();
            foreach (var hkey in hkeys_arr)
                hkeys.AppendElement(hkey);
            hkeys.Flush();

            if (!keysinmemory)
            {
                hkeys_arr = null;
                GC.Collect();
            }

            offsets.Clear();
            foreach (var off in offsets_arr)
                offsets.AppendElement(off);
            offsets.Flush();

            GC.Collect();
        }

        public object? GetByKey(IComparable keysample)
        {
            if (keyoff_dic.TryGetValue(keysample, out long off))
            {
                return sequence.GetByOffset(off);
            }
            int hkey = hashOfKey(keysample);

            if (hkeys_arr != null)
            {
                int pos = Array.BinarySearch<int>(hkeys_arr, hkey);
                if (pos < 0) return null;
                // ищем самую левую позицию 
                int p = pos;
                while (p >= 0 && hkeys_arr[p] == hkey) { pos = p; p--; }
                // движемся вправо
                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    long offset = (long)offsets.GetByIndex(pos);
                    object? val = sequence.GetByOffset(offset);
                    if (val == null) return null; // Непонятно, нужно ли?
                    var k = keyFunc(val);
                    if (k.CompareTo(keysample) == 0) return val;
                    pos++;
                }
                return null;
            }
            else
            {
                long first = GetFirstNom(hkey);
                if (first == -1) return null;
                for (long nom = first; nom < hkeys.Count(); nom++)
                {
                    long offset = (long)offsets.GetByIndex(nom);
                    object? val = sequence.GetByOffset(offset);
                    if (val == null) break;
                    var k = keyFunc(val);
                    if (hashOfKey(k) != hkey) break;
                    if (k.CompareTo(keysample) == 0) return val;
                }
            }
            return null;
        }

        /// <summary>
        /// Определение номера первого индекса последовательности hkeys, с которого значения РАВНЫ hkey (хешу от ключа)
        /// Если нет таких, то -1L
        /// </summary>
        /// <param name="hkey"></param>
        /// <returns></returns>
        private long GetFirstNom(int hkey)
        {
            long count = hkeys.Count();
            if (count == 0) return -1;

            long left = 0;
            long right = count;
            // интервал [left, right)
            while (left < right)
            {
                long middle = left + (right - left) / 2; // более безопасная форма  (start + end) / 2;
                int middleValue = (int)hkeys.GetByIndex(middle);

                if (middleValue < hkey)
                    // Займемся правым интервалом
                    left = middle + 1; // сам middle уже не нужно
                else
                    // Займемся левым интервалом, middle может быть подходящим номером значения, но не самым левым. 
                    right = middle;
            }

            if (left >= count) return -1;
            return (int)hkeys.GetByIndex(left) == hkey ? left : -1;
        }

        /// <summary>
        /// Метод определяет: является ли пара (key, offset) оригиналом или нет. Если такого ключа нет в дин. индексе, то это оригинал
        /// Если есть, то надо проверить офсет
        /// </summary>
        /// <param name="key"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool IsOriginal(IComparable key, long offset)
        {
            if (keyoff_dic.TryGetValue(key, out long off))
                return off == offset;

            return true; //TODO: здесь предполагается, что в основном индексе есть такое значение
        }
    }
}