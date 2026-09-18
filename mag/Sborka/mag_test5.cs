using Polar.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sborka
{
    internal class mag_test5
    {
        public static void Run(string dbPath, int npersons, bool toload)
        {
            // ======= тест на добавление элементов
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            var rnd = new Random();

            PType tp = new PTypeRecord(
                new NamedType("code", new PType(PTypeEnumeration.integer)),
                new NamedType("empty", new PType(PTypeEnumeration.boolean)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            // Генератор стримов
            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbPath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            USequenceBase sequ = new USequenceBase(tp, GenStream());

            IEnumerable<object> flow = Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, false, i.ToString(), 55 });
            if (toload)
            {
                sw.Restart();
                sequ.Clear();
                foreach (object item in flow) { long off = sequ.AppendElement(item); }
                sequ.Flush();
                sw.Stop();
                Console.WriteLine($"load ok. duration={sw.ElapsedMilliseconds} for {npersons} elements");
            } 
            else
            {
                sw.Restart();
                sequ.Refresh();
                sw.Stop();
                Console.WriteLine($"Refresh ok. duration={sw.ElapsedMilliseconds} for {npersons} elements");
            }

            Console.WriteLine("Сканируем");
            sw.Restart();
            sequ.ElementValues().Count();
            sw.Stop(); Console.Write($"ElementValues: {sw.ElapsedMilliseconds} мс. ");

            sw.Restart();
            sequ.ElementOffsetValuePairs().Count();
            sw.Stop(); Console.Write($"ElementOffsetValuePairs: {sw.ElapsedMilliseconds} мс. ");

            sw.Restart();
            sequ.Scan((off, obj) => true);
            sw.Stop(); Console.Write($"Scan: {sw.ElapsedMilliseconds} мс. ");
            Console.WriteLine();
            // ElementValues: 821 мс. ElementOffsetValuePairs: 551 мс. Scan: 358 мс.

            USequ usequence = new USequ(tp, GenStream, ob => (int)((object[])ob)[0], k => (int)k, ob => (bool)((object[])ob)[1]);

            usequence.Clear();
            usequence.Load(new object[]
            {
                new object[] { 4, false, "load", 1},
                new object[] { 3, false, "load", 1},
                new object[] { 2, false, "load", 1},
                new object[] { 1, true, "load", 1},
                new object[] { 0, false, "load", 1},
            });
            usequence.AppendElement(new object[] { 2, true, "append", 2 });
            usequence.AppendElement(new object[] { 4, false, "append", 3 });
            usequence.AppendElement(new object[] { 2, false, "append", 3 });
            
            //foreach (var el in usequence.ElementValues())
            //{
            //    Console.WriteLine(tp.Interpret(el));
            //}
            for (var i = 0; i<5; i++)
            {
                object? v = usequence.GetByKey(i);
                Console.Write(i);
                if (v == null) Console.WriteLine("null");
                else Console.WriteLine(tp.Interpret(v));
            }
            Console.WriteLine();


            usequence.Clear();
            sw.Restart();
            //npersons = 10;
            usequence.Load(Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, false, i.ToString(), 55 }));
            usequence.AppendElement(new object[] { 2, true, "append", 2 });
            usequence.AppendElement(new object[] { 4, false, "append", 3 });
            usequence.AppendElement(new object[] { 222222222, false, "append", 3 });

            foreach (var item in usequence.ElementValues().TakeLast(10))
            {
                Console.WriteLine(tp.Interpret(item));
            }
            sw.Stop();
            Console.WriteLine("DURATION " + sw.ElapsedMilliseconds);
        }
    }
}
