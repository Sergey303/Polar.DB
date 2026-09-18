using Polar.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sborka
{
    internal class mag_test1
    {
        public static void Run(string dbPath, int npersons, bool toload)
        {
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

            IEnumerable<object> flow = Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, i.ToString(), 22 });

            if (toload)
            {
                sw.Restart();
                usequence.Load(flow);
                sw.Stop();
                Console.WriteLine("Load ok. duration=" + sw.ElapsedMilliseconds);
            }
            else
            {
                sw.Restart();
                usequence.Connect();
                sw.Stop();
                Console.WriteLine("Refresh ok. duration=" + sw.ElapsedMilliseconds);
            }


            //foreach (object ob in usequence.ElementValues()) Console.WriteLine(tp.Interpret(ob));
            int cod = npersons * 2 / 3;
            object? ob = usequence.GetByKey(cod);

            if (ob != null) Console.WriteLine(tp.Interpret(ob));
            else Console.WriteLine($"item with cod {cod} not found");

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
