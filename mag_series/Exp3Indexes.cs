using Polar.DB;
//using Polar.Universal;

namespace mag_series
{
    internal class Exp3Indexes
    {
        public static void Run(string dbpath)
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Console.WriteLine("Start Exp3Indexes");


            // Тип элемента последовательности
            PType tp = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)),
                new NamedType("deleted", new PType(PTypeEnumeration.boolean)));

            //string dbpath = "C:\\Home\\data\\getstarted\\";
            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbpath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            USequence sequence = new USequence(tp, dbpath + "state.bin", GenStream,
                obj => (bool)((object[])obj)[3], obj => (int)((object[])obj)[0], key => (int)key);
            

            object[] flow0 = new object[]
            {
                new object[] { 5, "five", 55, false },
                new object[] { 4, "4", 44, false },
                new object[] { 3, "3", 31, false },
                new object[] { 2, "2", 33, false },
                new object[] { 1, "1___0", 32, false },
                new object[] { 10, "кандидат на уничтожение", 33, false },
                new object[] { 11, "1___2", 34, false },
                new object[] { 12, "Второй кандидат", 35, false },
                new object[] { 13, "Статически уничтоженный элемент", 36, true },
                new object[] { 100, "1_", 32, false },
                new object[] { 101, "1__", 32, false },
                // Возможно, надо добавить элементы с идентификаторами 6 и 7 
            };
            sequence.Clear();
            sequence.Load(flow0);
            sequence.Flush();

            EKeyIndex ageIndex = new EKeyIndex(GenStream, sequence,
                ob => Enumerable.Repeat<IComparable>((int)((object[])ob)[2], 1),
                com => (int)com);
            //Polar.DB.ExternalKey.ExternalKeyIndex<int> ageIndex = new Polar.DB.ExternalKey.ExternalKeyIndex<int>(
            //    GenStream, sequence,
            //    ob => Enumerable.Repeat<int>((int)((object[])ob)[2], 1));

            sequence.uindexes = new IUIndex[] { ageIndex };

            sequence.Build();

            Console.WriteLine("Начальный набор элементов");
            Action<string> elementset = (mess) =>
            {
                foreach (var element in sequence.ElementValues())
                {
                    Console.WriteLine(tp.Interpret(element));
                }
                Console.WriteLine($"={mess}\nвсе\n");
            };
            elementset("обратим внимание на то, что элемент 13 отсутствует в выдаче");

            Console.WriteLine($"находим элемент с id 4: {tp.Interpret(sequence.GetByKey(4))}\n");

            Console.WriteLine($"пытаемся найти элемент с id 13: ");
            try
            {
                var obj = tp.Interpret(sequence.GetByKey(13));
            }
            catch (Exception ex) { Console.WriteLine(ex); }

            Console.WriteLine("\nПоработаем с age индексом: будем искать все записи, в которых age=33");
            int age = 33;
            var qu = ageIndex.GetManyByKey(age).ToArray();
            if (qu != null) foreach (var item in qu) Console.WriteLine(tp.Interpret(item));

            Console.WriteLine($"\nУничтожим элементы 10 и 12");
            sequence.AppendElement(new object[] { 10, "", -1, true });
            sequence.AppendElement(new object[] { 12, "", -1, true });

            Console.WriteLine("\nПовторим испытание");
            var qu2 = ageIndex.GetManyByKey(age).ToArray();
            if (qu2 != null) foreach (var item in qu2) Console.WriteLine(tp.Interpret(item));

        }
    }
}
