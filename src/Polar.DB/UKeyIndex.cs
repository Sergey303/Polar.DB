namespace Polar.DB
{
    public class UKeyIndex
    {
        private readonly USequence sequence;
        // Ключом является объект, порождаемый ключевой функцией. Ключи можно сравнивать!
        private Func<object, IComparable> keyFunc;
        private Func<IComparable, int> hashOfKey;
        // Статическая часть индекса. Persistent формат оставлен прежним:
        // hkeys хранит int hash(key), offsets хранит offset записи.
        private UniversalSequenceBase hkeys;
        private UniversalSequenceBase offsets;
        // Динамическая часть индекса.
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

        // Массив оптимизации поиска по значению хеша. Нужен для backward-compatible hash lookup.
        private int[]? hkeys_arr = null;
        private long[]? hkey_offsets_arr = null;

        // In-memory typed lookup слой. Persistent формат не меняется: эти массивы строятся
        // при Build() и восстанавливаются при Refresh() из существующего offsets + data файла.
        private TypedKeyKind typedKeyKind = TypedKeyKind.None;
        private int[]? int_keys_arr = null;
        private long[]? long_keys_arr = null;
        private Guid[]? guid_keys_arr = null;
        private long[]? typed_offsets_arr = null;

        public void Clear()
        {
            hkeys.Clear();
            offsets.Clear();
            keyoff_dic.Clear();
            ClearInMemoryIndexes();
        }

        public void Flush() { hkeys.Flush(); offsets.Flush(); }
        public void Close() { hkeys.Close(); offsets.Close(); }

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
        }

        public void Build()
        {
            // Сканируем опорную последовательность, формируем массивы.
            // Важно: persistent hkeys/offsets по-прежнему строятся как hash -> offset,
            // чтобы не менять файловый формат и не ломать старые сценарии.
            var hkeys_list = new List<int>();
            var offsets_list = new List<long>();
            var typed_keys_list = new List<IComparable>();
            var typed_offsets_list = new List<long>();

            sequence.Scan((off, obj) =>
            {
                var key = keyFunc(obj);
                offsets_list.Add(off);
                hkeys_list.Add(hashOfKey(key));

                typed_keys_list.Add(key);
                typed_offsets_list.Add(off);
                return true;
            });

            hkeys_arr = hkeys_list.ToArray();
            hkeys_list = null;
            hkey_offsets_arr = offsets_list.ToArray();
            offsets_list = null;

            Array.Sort(hkeys_arr, hkey_offsets_arr);

            BuildTypedInMemoryIndex(typed_keys_list, typed_offsets_list);
            typed_keys_list = null;
            typed_offsets_list = null;

            hkeys.Clear();
            foreach (var hkey in hkeys_arr) { hkeys.AppendElement(hkey); }
            hkeys.Flush();

            offsets.Clear();
            foreach (var off in hkey_offsets_arr) { offsets.AppendElement(off); }
            offsets.Flush();

            if (!keysinmemory)
            {
                ClearInMemoryIndexes();
                GC.Collect();
            }
        }

        public object? GetByKey(IComparable keysample)
        {
            foreach (var value in GetAllByKey(keysample))
            {
                return value;
            }

            return null;
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

        private bool TryGetTypedOffsetRange(IComparable keysample, out int start, out int count)
        {
            start = 0;
            count = 0;

            switch (typedKeyKind)
            {
                case TypedKeyKind.Int32:
                    if (TryConvertToInt32Key(keysample, out var intKey) && int_keys_arr != null && typed_offsets_arr != null)
                    {
                        start = LowerBound(int_keys_arr, intKey);
                        var end = start;
                        while (end < int_keys_arr.Length && int_keys_arr[end] == intKey) end++;
                        count = end - start;
                        return true;
                    }
                    break;

                case TypedKeyKind.Int64:
                    if (TryConvertToInt64Key(keysample, out var longKey) && long_keys_arr != null && typed_offsets_arr != null)
                    {
                        start = LowerBound(long_keys_arr, longKey);
                        var end = start;
                        while (end < long_keys_arr.Length && long_keys_arr[end] == longKey) end++;
                        count = end - start;
                        return true;
                    }
                    break;

                case TypedKeyKind.Guid:
                    if (TryConvertToGuidKey(keysample, out var guidKey) && guid_keys_arr != null && typed_offsets_arr != null)
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

        private void ClearInMemoryIndexes()
        {
            hkeys_arr = null;
            hkey_offsets_arr = null;
            typedKeyKind = TypedKeyKind.None;
            int_keys_arr = null;
            long_keys_arr = null;
            guid_keys_arr = null;
            typed_offsets_arr = null;
        }

        private void RebuildTypedIndexFromOffsets(long[] sourceOffsets)
        {
            if (!keysinmemory || sourceOffsets.Length == 0)
            {
                return;
            }

            var keys = new List<IComparable>(sourceOffsets.Length);
            var typedOffsets = new List<long>(sourceOffsets.Length);

            foreach (var off in sourceOffsets)
            {
                var value = sequence.GetByOffset(off);
                if (value == null || !sequence.IsOriginalAndNotEmpty(value, off))
                {
                    continue;
                }

                keys.Add(keyFunc(value));
                typedOffsets.Add(off);
            }

            BuildTypedInMemoryIndex(keys, typedOffsets);
        }

        private void BuildTypedInMemoryIndex(IReadOnlyList<IComparable> keys, IReadOnlyList<long> sourceOffsets)
        {
            typedKeyKind = TypedKeyKind.None;
            int_keys_arr = null;
            long_keys_arr = null;
            guid_keys_arr = null;
            typed_offsets_arr = null;

            if (!keysinmemory || keys.Count == 0 || keys.Count != sourceOffsets.Count)
            {
                return;
            }

            if (TryBuildInt32Index(keys, sourceOffsets)) return;
            if (TryBuildInt64Index(keys, sourceOffsets)) return;
            TryBuildGuidIndex(keys, sourceOffsets);
        }

        private bool TryBuildInt32Index(IReadOnlyList<IComparable> keys, IReadOnlyList<long> sourceOffsets)
        {
            var typedKeys = new int[keys.Count];
            var typedOffsets = new long[sourceOffsets.Count];

            for (var i = 0; i < keys.Count; i++)
            {
                if (!TryConvertToInt32Key(keys[i], out typedKeys[i]))
                {
                    return false;
                }

                typedOffsets[i] = sourceOffsets[i];
            }

            Array.Sort(typedKeys, typedOffsets);
            typedKeyKind = TypedKeyKind.Int32;
            int_keys_arr = typedKeys;
            typed_offsets_arr = typedOffsets;
            return true;
        }

        private bool TryBuildInt64Index(IReadOnlyList<IComparable> keys, IReadOnlyList<long> sourceOffsets)
        {
            var typedKeys = new long[keys.Count];
            var typedOffsets = new long[sourceOffsets.Count];

            for (var i = 0; i < keys.Count; i++)
            {
                if (!TryConvertToInt64Key(keys[i], out typedKeys[i]))
                {
                    return false;
                }

                typedOffsets[i] = sourceOffsets[i];
            }

            Array.Sort(typedKeys, typedOffsets);
            typedKeyKind = TypedKeyKind.Int64;
            long_keys_arr = typedKeys;
            typed_offsets_arr = typedOffsets;
            return true;
        }

        private bool TryBuildGuidIndex(IReadOnlyList<IComparable> keys, IReadOnlyList<long> sourceOffsets)
        {
            var typedKeys = new Guid[keys.Count];
            var typedOffsets = new long[sourceOffsets.Count];

            for (var i = 0; i < keys.Count; i++)
            {
                if (!TryConvertToGuidKey(keys[i], out typedKeys[i]))
                {
                    return false;
                }

                typedOffsets[i] = sourceOffsets[i];
            }

            Array.Sort(typedKeys, typedOffsets);
            typedKeyKind = TypedKeyKind.Guid;
            guid_keys_arr = typedKeys;
            typed_offsets_arr = typedOffsets;
            return true;
        }

        private static bool TryConvertToInt32Key(IComparable key, out int value)
        {
            switch (key)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                    value = (int)longValue;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryConvertToInt64Key(IComparable key, out long value)
        {
            switch (key)
            {
                case long longValue:
                    value = longValue;
                    return true;
                case int intValue:
                    value = intValue;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryConvertToGuidKey(IComparable key, out Guid value)
        {
            switch (key)
            {
                case Guid guidValue:
                    value = guidValue;
                    return true;
                case string text when Guid.TryParse(text, out var parsed):
                    value = parsed;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private static int LowerBound(int[] values, int sample)
        {
            var start = 0;
            var end = values.Length;
            while (start < end)
            {
                var middle = start + ((end - start) / 2);
                if (values[middle] < sample) start = middle + 1;
                else end = middle;
            }
            return start;
        }

        private static int LowerBound(long[] values, long sample)
        {
            var start = 0;
            var end = values.Length;
            while (start < end)
            {
                var middle = start + ((end - start) / 2);
                if (values[middle] < sample) start = middle + 1;
                else end = middle;
            }
            return start;
        }

        private static int LowerBound(Guid[] values, Guid sample)
        {
            var start = 0;
            var end = values.Length;
            while (start < end)
            {
                var middle = start + ((end - start) / 2);
                if (values[middle].CompareTo(sample) < 0) start = middle + 1;
                else end = middle;
            }
            return start;
        }

        private int GetFirstInMemoryNom(int hkey)
        {
            if (hkeys_arr == null) return -1;

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
        /// Определение номера первого индекса последовательности hkeys, с которого значения РАВНЫ hkey (хешу от ключа).
        /// Если нет таких, то -1L.
        /// </summary>
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
        /// Определяет является ли пара (key, offset) оригиналом или нет. Если такого ключа нет в дин. индексе, то это оригинал.
        /// Если есть, то надо проверить офсет.
        /// </summary>
        public bool IsOriginal(IComparable key, long offset)
        {
            if (keyoff_dic.TryGetValue(key, out long off))
            {
                return off == offset;
            }
            return true; // TODO: здесь предполагается, что в основном индексе есть такое значение.
        }

        private enum TypedKeyKind
        {
            None,
            Int32,
            Int64,
            Guid
        }
    }
}
