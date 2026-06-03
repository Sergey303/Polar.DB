using Polar.DB;

namespace Polar.Universal
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
        
        internal bool ElementChanged(IComparable key) { return keyoff_dic.ContainsKey(key); }

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

        public void Refresh()
        {
            hkeys.Refresh();
            offsets.Refresh();

            if (keysinmemory)
            {
                hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
            }
            else
            {
                hkeys_arr = null;
            }
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
            {
                hkeys.AppendElement(hkey);
            }

            hkeys.Flush();
            if (!keysinmemory)
            {
                hkeys_arr = null;
                GC.Collect();
            }


            offsets.Clear();
            foreach (var off in offsets_arr)
            {
                offsets.AppendElement(off);
            }

            offsets.Flush();

            offsets_arr = null;
            GC.Collect();
        }

        public object GetByKey(IComparable keysample)
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
                while (p >= 0 && hkeys_arr[p] == hkey)
                {
                    pos = p;
                    p--;
                }

                // движемся вправо
                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    long offset = (long)offsets.GetByIndex(pos);
                    object val = sequence.GetByOffset(offset);
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
                    object val = sequence.GetByOffset(offset);
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
            long left = 0;
            long right = count;

            while (left < right)
            {
                long middle = left + (right - left) / 2;
                int middleValue = (int)hkeys.GetByIndex(middle);

                if (middleValue < hkey)
                {
                    left = middle + 1;
                }
                else
                {
                    right = middle;
                }
            }

            if (left >= count) return -1;
            return (int)hkeys.GetByIndex(left) == hkey ? left : -1;
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

        /// <summary>
        /// Возвращает ровно один актуальный элемент по ключу.
        /// Если элементов нет или их больше одного, бросает InvalidOperationException.
        /// </summary>
        public object GetExactlyOneByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));

            var offset = GetExactlyOneOffsetByKey(keysample);
            var value = sequence.GetByOffset(offset);
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one Polar.DB element for key '{keysample}', but payload at offset {offset} is null.");
            }

            var key = keyFunc(value);
            if (key.CompareTo(keysample) != 0 || !sequence.IsOriginalAndNotEmpty(value, offset))
            {
                throw new InvalidOperationException(
                    $"Expected exactly one Polar.DB element for key '{keysample}', but payload at offset {offset} did not validate.");
            }

            return value;
        }

        /// <summary>
        /// Возвращает все актуальные элементы, ключ которых равен keysample.
        /// Этот materialized API сначала получает offset-ы, затем читает payload-записи.
        /// </summary>
        public IEnumerable<object> GetAllByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));

            foreach (var offset in GetOffsetsByKey(keysample))
            {
                var value = sequence.GetByOffset(offset);
                if (value == null) continue;

                var key = keyFunc(value);
                if (key.CompareTo(keysample) == 0 && sequence.IsOriginalAndNotEmpty(value, offset))
                {
                    yield return value;
                }
            }
        }

        /// <summary>
        /// Возвращает offset-ы всех актуальных элементов, ключ которых равен keysample.
        /// Использует существующий static hash-index и проверяет настоящий ключ через payload-запись.
        /// </summary>
        public IReadOnlyList<long> GetOffsetsByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));

            // Динамическая часть UKeyIndex исторически хранит последний актуальный offset для ключа.
            // Если ключ есть в динамике, старые static offset-ы этого ключа считаются неоригинальными.
            if (keyoff_dic.TryGetValue(keysample, out long dynamicOffset))
            {
                return new[] { dynamicOffset };
            }

            return GetOffsetsByHashCompatiblePath(keysample);
        }

        /// <summary>
        /// Возвращает число актуальных элементов по ключу.
        /// </summary>
        public int CountByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));
            return GetOffsetsByKey(keysample).Count;
        }

        /// <summary>
        /// Пытается получить offset ровно одного элемента по ключу.
        /// Возвращает false, если найдено 0 или больше 1 offset-а.
        /// </summary>
        public bool TryGetExactlyOneOffsetByKey(IComparable keysample, out long offset)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));

            var offsetsByKey = GetOffsetsByKey(keysample);
            if (offsetsByKey.Count == 1)
            {
                offset = offsetsByKey[0];
                return true;
            }

            offset = default;
            return false;
        }

        /// <summary>
        /// Возвращает offset ровно одного элемента по ключу. Если найдено 0 или больше 1, бросает InvalidOperationException.
        /// </summary>
        public long GetExactlyOneOffsetByKey(IComparable keysample)
        {
            if (TryGetExactlyOneOffsetByKey(keysample, out var offset))
            {
                return offset;
            }

            var count = CountByKey(keysample);
            throw new InvalidOperationException(
                $"Expected exactly one Polar.DB element offset for key '{keysample}', but found {count}.");
        }

        private IReadOnlyList<long> GetOffsetsByHashCompatiblePath(IComparable keysample)
        {
            var result = new List<long>();
            int hkey = hashOfKey(keysample);

            if (hkeys_arr != null)
            {
                int pos = Array.BinarySearch(hkeys_arr, hkey);
                if (pos < 0) return result;

                while (pos > 0 && hkeys_arr[pos - 1] == hkey)
                {
                    pos--;
                }

                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    long offset = (long)offsets.GetByIndex(pos);

                    object val = sequence.GetByOffset(offset);
                    if (val == null) break;

                    var key = keyFunc(val);
                    if (key.CompareTo(keysample) == 0 && sequence.IsOriginalAndNotEmpty(val, offset))
                    {
                        result.Add(offset);
                    }

                    pos++;
                }

                return result;
            }

            long first = GetFirstNom(hkey);
            if (first == -1) return result;

            for (long nom = first; nom < hkeys.Count(); nom++)
            {
                int currentHash = (int)hkeys.GetByIndex(nom);
                if (currentHash != hkey) break;

                long offset = (long)offsets.GetByIndex(nom);
                object val = sequence.GetByOffset(offset);
                if (val == null) break;

                var key = keyFunc(val);
                if (key.CompareTo(keysample) == 0 && sequence.IsOriginalAndNotEmpty(val, offset))
                {
                    result.Add(offset);
                }
            }

            return result;
        }




    }
}