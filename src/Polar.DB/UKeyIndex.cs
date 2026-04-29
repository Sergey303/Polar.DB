namespace Polar.DB
{
    public class UKeyIndex
    {
        private readonly USequence sequence;
        // Ключом является объект, порождаемый ключевой функцией. Ключи можно сравнивать!
        private Func<object, IComparable> keyFunc;
        private Func<IComparable, int> hashOfKey;
        // Статическая часть индекса
        private UniversalSequenceBase hkeys;
        private UniversalSequenceBase offsets;
        // Динамическая часть индекса
        private Dictionary<IComparable, long> keyoff_dic;
        private bool keysinmemory;

        public UKeyIndex(Func<Stream> streamGen, USequence sequence,
            Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey, bool keysinmemory = true)
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
            {
                keyoff_dic.Remove(key);
            }
            keyoff_dic.Add(key, offset);
        }

        // Массив оптимизации поиска по значению хеша
        private int[] hkeys_arr = null;

        public void Clear() { hkeys.Clear(); hkeys_arr = null; offsets.Clear(); keyoff_dic.Clear(); }
        public void Flush() { hkeys.Flush(); offsets.Flush();  }
        public void Close() { hkeys.Close(); offsets.Close();  }
        public void Refresh() 
        {
            if (keysinmemory) hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
            else hkeys.Refresh();
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
            foreach (var hkey in hkeys_arr) { hkeys.AppendElement(hkey); }
            hkeys.Flush();
            if (!keysinmemory)
            {
                hkeys_arr = null;
                GC.Collect();
            }


            offsets.Clear();
            foreach (var off in offsets_arr) { offsets.AppendElement(off); }
            offsets.Flush();
            offsets_arr = null;
            GC.Collect();
        }

        public object GetByKey(IComparable keysample)
        {
            foreach (var value in GetAllByKey(keysample))
            {
                return value;
            }

            return null;
        }

        /// <summary>
        /// Возвращает все актуальные элементы, ключ которых равен keysample.
        /// Метод идёт от первой позиции с совпавшим hash-key, поэтому корректно работает
        /// для групп дублей и для сценариев range traversal после binary search.
        /// </summary>
        public IEnumerable<object> GetAllByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));

            if (keyoff_dic.TryGetValue(keysample, out long dynamicOffset))
            {
                object dynamicValue = sequence.GetByOffset(dynamicOffset);
                if (dynamicValue != null)
                {
                    var dynamicKey = keyFunc(dynamicValue);
                    if (dynamicKey.CompareTo(keysample) == 0 && sequence.IsOriginalAndNotEmpty(dynamicValue, dynamicOffset))
                    {
                        yield return dynamicValue;
                    }
                }
            }

            int hkey = hashOfKey(keysample);

            if (hkeys_arr != null)
            {
                int pos = GetFirstInMemoryNom(hkey);
                if (pos < 0) yield break;

                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    long offset = (long)offsets.GetByIndex(pos);
                    object val = sequence.GetByOffset(offset);
                    if (val == null) yield break;

                    var key = keyFunc(val);
                    if (key.CompareTo(keysample) == 0 && sequence.IsOriginalAndNotEmpty(val, offset))
                    {
                        yield return val;
                    }

                    pos++;
                }

                yield break;
            }

            long first = GetFirstNom(hkey);
            if (first == -1) yield break;

            for (long nom = first; nom < hkeys.Count(); nom++)
            {
                int currentHash = (int)hkeys.GetByIndex(nom);
                if (currentHash != hkey) yield break;

                long offset = (long)offsets.GetByIndex(nom);
                object val = sequence.GetByOffset(offset);
                if (val == null) yield break;

                var key = keyFunc(val);
                if (key.CompareTo(keysample) == 0 && sequence.IsOriginalAndNotEmpty(val, offset))
                {
                    yield return val;
                }
            }
        }

        /// <summary>
        /// Возвращает ровно один актуальный элемент по ключу.
        /// Если элементов нет или их больше одного, бросает InvalidOperationException.
        /// Это намеренно отдельный контракт для таблиц/индексов, где бизнес-инвариант гарантирует уникальность.
        /// </summary>
        public object GetExactlyOneByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));

            object single = null;
            var count = 0;

            foreach (var value in GetAllByKey(keysample))
            {
                count++;
                if (count == 1)
                {
                    single = value;
                    continue;
                }

                throw new InvalidOperationException(
                    $"Expected exactly one Polar.DB element for key '{keysample}', but found more than one.");
            }

            if (count == 0)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one Polar.DB element for key '{keysample}', but found none.");
            }

            return single;
        }

        private int GetFirstInMemoryNom(int hkey)
        {
            int start = 0;
            int end = hkeys_arr.Length;

            while (start < end)
            {
                int middle = start + ((end - start) / 2);
                if (hkeys_arr[middle] < hkey)
                {
                    start = middle + 1;
                }
                else
                {
                    end = middle;
                }
            }

            return start < hkeys_arr.Length && hkeys_arr[start] == hkey ? start : -1;
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
            long start = 0;
            long end = count;

            while (start < end)
            {
                long middle = start + ((end - start) / 2);
                int middleValue = (int)hkeys.GetByIndex(middle);
                if (middleValue < hkey)
                {
                    start = middle + 1;
                }
                else
                {
                    end = middle;
                }
            }

            return start < count && (int)hkeys.GetByIndex(start) == hkey ? start : -1L;
        }

        /// <summary>
        /// Определяет является ли пара (key, offset) оригиналом или нет. Если такого ключа нет в дин. индексе, то это оригинал
        /// Если есть, то надо проверить офсет
        /// </summary>
        /// <param name="key"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool IsOriginal(IComparable key, long offset)
        {
            if (keyoff_dic.TryGetValue(key, out long off))
            {
                if (off == offset) return true;
                return false;
            }
            return true; //TODO: здесь предполагается, что в основном индексе есть такое значение
        }

    }

}
