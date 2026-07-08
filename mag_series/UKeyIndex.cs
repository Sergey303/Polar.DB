using Polar.DB;
using static mag_series.USequence;

namespace mag_series
{
    public class UKeyIndex
    {
        private readonly USequence sequence;

        // Ключом является объект, порождаемый ключевой функцией. Ключи можно сравнивать!
        private Func<object, IComparable> keyFunc;

        private Func<IComparable, int> hashOfKey;

        // Статическая часть индекса
        private USequenceBase hkeys;

        private USequenceBase offsets;

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

            hkeys = new USequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new USequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());

            keyoff_dic = new Dictionary<IComparable, long>();
        }

        public void OnAppendElement(object element, long offset)
        {
            var key = keyFunc(element);
            if (keyoff_dic.ContainsKey(key))
            {
                keyoff_dic.Remove(key);
                //TODO: можно и по-другому типа: keyoff_dic[key] = offset; с соответствующей коррекцией логики 
            }
            keyoff_dic.Add(key, offset);
        }

        // Массив оптимизации поиска по значению хеша
        private int[] hkeys_arr = new int[0];

        public void Clear()
        {
            hkeys.Clear();
            hkeys_arr = new int[0];
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
                hkeys_arr = new int[0];
            }
        }
        private  HashSet<int> not_single_element_hcodes = new HashSet<int>();
        public void Build(List<KOHTriple> koh_list)
        {
            // Результаты: 
            List<long> offset_res = new List<long>();
            List<int> hkey_res = new List<int>();
            not_single_element_hcodes = new HashSet<int>();

            var sorted = koh_list.OrderBy(koh => koh.hkey).ToArray();
            int hkey = int.MinValue;
            Dictionary<IComparable, KOHTriple> key_dic = new Dictionary<IComparable, KOHTriple>(); 
            foreach (var koh in sorted) 
            { 
                // Текущий h (hkey)
                int h = koh.hkey;
                IComparable k = koh.key;
                long o = koh.offset;
                // Есть два варианта - продолжение накапливания под текущим hkey и смена текущего hkey
                if (h == hkey)
                {  // Накапливаем
                    if (!not_single_element_hcodes.Contains(h)) not_single_element_hcodes.Add(h);
                    if (key_dic.TryGetValue(k, out KOHTriple triple)) // Если такой код уже есть, надо попробовать заменить триплет
                    {
                        if ( o > triple.offset) key_dic[k] = koh; 
                    }
                    else // Если кода нет - добавим
                    {
                        key_dic[k] = koh;
                    }
                }
                else
                {  // Сбрасываем накопленное и фиксируем новое
                    foreach (var pair in key_dic)
                    {
                        offset_res.Add(pair.Value.offset);
                        hkey_res.Add(pair.Value.hkey);
                    }
                    key_dic.Clear();
                    hkey = h;
                    key_dic[k] = koh;
                }
            }
            foreach (var pair in key_dic)
            {
                offset_res.Add(pair.Value.offset);
                hkey_res.Add(pair.Value.hkey);
            }

            hkeys_arr = hkey_res.ToArray();
            hkeys.Clear();
            foreach (var hk in hkeys_arr)
            {
                hkeys.AppendElement(hk);
            }
            hkeys.Flush();

            offsets.Clear();
            foreach (var os in offset_res)
            {
                offsets.AppendElement(os);
            }
            offsets.Flush();

        }


        public object? GetByKey(IComparable keysample)
        {
            if (keyoff_dic.TryGetValue(keysample, out long off))
            {
                return sequence.GetByOffset(off);
            }

            int hkey = hashOfKey(keysample);

            if (keysinmemory)
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
            var hkey = hashOfKey(key);
            if (not_single_element_hcodes.Contains(hkey)) // Если содержит, то надо проверить эти офсеты
            {
                // Ищем позицию решения
                var pos = hkeys_arr.BinarySearch(hkey);
                if (pos == -1) return false; // Вообще нет такого. Может, ошибка?
                while ( pos-1 > -1 && hkeys_arr[pos-1] == hkey ) pos --; // переход на начало цепочки
                // Цикл по элементам цепочки одинаковых hkey
                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    // Получим офсет
                    long offs = (long)offsets.GetByIndex(pos);
                    if (offs != offset) return false;
                    object mat = sequence.GetByOffset(offs);
                    IComparable k = keyFunc(mat); 
                    if (k  == key) return true;
                    pos++;
                }
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