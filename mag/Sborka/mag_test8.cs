using Polar.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sborka
{
    internal class mag_test8
    {
        public static void Run(string dbPath, int npersons, bool toload)
        {
            // ======= тест на добавление элементов
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            var rnd = new Random();

            PType tp_person = new PTypeRecord(
                new NamedType("code", new PType(PTypeEnumeration.integer)),
                new NamedType("empty", new PType(PTypeEnumeration.boolean)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));
            PType tp_photo = new PTypeRecord(
                new NamedType("code", new PType(PTypeEnumeration.integer)),
                new NamedType("empty", new PType(PTypeEnumeration.boolean)),
                new NamedType("name", new PType(PTypeEnumeration.sstring))
                );
            PType tp_reflection = new PTypeRecord(
                new NamedType("code", new PType(PTypeEnumeration.integer)),
                new NamedType("empty", new PType(PTypeEnumeration.boolean)),
                new NamedType("reflected", new PType(PTypeEnumeration.integer)),
                new NamedType("indoc", new PType(PTypeEnumeration.integer)));

            // Генератор стримов
            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbPath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            npersons = 1_000_000;
            USequ persons = new USequ(tp_person, GenStream, ob => (int)((object[])ob)[0], k => (int)k, ob => (bool)((object[])ob)[1]);
            USequ photos = new USequ(tp_photo, GenStream, ob => (int)((object[])ob)[0], k => (int)k, ob => (bool)((object[])ob)[1]);
            USequ reflections = new USequ(tp_reflection, GenStream, ob => (int)((object[])ob)[0], k => (int)k, ob => (bool)((object[])ob)[1]);
            EKeyIndex eperso = new EKeyIndex(GenStream, reflections, ob => new IComparable[] { (IComparable)((object[])ob)[2] }, k => (int)k);
            EKeyIndex ephoto = new EKeyIndex(GenStream, reflections, ob => new IComparable[] { (IComparable)((object[])ob)[3] }, k => (int)k);
            reflections.AppendIndexes(new IEIndex[] { eperso, ephoto });

            if (toload)
            {
                sw.Restart();
                persons.Load(Enumerable.Range(0, npersons).Select(i => new object[] { i, false, "p" + i, 33 }));
                photos.Load(Enumerable.Range(0, npersons * 2).Select(i => new object[] { i, false, "ph" + i }));
                reflections.Load(Enumerable.Range(0, npersons * 6).Select(i => new object[] { i, false, rnd.Next(npersons), rnd.Next(npersons * 2) }));
                sw.Stop();
                Console.WriteLine($"load ok. duration={sw.ElapsedMilliseconds} ms");
            }
            int k = npersons * 2 / 3;
            var ob = persons.GetByKey(k);
            if (ob != null) Console.WriteLine(tp_person.Interpret(ob));

            sw.Restart();
            for (int i = 0; i < 10000; i++)
            {
                int p = rnd.Next(npersons);
                var pe = persons.GetByKey(p);
                if (pe == null) Console.WriteLine($"null in {p}");
            }
            sw.Stop();
            Console.WriteLine($"GetByKey duration={sw.ElapsedMilliseconds} ms");

            sw.Restart();
            for (int j = 0; j < 1000; j++)
            {
                int r = rnd.Next(npersons);
                //Console.WriteLine(tp_person.Interpret(persons.GetByKey(r)));
                var qwery = eperso.GetManyByKey(r);
                foreach (object[] reflection in qwery)
                {
                    int edoc = (int)reflection[3];
                    var doc = photos.GetByKey(edoc);
                    //Console.WriteLine("\t" + tp_photo.Interpret(doc));
                }
            }
            sw.Stop();
            Console.WriteLine($"GetManyByKey duration={sw.ElapsedMilliseconds} ms");
        }
    }
}
