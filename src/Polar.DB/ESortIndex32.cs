using Polar.DB;

namespace Polar.Universal
{
    public class ESortIndex32 : IUIndex
    {
        // Есть опорная последовательность
        private readonly USequence sequence;
        // Есть ключевая функция, вырабатывающая на элементах поток ключей. Ключи можно сравнивать!
        private Func<object, IEnumerable<int>> valuesFunc;

        // Статическая часть индекса
        private UniversalSequenceBase values;
        private UniversalSequenceBase offsets;
        private bool disposed;

        // Динамическая часть состоит из списка первичный_ключ - (локальный_)ключ-величина - офсет
        struct PVO
        {
            public IComparable primary;
            public int value32;
            public long offset;
        };
        private PVO[] pvo_arr;
        private Comparer<int>? comp;

        public ESortIndex32(Func<Stream> streamGen, USequence sequence,
            Func<object, IEnumerable<int>> valuesFunc, Comparer<int>? comp)
        {
            this.sequence = sequence;
            this.valuesFunc = valuesFunc;
            this.comp = comp;

            values = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());

            pvo_arr = new PVO[0];
        }

        public void OnAppendElement(object element, long offset)
        {
            var vals = valuesFunc(element);//
            var primary_key = sequence.keyFunc(element);
            var query = pvo_arr
                .Where(pvo => pvo.primary != primary_key)
                .Concat(vals.Select(k => new PVO()
                {
                    primary = primary_key,
                    value32 = k,
                    offset = offset
                }));
            pvo_arr = query.ToArray();
        }

        // Массив оптимизации поиска по значению хеша
        private int[] values_arr = new int[0];

        public void Clear() { values.Clear(); values_arr = new int[0]; offsets.Clear(); pvo_arr = new PVO[0]; }
        public void Flush() { values.Flush(); offsets.Flush(); }
        public void Close() { Dispose(); }

        public void Refresh()
        {
            values_arr = values.ElementValues().Cast<int>().ToArray();
            offsets.Refresh();
        }

        public void Build()
        {
            // сканируем опорную последовательность, формируем массивы
            List<int> values_list = new();
            List<long> offsets_list = new();

            sequence.Scan((off, obj) =>
            {
                var loc_keys = valuesFunc(obj);
                foreach (var lk in loc_keys)
                {
                    offsets_list.Add(off);
                    values_list.Add(lk);
                }
                return true;
            });
            values_arr = values_list.ToArray();
            values_list = new List<int>();
            long[] offsets_arr = offsets_list.ToArray();
            offsets_list = new List<long>();
            GC.Collect();

            Array.Sort(values_arr, offsets_arr);

            values.Clear();
            foreach (var vals in values_arr) { values.AppendElement(vals); }
            values.Flush();

            offsets.Clear();
            foreach (var off in offsets_arr) { offsets.AppendElement(off); }
            offsets.Flush();
            offsets_arr = new long[0];
            GC.Collect();
        }

        public IEnumerable<object> GetManyByValue(int value)
        {
            // Определяю функцию определения равенства с возможным учетом компаратора
            Func<int, int, bool> CEQU = (arg, sample) => comp == null ? arg == sample : comp.Compare(arg, sample) == 0;

            // Посмотрим в динамическом множестве. Надо убрать пустые, остальные годятся
            var dyn_candidates = pvo_arr.Where(pvo => CEQU(pvo.value32, value)).ToArray();

            // Ищем в статическом индексе
            int pos = Array.BinarySearch<int>(values_arr, value, comp);

            // Список статически найденных элементов
            List<object> objects = new();
            if (pos >= 0)
            {
                //  ищем самую левую позицию 
                int p = pos;
                while (p >= 0 && CEQU(values_arr[p], value)) { pos = p; p--; }

                // движемся вправо
                for (int i = pos; i < values_arr.Length && CEQU(values_arr[i], value); i++)
                {
                    // Проверки 1) на первичный ключ 2) на непустоту  3) на локальный ключ

                    // Находим офсет из параллельной последовательности офсетов
                    long offset = (long)offsets.GetByIndex(i);
                    // Десериализуем элемент (объект)
                    object elem_obj = sequence.GetByOffset(offset);
                    // Получаем первичный ключ
                    var p_key = sequence.keyFunc(elem_obj);
                    // Этот ключ мог быть перемещен в динамическую область. Проверим
                    if (pvo_arr.Any(plo => plo.primary == p_key)) continue; // Этот не берем

                    // элемент может быть пустым, такие не берем
                    if (sequence.isEmpty(elem_obj)) continue;

                    // Проверим на то, что первичный ключ изменен, в этом случае элемент пропускаем
                    if (sequence.ElementChanged(p_key)) continue; // пропускаем

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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || disposed) return;
            values.Dispose();
            offsets.Dispose();
            disposed = true;
        }
    }
}
