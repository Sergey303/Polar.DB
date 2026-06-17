using Polar.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace mag_experiments
{
    internal class Experiment3
    {
        public static void Run()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Random rnd = new Random();
            Console.WriteLine("Experiment3");



            // Тип элемента последовательности
            PType tp_pers = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            // Указываем директорию для файлов базы данных, формируем генератор потоков
            string dbpath = "C:\\Home\\data\\getstarted\\";

            bool toload = true;
            if (toload)
            {
                var files = Directory.GetFiles(dbpath);
                foreach (var file in files) File.Delete(file);
            }

            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbpath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            // Создаем опорную последовательность
            UniversalSequence usb = new UniversalSequence(tp_pers, dbpath + "state.bin", GenStream, ob => false,
                ob => (int)((object[])ob)[0], ic => (int)ic);
            //BearingSequence usb = new BearingSequence(tp_pers, GenStream, obj => (int)((object[])obj)[0], k => (int)k);

            // Загрузка данными
            int npersons = 5_000_000;
            Console.WriteLine($"{npersons} элементов");

            var query = Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, i.ToString(), 22 });

            if (toload)
            {
                sw.Restart();
                usb.Load(query);
                sw.Stop();
                Console.WriteLine($"Загрузка: {sw.ElapsedMilliseconds} ms.");
                sw.Restart();
                usb.Build();
                sw.Stop();
                Console.WriteLine($"Сортировка: {sw.ElapsedMilliseconds} ms.");
            }
            else
            {
                sw.Restart();
                usb.Refresh();
                sw.Stop();
                Console.WriteLine($"Refresh: {sw.ElapsedMilliseconds} ms.");
            }



            int ke = npersons * 2 / 3;
            var obj = usb.GetByKey(ke);
            Console.WriteLine(tp_pers.Interpret(obj));

            sw.Restart();

            for (int i = 0; i < 10000; i++)
            {
                int k = rnd.Next(npersons);
                var ob = usb.GetByKey(k);
                //if (i < 100) Console.WriteLine(tp_pers.Interpret(ob));
            }

            sw.Stop();
            Console.WriteLine($"10000 выборок: {sw.ElapsedMilliseconds} ms.");

            //// Результаты:
            //// 10 млн. элементов загрузка 1168 мс. построение 854 мс. Выборки: 43 мс / 10 тыс. (для массива offsets_arr) 77 мс (нормально)

            //// 5 млн. элементов загрузка 746 мс. построение 545 мс. Выборки: 76 мс (нормально)

            //// Убрал Build (возможно, он будет для индексов), теперь загрузка включает в себя сортировку.
            //// 5 млн. элементов загрузка 1151 мс. Выборки: 83 мс

        }

    }
}
