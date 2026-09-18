using Polar.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sborka
{
    internal class mag_test4
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

            USequ usequence = new USequ(tp, GenStream, ob => (int)((object[])ob)[0], k => (int)k, ob => (bool)((object[])ob)[1]);

            // Сначала загрузка
            usequence.Load(new object[]
            {
                new object[] { 4, false, "load", 1},
                new object[] { 3, false, "load", 1},
                new object[] { 2, false, "load", 1},
                new object[] { 1, true, "load", 1},
                new object[] { 0, false, "load", 1},
            });
            usequence.AppendElement(new object[] { 2, true, "append", 2 });
            usequence.AppendElement(new object[] { 5, false, "append", 3 });
            usequence.AppendElement(new object[] { 2, false, "append", 3 });

            foreach (var key in new int[] { 0, 1, 2, 3, 4 })
            {
                object? ob = usequence.GetByKey(key);
                string line = "key=" + key + ": " + (ob == null ? "null" : tp.Interpret(ob));
                Console.WriteLine(line);
            }
        }
    }
}
