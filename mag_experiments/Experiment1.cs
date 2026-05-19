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
            USequence useq = new USequence(tp_pers, dbpath + "state.bin", GenStream, ob => false,
                ob => (int)((object[])ob)[0], ic => (int)ic, true);

            sw.Restart();
            // Загрузка данными
            int npersons = 10_000_000;
            useq.Load(Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, i.ToString(), 33 }));
            sw.Stop();
            Console.WriteLine($"Загрузка: {sw.ElapsedMilliseconds} ms.");
            
            sw.Restart();
            // Построение индекса
            useq.Build();
            sw.Stop();
            Console.WriteLine($"Построение: {sw.ElapsedMilliseconds} ms.");

            int key = npersons * 2 / 3;
            var valu = useq.GetByKey(key);
            // Проверка
            Console.WriteLine(tp_pers.Interpret(valu));

            sw.Restart();
            // Испытание
            for (int i = 0; i < 10000; i++)
            {
                int k = rnd.Next(npersons);
                var ob = useq.GetByKey(k);
            }

            sw.Stop();
            Console.WriteLine($"Испытание (10 тыс.): {sw.ElapsedMilliseconds} ms.");

            // Результаты: 10 млн. записей
            // загр. 1.3 с, пост. 2.6 с., испы. 77 мс.
        }
    }
}
