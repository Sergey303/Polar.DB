using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private bool keysinmemory;
        private long[]? typed_offsets_arr = null;
        private long[]? hkey_offsets_arr = null;

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
            ClearInMemoryIndexes();
            hkeys.Refresh();
            offsets.Refresh();

            if (keysinmemory)
            {
                hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
                hkey_offsets_arr = offsets.ElementValues().Cast<long>().ToArray();
                RebuildTypedIndexFromOffsets(hkey_offsets_arr);
            }
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
            long start = 0, end = hkeys.Count() - 1, right_equal = -1;
            // Сжимаем диапазон
            while (end - start > 1)
            {
                // Находим середину
                long middle = (start + end) / 2;
                int middle_value = (int)hkeys.GetByIndex(middle);
                if (middle_value < hkey)
                {
                    // Займемся правым интервалом
                    start = middle;
                }
                else if (middle_value > hkey)
                {
                    // Займемся левым интервалом
                    end = middle;
                }
                else
                {
                    // Середина дает РАВНО
                    end = middle;
                    right_equal = middle;
                }
            }

            return right_equal;
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
        /// Для int/long/Guid, когда индекс находится в памяти, используется typed lookup по настоящему ключу
        /// без чтения payload-записей. Это основной index-only API для benchmark-ов.
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

            if (TryGetTypedOffsetRange(keysample, out var start, out var count))
            {
                if (count == 0) return Array.Empty<long>();

                var result = new long[count];
                Array.Copy(typed_offsets_arr!, start, result, 0, count);
                return result;
            }

            return GetOffsetsByHashCompatiblePath(keysample);
        }

        /// <summary>
        /// Возвращает число offset-ов по ключу без чтения payload, если доступен typed index.
        /// </summary>
        public int CountByKey(IComparable keysample)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));

            if (keyoff_dic.ContainsKey(keysample))
            {
                return 1;
            }

            if (TryGetTypedOffsetRange(keysample, out _, out var count))
            {
                return count;
            }

            return GetOffsetsByHashCompatiblePath(keysample).Count;
        }

        /// <summary>
        /// Пытается получить offset ровно одного элемента по ключу без чтения payload, если typed index доступен.
        /// Возвращает false, если найдено 0 или больше 1 offset-а.
        /// </summary>
        public bool TryGetExactlyOneOffsetByKey(IComparable keysample, out long offset)
        {
            if (keysample == null) throw new ArgumentNullException(nameof(keysample));

            if (keyoff_dic.TryGetValue(keysample, out offset))
            {
                return true;
            }

            if (TryGetTypedOffsetRange(keysample, out var start, out var count))
            {
                if (count == 1)
                {
                    offset = typed_offsets_arr![start];
                    return true;
                }

                offset = default;
                return false;
            }

            var offsets = GetOffsetsByHashCompatiblePath(keysample);
            if (offsets.Count == 1)
            {
                offset = offsets[0];
                return true;
            }

            offset = default;
            return false;
        }

        private bool TryGetTypedOffsetRange(IComparable keysample, out int start, out int count)
        {
            start = 0;
            count = 0;

            switch (typedKeyKind)
            {
                case TypedKeyKind.Int32:
                    if (TryConvertToInt32Key(keysample, out var intKey) && int_keys_arr != null &&
                        typed_offsets_arr != null)
                    {
                        start = LowerBound(int_keys_arr, intKey);
                        var end = start;
                        while (end < int_keys_arr.Length && int_keys_arr[end] == intKey) end++;
                        count = end - start;
                        return true;
                    }

                    break;

                case TypedKeyKind.Int64:
                    if (TryConvertToInt64Key(keysample, out var longKey) && long_keys_arr != null &&
                        typed_offsets_arr != null)
                    {
                        start = LowerBound(long_keys_arr, longKey);
                        var end = start;
                        while (end < long_keys_arr.Length && long_keys_arr[end] == longKey) end++;
                        count = end - start;
                        return true;
                    }

                    break;

                case TypedKeyKind.Guid:
                    if (TryConvertToGuidKey(keysample, out var guidKey) && guid_keys_arr != null &&
                        typed_offsets_arr != null)
                    {
                        start = LowerBound(guid_keys_arr, guidKey);
                        var end = start;
                        while (end < guid_keys_arr.Length && guid_keys_arr[end].CompareTo(guidKey) == 0) end++;
                        count = end - start;
                        return true;
                    }

                    break;
            }

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
                int pos = GetFirstInMemoryNom(hkey);
                if (pos < 0) return result;

                while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                {
                    long offset = hkey_offsets_arr != null
                        ? hkey_offsets_arr[pos]
                        : (long)offsets.GetByIndex(pos);

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