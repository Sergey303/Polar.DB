using Polar.DB;
using Polar.Universal;
using System;
using System.Collections.Generic;
using System.Text;

namespace mag_series00
{
    internal class TestUSequence
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
            int npersons = 100_000;

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
            var elements = new object[]
            {
                new object[] { 3, false, "0", 33 },
                new object[] { 2, false, "1", 33 },
                new object[] { 1, false, "2", 33 },
                new object[] { 0, false, "3", 33 },
                //new object[] { 3, false, "00", 11 },
            };
            useq.Load(elements);
            useq.Build();
            useq.AppendElement(new object[] { 3, false, "000", 111 });

            Console.WriteLine();
            var all = useq.ElementValues();
            foreach (var element in all)
            {
                Console.WriteLine(tp_pers.Interpret(element));
            }
            var val = useq.GetByKey(3);
            if (val != null) Console.WriteLine("\n" + tp_pers.Interpret(val));
        }

    }
}
