using Polar.DB;

namespace Polar.Universal
{
    public class ESortIndexStr : IUIndex
    {
        // Есть опорная последовательность
        private readonly USequence sequence;
        // Есть ключевая функция, вырабатывающая на элементах поток ключей. Ключи можно сравнивать!
        private Func<object, IEnumerable<string>> svaluesFunc;

        // Статическая часть индекса
        private UniversalSequenceBase svalues;
        private UniversalSequenceBase offsets;
        private bool disposed;

        // Динамическая часть состоит из списка первичный_ключ - (локальный_)ключ-величина - офсет
        struct PSO
        {
            public IComparable primary;
            public string svalue;
            public long offset;
        };
        private PSO[] pso_arr;
        private Comparer<string>? comp;

        public ESortIndexStr(Func<Stream> streamGen, USequence sequence,
            Func<object, IEnumerable<string>> svaluesFunc, Comparer<string>? comp)
        {
            this.sequence = sequence;
            this.svaluesFunc = svaluesFunc;
            this.comp = comp;

            svalues = new UniversalSequenceBase(new PType(PTypeEnumeration.sstring), streamGen());
            offsets = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());

            pso_arr = new PSO[0];
        }

        public void OnAppendElement(object element, long offset)
        {
            var svals = svaluesFunc(element);//
            var primary_key = sequence.keyFunc(element);
            var query = pso_arr
                .Where(pso => pso.primary != primary_key)
                .Concat(svals.Select(s => new PSO()
                {
                    primary = primary_key,
                    svalue = s,
                    offset = offset
                }));
            pso_arr = query.ToArray();
        }

        // Массив оптимизации поиска по значению хеша
        private string[] svalues_arr = new string[0];

        public void Clear() { svalues.Clear(); svalues_arr = new string[0]; offsets.Clear(); pso_arr = new PSO[0]; }
        public void Flush() { svalues.Flush(); offsets.Flush(); }
        public void Close() { Dispose(); }

        public void Refresh()
        {
            svalues_arr = svalues.ElementValues().Cast<string>().ToArray();
            offsets.Refresh();
        }

        public void Build()
        {
            // сканируем опорную последовательность, формируем массивы
            List<string> svalues_list = new();
            List<long> offsets_list = new();

            sequence.Scan((off, obj) =>
            {
                var loc_keys = svaluesFunc(obj);
                foreach (var lk in loc_keys)
                {
                    offsets_list.Add(off);
                    svalues_list.Add(lk);
                }
                return true;
            });
            svalues_arr = svalues_list.ToArray();
            svalues_list = new List<string>();
            long[] offsets_arr = offsets_list.ToArray();
            offsets_list = new List<long>();
            GC.Collect();

            Array.Sort(svalues_arr, offsets_arr);

            svalues.Clear();
            foreach (var svals in svalues_arr) { svalues.AppendElement(svals); }
            svalues.Flush();

            offsets.Clear();
            foreach (var off in offsets_arr) { offsets.AppendElement(off); }
            offsets.Flush();
            offsets_arr = new long[0];
            GC.Collect();
        }

        public IEnumerable<object> GetManyByValue(string svalue)
        {
            // Определяю функцию определения равенства с возможным учетом компаратора
            Func<string, string, bool> SEQU = (arg, sample) => comp == null ? arg == sample : comp.Compare(arg, sample) == 0;

            // Посмотрим в динамическом множестве. Надо убрать пустые, остальные годятся
            var dyn_candidates = pso_arr.Where(pso => SEQU(pso.svalue, svalue));

            // Ищем в статическом индексе
            int pos = Array.BinarySearch<string>(svalues_arr, svalue, comp);

            // Список статически найденных элементов
            List<object> objects = new();
            if (pos >= 0)
            {
                //  ищем самую левую позицию 
                int p = pos;
                while (p >= 0 && SEQU(svalues_arr[p], svalue)) { pos = p; p--; }

                // движемся вправо
                for (int i = pos; i < svalues_arr.Length && SEQU(svalues_arr[i], svalue); i++)
                {
                    // Проверки 1) на первичный ключ 2) на непустоту  3) на локальный ключ

                    // Находим офсет из параллельной последовательности офсетов
                    long offset = (long)offsets.GetByIndex(i);
                    // Десериализуем элемент (объект)
                    object elem_obj = sequence.GetByOffset(offset);
                    // Получаем первичный ключ
                    var p_key = sequence.keyFunc(elem_obj);
                    // Этот ключ мог быть перемещен в динамическую область. Проверим
                    if (pso_arr.Any(pso => pso.primary == p_key)) continue; // Этот не берем
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
            svalues.Dispose();
            offsets.Dispose();
            disposed = true;
        }
    }
}
