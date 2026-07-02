using Polar.DB;
using Polar.DB.ExternalKey;
using Polar.Universal;

namespace mag_experiments
{
    internal static class PersonExternalKeySample
    {
        

    public static void Run(string dbPath)
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Random rnd = new Random();

            // Тип элемента последовательности
            var tp = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)),
                new NamedType("deleted", new PType(PTypeEnumeration.boolean)));
            var accessor = new RecordAccessor(tp);

            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbPath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            USequence sequence = new (tp, dbPath + "state.bin", GenStream,
                obj => accessor.Get<bool>(obj, "deleted"), 
                obj => accessor.Get<int>(obj, "id"), 
                key => (int)key);

            object[] flow0 =
[
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
                new object[] { 99, "11", 33, false },
                new object[] { 101, "1__", 32, false }
// Возможно, надо добавить элементы с идентификаторами 6 и 7 
];
            int npersons = 5_000_000;
            var flow5m = Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, i.ToString(), 22, false });

            sequence.Load(flow0);

            ExternalKeyIndex<int> ageIndex = new ExternalKeyIndex<int>(GenStream, sequence,
                obj => Enumerable.Repeat(accessor.Get<int>(obj, "age"), 1));
            //Polar.DB.ExternalKey.ExternalKeyIndex<int> ageIndex = new Polar.DB.ExternalKey.ExternalKeyIndex<int>(
            //    GenStream, sequence,
            //    ob => Enumerable.Repeat<int>((int)((object[])ob)[2], 1));

            ExternalKeyIndex<int> ager = new ExternalKeyIndex<int>(GenStream, sequence,
                obj => Enumerable.Repeat(accessor.Get<int>(obj, "age"), 1),
                Comparer<int>.Create((int v1, int v2) => Math.Abs(v1 - v2) < 2 ? 0 : v1 - v2));
            ExternalKeyIndex<string> namer = new ExternalKeyIndex<string>(GenStream, sequence,
                obj => Enumerable.Repeat(accessor.Get<string>(obj, "name"), 1),
                Comparer<string>.Create((string s1, string s2) =>
                {
                    string a = (string)s1;
                    string b = (string)s2;
                    if (string.IsNullOrEmpty(b)) return 0;
                    int len = b.Length;
                    return string.Compare(
                        a, 0,
                        b, 0, len, StringComparison.Ordinal);
                }));

            //sequence.uindexes = [ageIndex, ager, namer];
            sequence.Build();


            sequence.Clear();
            sw.Restart();
            sequence.Load(flow5m);
            sequence.Build();
            sw.Stop();
            Console.WriteLine($"Load 5M OK duration={sw.ElapsedMilliseconds}");

            sequence.Clear();
            sequence.Load(flow0);
            sequence.Flush();
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
            var qu = ageIndex.GetManyByValue(age).ToArray();
            if (qu != null) foreach (var item in qu) Console.WriteLine(tp.Interpret(item));

            Console.WriteLine();
            Console.WriteLine($"Испытание индекса ager, выдаем все записи, в которых age менее, чем на 2 отличается от образца {age}");
            var qu2 = ager.GetManyByValue(age).ToArray();
            if (qu2 != null) foreach (var item in qu2) Console.WriteLine(tp.Interpret(item));

            Console.WriteLine($"\nУничтожим элементы 10 и 12");
            sequence.AppendElement(new object[] { 10, "", -1, true });
            sequence.AppendElement(new object[] { 12, "", -1, true });

            Console.WriteLine("\nПовторим испытание ager");
            var qu3 = ager.GetManyByValue(age).ToArray();
            if (qu3 != null) foreach (var item in qu3) Console.WriteLine(tp.Interpret(item));

            Console.WriteLine("\nМножество элементов:");
            elementset("");

            var squery = namer.GetManyByValue("1_").ToArray();
            if (squery != null) foreach (var item in squery) Console.WriteLine(tp.Interpret(item));

            // Проверка скорости загрузки
            Console.WriteLine("\n Проверка скорости загрузки");
            sequence.Clear();
            sw.Restart();

            // Загрузка данными
            

            sequence.Load(flow5m);
            sequence.Build();

            sw.Stop();
            int ke = npersons * 2 / 3;
            var res = sequence.GetByKey(ke);
            Console.WriteLine(tp.Interpret(res));
            Console.WriteLine($"Проба ok. duration={sw.ElapsedMilliseconds} ms");

            sw.Restart();
            for (int i = 0; i < 10_000; i++)
            {
                int k = rnd.Next(npersons);
                var result = sequence.GetByKey(k);
            }
            sw.Stop();
            Console.WriteLine($"Выборка 10 тыс. элементов по ключу. duration={sw.ElapsedMilliseconds} ms");




        }
    }
}
