using Polar.DB;
using Polar.Universal;

namespace mag_experiments
{
    internal class Exp2
    {
        public static void Run()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Random rnd = new Random();
            Console.WriteLine("Exp2");

            // Тип элемента последовательности
            PType tp_pers = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.sstring)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            // Указываем директорию для файлов базы данных, формируем генератор потоков
            string dbpath = "C:\\Home\\data\\getstarted\\";
            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbpath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            // Создаем универсальную последовательность
            USequence useq = new USequence(tp_pers, dbpath + "state.bin", GenStream, ob => false,
                ob => (string)((object[])ob)[0], ic => (int)Hashfunctions.HashRot13((string)ic), true);

            sw.Restart();
            // Загрузка данными
            int npersons = 10_000_000;
            useq.Load(Enumerable.Range(0, npersons)
                .Select(i => new object[] { "" + (npersons - i - 1), i.ToString(), rnd.Next(npersons / 100) }));
            sw.Stop();
            Console.WriteLine($"Загрузка: {sw.ElapsedMilliseconds} ms.");

            sw.Restart();
            // Построение индекса
            useq.Build();
            sw.Stop();
            Console.WriteLine($"Построение: {sw.ElapsedMilliseconds} ms.");

            string key = "" + (npersons * 2 / 3);
            var valu = useq.GetByKey(key);
            // Проверка
            Console.WriteLine(tp_pers.Interpret(valu));

            sw.Restart();
            // Испытание
            for (int i = 0; i < 10000; i++)
            {
                string k = "" + rnd.Next(npersons);
                var ob = useq.GetByKey(k);
            }
            sw.Stop();
            Console.WriteLine($"Испытание (10 тыс.): {sw.ElapsedMilliseconds} ms.");

            // Результаты: 10 млн. записей
            // загр. 1.5 с, пост. 3.2 с., испы. 80 мс.

            // Предельный расчет 300 млн. записей (10 Гб). 
            // загр. 28 с, пост. 62 с., испы. 1300 мс. (ОЗУ около 8-9 Гб)

            // Добавлю 1% записей с другими идентификаторами
            int nadditions = npersons / 100;
            sw.Restart();
            for (int i = 0; i < nadditions; i++)
            {
                useq.AppendElement(new object[] { "n" + i, "n" + i, 999999 });
            }
            sw.Stop();
            Console.WriteLine($"{nadditions} добавлений: {sw.ElapsedMilliseconds} ms.");

            // Проверяю доступ к старым элементам
            sw.Restart();
            // Испытание
            for (int i = 0; i < 10000; i++)
            {
                string k = "" + rnd.Next(npersons);
                var ob = useq.GetByKey(k);
                object[] rec = (object[])ob;
                string sid = (string)rec[0];
                if (sid == null || sid != k) throw new Exception();
            }
            sw.Stop();
            Console.WriteLine($"Поиск старых (10 тыс.): {sw.ElapsedMilliseconds} ms.");

            // Проверяю доступ к новым элементам
            sw.Restart();
            // Испытание
            for (int i = 0; i < 10000; i++)
            {
                string k = "n" + rnd.Next(nadditions);
                var ob = useq.GetByKey(k);
                object[] rec = (object[])ob;
                string sid = (string)rec[0];
                if (sid == null || sid != k) throw new Exception();
            }
            sw.Stop();
            Console.WriteLine($"Поиск новых (10 тыс.): {sw.ElapsedMilliseconds} ms.");

            // Сканирование всей последовательности
            sw.Restart();
            int nvalues = 0;
            foreach (var ob  in useq.ElementValues())
            {
                nvalues++;
            }
            sw.Stop();
            Console.WriteLine($"Сканирование всех ({nvalues}): {sw.ElapsedMilliseconds} ms.");

        }
    }
}
