using Polar.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sborka
{
    internal class mag_test3
    {
        public static void Run(string dbPath, int npersons, bool toload)
        {
            // ======= тест на добавление элементов
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            var rnd = new Random();

            PType tp = new PTypeRecord(
                new NamedType("code", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            // Генератор стримов
            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbPath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            USequ usequence = new USequ(tp, GenStream, ob => (int)((object[])ob)[0], k => (int)k, ob => false);

            sw.Restart();
            for (int i = 0; i < npersons; i++)
            {
                usequence.AppendElement(new object[] { npersons - i - 1, i.ToString(), 44 });
            }
            sw.Stop();
            Console.WriteLine($"Dynamic appends ok. duration={sw.ElapsedMilliseconds} ms");
            sw.Restart();
            for (int i = 0; i < 10000; i++)
            {
                int k = rnd.Next(npersons);
                object? v = usequence.GetByKey(k);
                if (v != null) { }//Console.WriteLine(tp.Interpret(v));
                else Console.WriteLine($"item with cod {k} not found");
            }
            sw.Stop();
            Console.WriteLine("10K GetByKey ok. duration=" + sw.ElapsedMilliseconds);
        }
    }
}
