using Polar.DB;
using Polar.Universal;
using System;
using System.Collections.Generic;
using System.Text;

namespace Polar.Universal
{
    public class EKeyIndex : IUIndex
    {
        // Есть опорная последовательность
        private readonly USequence sequence;
        // Есть ключевая функция, вырабатывающая на элементах поток ключей. Ключи можно сравнивать!
        private Func<object, IEnumerable<IComparable>> keysFunc;
        // Есть преобразователь ключа в целое, это может быть хеш-функция или тожлественная
        private Func<IComparable, int> hashOfKey;
        // Статическая часть индекса
        private UniversalSequenceBase hkeys;
        private UniversalSequenceBase offsets;
        // Динамическая часть состоит из списка первичный_ключ - (локальный_)ключ - объект
        struct PLO
        {
            public IComparable primary;
            public IComparable local;
            public long offset;
        };
        private List<PLO> plo_list;

        public EKeyIndex(Func<Stream> streamGen, USequence sequence,
            Func<object, IEnumerable<IComparable>> keysFunc, Func<IComparable, int> hashOfKey)
        {
            this.sequence = sequence;
            this.keysFunc = keysFunc;
            this.hashOfKey = hashOfKey;

            hkeys = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());

            plo_list = new List<PLO>();
        }
        public void OnAppendElement(object element, long offset)
        {
            var keys = keysFunc(element);//.Distinct(); // Возможно, надо так...

            var primary_key = sequence.keyFunc(element);
            var query = plo_list
                .Where(plo => plo.primary != primary_key)
                .Concat(keys.Select(k => new PLO()
                {
                    primary = primary_key,
                    local = k,
                    offset = offset
                }));
            plo_list = query.ToList();
        }

        // Массив оптимизации поиска по значению хеша
        private int[] hkeys_arr = new int[0];

        public void Clear() { hkeys.Clear(); hkeys_arr = new int[0]; offsets.Clear(); plo_list = new List<PLO>(); }
        public void Flush() { hkeys.Flush(); offsets.Flush(); }
        public void Close() { hkeys.Close(); offsets.Close(); }
        public void Refresh()
        {
            hkeys_arr = hkeys.ElementValues().Cast<int>().ToArray();
            offsets.Refresh();
        }

        public void Build()
        {
            // сканируем опорную последовательность, формируем массивы
            List<int> hkeys_list = new ();
            List<long> offsets_list = new ();

            sequence.Scan((off, obj) =>
            {
                var loc_keys = keysFunc(obj);
                foreach (var lk in loc_keys)
                {
                    offsets_list.Add(off);
                    hkeys_list.Add(hashOfKey(lk));
                }
                return true;
            });
            hkeys_arr = hkeys_list.ToArray();
            hkeys_list = new List<int>();
            long[] offsets_arr = offsets_list.ToArray();
            offsets_list = new List<long>();
            GC.Collect();

            Array.Sort(hkeys_arr, offsets_arr);

            hkeys.Clear();
            foreach (var hkey in hkeys_arr) { hkeys.AppendElement(hkey); }
            hkeys.Flush();

            offsets.Clear();
            foreach (var off in offsets_arr) { offsets.AppendElement(off); }
            offsets.Flush();
            offsets_arr = new long[0];
            GC.Collect();
        }

        public IEnumerable<object> GetManyByKey(IComparable localkey)
        {
            // Посмотрим в динамическом множестве. Надо убрать пустые, остальные годятся
            var dyn_candidates = plo_list.Where(plo => plo.local == localkey);

            int hkey = hashOfKey(localkey);

            // Ищем в статическом индексе
            int pos = Array.BinarySearch<int>(hkeys_arr, hkey);


            // Список статически найденных элементов
            List<object> objects = new ();
            if (pos >= 0)
            {
                //  ищем самую левую позицию 
                int p = pos;
                while (p >= 0 && hkeys_arr[p] == hkey) { pos = p; p--; }

                // Создаем множество офсетов объектов objects
                HashSet<long> offhash = new ();

                // движемся вправо
                //while (pos < hkeys_arr.Length && hkeys_arr[pos] == hkey)
                for (int i = pos; i < hkeys_arr.Length && hkeys_arr[i] == hkey; i++)
                {
                    // Проверки 1) на первичный ключ 2) на непустоту  3) на локальный ключ

                    // Находим офсет из параллельной последовательности офсетов
                    long offset = (long)offsets.GetByIndex(i);
                    // Десериализуем элемент (объект)
                    object elem_obj = sequence.GetByOffset(offset);
                    // Получаем первичный ключ
                    var p_key = sequence.keyFunc(elem_obj);
                    // Этот ключ мог быть перемещен в динамическую область. Проверим
                    if (plo_list.Any(plo => plo.primary == p_key)) continue; // Этот не берем
                                                                             // элемент может быть пустым, такие не берем
                    if (sequence.isEmpty(elem_obj)) continue;
                    // Элемент должен содержать искомый локальный ключ
                    if (!keysFunc(elem_obj).Contains<IComparable>(localkey)) continue;

                    // Теперь этот элемент надо попробовать накопить, элементы разные если офсеты разные
                    // Проверим на вхождение в offhash
                    if (offhash.Contains(offset)) continue; // пропускаем
                                                            // Добавим в offhash и objects
                    offhash.Add(offset);
                    objects.Add(elem_obj);
                }
            }
            return dyn_candidates
                    .Select(plo => plo.offset)
                    .Distinct() // убрали повторы
                    .Select(off => sequence.GetByOffset(off)) // преобразовали в объектную форму
                    .Where(ob => !sequence.isEmpty(ob)) // убрали пустые
                    .Concat(objects)
                    ;
        }
    }
}
