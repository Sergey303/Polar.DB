using Polar.DB;
//using Polar.Universal;

namespace mag_series
{
    internal class Exp2KeyValueStorage
    {
        public static void Run(string dbpath)
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Random rnd = new Random();

            // Тип элемента последовательности
            PType tp_pers = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("empty", new PType(PTypeEnumeration.boolean)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            // Указываем директорию для файлов базы данных, формируем генератор потоков
            //string dbpath = "C:\\Home\\data\\getstarted\\";
            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbpath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            // Создаем универсальную последовательность
            USequence useq = new(tp_pers, dbpath + "state.bin", GenStream, ob => (bool)((object[])ob)[1],
                ob => (int)((object[])ob)[0], ic => (int)ic);
            
            sw.Restart();

            // Загрузка данными
            int npersons = 5_000_000;

            useq.Clear();

            var flow = Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, false, i.ToString(), 22 });
            useq.Load(flow);
            useq.Build();

            sw.Stop();
            int ke = npersons * 2 / 3;
            var res = useq.GetByKey(ke);
            Console.WriteLine(tp_pers.Interpret(res));
            Console.WriteLine($"Проба ok. duration={sw.ElapsedMilliseconds} ms");

            sw.Restart();
            for (int i = 0; i < 10_000; i++)
            {
                int k = rnd.Next(npersons);
                var result = useq.GetByKey(k);
            }
            sw.Stop();
            Console.WriteLine($"Выборка 10 тыс. элементов по ключу. duration={sw.ElapsedMilliseconds} ms");


            useq.Clear();
            Console.WriteLine("\nЭксперименты со слабой динамикой");

            // Загрузка малыми данными для проверки слабой динамики
            npersons = 10;
            useq.Load(Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, false, i.ToString(), 33 }));
            // Построение индекса
            useq.Build();
            // Испытание 
            int key = npersons * 2 / 3;
            object? valu = useq.GetByKey(key);
            if (valu != null) Console.WriteLine($"Проверка выборки по ключу {key} " + tp_pers.Interpret(valu));

            Console.WriteLine($"В последовательность записано {npersons} элементов:");
            foreach (var v in useq.ElementValues()) Console.WriteLine(tp_pers.Interpret(v));
            Console.WriteLine();

            Console.WriteLine($"Добавлю пуcтой элемент под существующим индексом 0");
            useq.AppendElement(new object[] { 0, true, "Пупкин", 22 });

            Console.WriteLine($"Осталось nelementvalues={useq.ElementValues().Count()}");

            key = 0; valu = useq.GetByKey(key);
            Console.Write($"Запрашиваем запись с ключом 0 ... ");
            if (valu != null) Console.WriteLine(tp_pers.Interpret(valu));
            else Console.WriteLine("null");

            Console.WriteLine($"Добавлю непуcтой элемент под существующим индексом 2");
            useq.AppendElement(new object[] { 2, false, "Пупкин2", 22 });

            int nelementvalues = useq.ElementValues().Count();
            Console.WriteLine($"Осталось nelementvalues={nelementvalues}");

            key = 2; valu = useq.GetByKey(key);
            Console.Write($"Запрашиваем запись с ключом 2 ... ");
            if (valu != null) Console.WriteLine(tp_pers.Interpret(valu));
            else Console.WriteLine("null");

            Console.WriteLine($"Добавлю три непуcтых элемента один под существующим 7, другие под не существующим ключом 2023334444+1");
            useq.AppendElement(new object[] { 7, false, "Пупкин Старый", 44 });
            useq.AppendElement(new object[] { 2023334444, false, "Пупкин Новый", 55 });
            useq.AppendElement(new object[] { 2023334445, false, "Пупкин Новый 2", 66 });
            useq.Flush();

            nelementvalues = useq.ElementValues().Count();
            Console.WriteLine($"Осталось nelementvalues={nelementvalues}");

            Console.WriteLine("Все  элементы последовательности:");
            foreach (var v in useq.ElementValues()) Console.WriteLine(tp_pers.Interpret(v));
            Console.WriteLine(); useq.Close();

        }
    }
}
