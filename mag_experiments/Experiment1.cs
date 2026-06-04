using Polar.DB;
using Polar.Universal;

namespace mag_experiments
{
    internal class Experiment1
    {
        public static void Run()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Random rnd = new Random();
            Console.WriteLine("Experiment1");

            // Тип элемента последовательности
            PType tp_pers = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            // Указываем директорию для файлов базы данных, формируем генератор потоков
            string dbpath = "C:\\Home\\data\\getstarted\\";
            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbpath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            // Создаем универсальную последовательность
            //USequence useq = new USequence(tp_pers, dbpath + "state.bin", GenStream, ob => false,
            //    ob => (int)((object[])ob)[0], ic => (int)ic, true);
            USequenceBase usb = new USequenceBase(tp_pers, GenStream());
            sw.Restart();
            // Загрузка данными
            int npersons = 5_000_000;

            usb.Clear();

            var query = Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, i.ToString(), 22 });

            List<int> key_list = new List<int>();
            List<long> offset_list = new List<long>();
            foreach (var item in query)
            {
                int key = (int)((object[])item)[0];
                long off = usb.AppendElement(item);
                key_list.Add(key);
                offset_list.Add(off);
            }
            usb.Flush();

            int[] keys_arr = key_list.ToArray();
            key_list = new List<int>();
            long[] offsets_arr = offset_list.ToArray();
            offset_list = new List<long>();

            sw.Stop();
            Console.WriteLine($"Загрузка: {sw.ElapsedMilliseconds} ms.");

            sw.Restart();

            // Построение индекса
            Array.Sort<int, long>(keys_arr, offsets_arr);
            USequenceBase keys = new USequenceBase(new PType(PTypeEnumeration.integer), GenStream());
            foreach (var key in keys_arr) { keys.AppendElement(key); }
            keys.Flush();
            USequenceBase offsets = new USequenceBase(new PType(PTypeEnumeration.longinteger), GenStream());
            foreach (var off in offsets_arr) { offsets.AppendElement(off); }
            offsets.Flush();

            sw.Stop();
            Console.WriteLine($"Сортировка: {sw.ElapsedMilliseconds} ms.");

            int ke = npersons * 2 / 3;
            int nom = keys_arr.BinarySearch<int>(ke);
            long offset = offsets_arr[nom];
            var obj = usb.GetElement(offset);
            Console.WriteLine(tp_pers.Interpret(obj));

            sw.Restart();
            
            for (int i = 0; i < 10000; i++)
            {
                int k = rnd.Next(npersons);
                int n = keys_arr.BinarySearch<int>(k);
                //long o = offsets_arr[n];
                long o = (long)offsets.GetByIndex(n);
                var ob = usb.GetElement(o);
                //if (i < 100) Console.WriteLine(tp_pers.Interpret(ob));
            }

            sw.Stop();
            Console.WriteLine($"10000 выборок: {sw.ElapsedMilliseconds} ms.");

            // Результаты:
            // 10 млн. элементов загрузка 1168 мс. построение 854 мс. Выборки: 43 мс / 10 тыс. (для массива offsets_arr) 77 мс (нормально)

            // 5 млн. элементов загрузка 746 мс. построение 545 мс. Выборки: 76 мс (нормально)

            //useq.Build();
            //sw.Stop();
            //Console.WriteLine($"Построение: {sw.ElapsedMilliseconds} ms.");

            //int key = npersons * 2 / 3;
            //var valu = useq.GetByKey(key);
            //// Проверка
            //Console.WriteLine(tp_pers.Interpret(valu));

            //sw.Restart();
            //// Испытание
            //for (int i = 0; i < 10000; i++)
            //{
            //    int k = rnd.Next(npersons);
            //    var ob = useq.GetByKey(k);
            //}

            //sw.Stop();
            //Console.WriteLine($"Испытание (10 тыс.): {sw.ElapsedMilliseconds} ms.");

            // Результаты: 10 млн. записей
            // загр. 1.3 с, пост. 2.6 с., испы. 77 мс.
            // Release: загр. 0.9 с, пост. 2.0 с., испы. 81 мс.
        }
    }
}
