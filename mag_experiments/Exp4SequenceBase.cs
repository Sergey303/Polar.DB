using Polar.DB;

namespace mag_experiments
{
    internal class Exp4SequenceBase
    {
        public static void Run()
        {
            System.Diagnostics.Stopwatch sw = new ();

            // Тип элемента последовательности
            PType tp = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            string dbpath = "C:\\Home\\data\\getstarted\\";
            string file1 = dbpath + "file1.bin";
            Stream db1 = File.Open(file1, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            UniversalSequenceBase sequence = new (tp, db1);

            int nelements = 10_000_000;

            sw.Restart();
            IEnumerable<object> flow1 = Enumerable.Range(0, nelements)
                .Select(i => new object[] { nelements - i - 1, "" + (nelements - i - 1), 33 });
            sequence.Clear();
            foreach (var element in flow1)
            {
                sequence.AppendElement(element);
            }
            sequence.Flush();
            sw.Stop();
            Console.WriteLine($">>>Loading {nelements} elements. Duration={sw.ElapsedMilliseconds}");
            sw.Restart();

            string file2 = dbpath + "file2.bin";
            Stream db2 = File.Open(file2, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            IEnumerable<object> flow2 = Enumerable.Range(0, nelements)
                .Select(i => new object[] { i });
            PType tp2 = new PTypeRecord(new NamedType("id", new PType(PTypeEnumeration.integer)));
            UniversalSequenceBase sequence2 = new (tp2, db2);
            sequence2.Clear();
            foreach (var element in flow2)
            {
                sequence2.AppendElement(element);
            }
            sequence2.Flush();
            sw.Stop();
            Console.WriteLine($"Loading 2 {nelements} elements. Duration={sw.ElapsedMilliseconds}");

            long off = sequence2.ElementOffset(nelements * 2 / 3);
            var record = sequence2.GetElement(off);
            Console.WriteLine(tp2.Interpret(record));   

            int ntests = 10000;
            Random rnd = new ();
            sw.Restart();
            for (int i = 0; i < ntests; i++)
            {
                off = sequence2.ElementOffset(rnd.Next(0, nelements));
                var re = sequence2.GetElement(off);
            }
            sw.Stop();
            Console.WriteLine($"10000 tests. Duration={sw.ElapsedMilliseconds}");

            sw.Restart();
            // Делаю два массива: офсеты и идентификаторы
            long[] off_arr = new long[nelements];
            int[] id_arr = new int[nelements];
            // Сканирую
            int ind = 0;
            sequence.Scan((of, re) =>
            {
                off_arr[ind] = of;
                id_arr[ind] = (int)((object[])re)[0];
                if (ind < 5) Console.WriteLine($"{off_arr[ind]} {id_arr[ind]}");
                ind++;
                return true;
            });
            // Сортирую
            Array.Sort(id_arr, off_arr);
            sw.Stop();
            Console.WriteLine($">>>Arrays ok. Duration={sw.ElapsedMilliseconds}");

            // Найдем реальный элемент
            int id = nelements * 2 / 3;
            int n = Array.BinarySearch(id_arr, id);
            off = off_arr[n];
            var rec = sequence.GetElement(off);
            Console.WriteLine(tp.Interpret(rec));

            // Сделаем тест в цикле
            sw.Restart();
            for (int i=0; i<ntests; i++ )
            {
                id = rnd.Next(0, nelements);
                n = Array.BinarySearch(id_arr, id);
                off = off_arr[n];
                var re = sequence.GetElement(off);
                //if (i < 10) Console.WriteLine(tp.Interpret(re));
            }
            sw.Stop();
            Console.WriteLine($"Main test for {nelements} elements. Duration={sw.ElapsedMilliseconds}");

            // Запишем офсеты в последовательность
            UniversalSequenceBase offsets = new (new PType(PTypeEnumeration.longinteger),
                File.Open(dbpath + "offsets", FileMode.OpenOrCreate, FileAccess.ReadWrite));
            offsets.Clear();
            foreach (var of in off_arr)
            {
                offsets.AppendElement(of);
            }
            offsets.Flush();
            UniversalSequenceBase ids = new (new PType(PTypeEnumeration.integer),
                File.Open(dbpath + "ids", FileMode.OpenOrCreate, FileAccess.ReadWrite));
            ids.Clear();
            foreach (var idd in id_arr)
            {
                ids.AppendElement(idd);
            }
            ids.Flush();
            sw.Stop();
            Console.WriteLine($">>>Запись офсетов и идентификаторов в последовательности. Duration={sw.ElapsedMilliseconds}");

            // Сделаем Главный  тест в цикле
            sw.Restart();
            for (int i = 0; i < ntests; i++)
            {
                id = rnd.Next(0, nelements);
                n = Array.BinarySearch(id_arr, id);
                long off_off = offsets.ElementOffset(n);
                off = (long)offsets.GetElement(off_off);
                var re = sequence.GetElement(off);
                //if (i < 20) Console.WriteLine(tp.Interpret(re));
            }
            sw.Stop();
            Console.WriteLine($"Main test for {nelements} elements. Duration={sw.ElapsedMilliseconds}");

        }
    }
}
