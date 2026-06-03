using Polar.DB;
using Polar.Universal;

using System;
using System.Collections.Generic;
using System.Text;

namespace mag_experiments
{
    internal class Exp7Indexes
    {
        public static void Run()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Console.WriteLine("Start Exp7Indexes");


            // Тип элемента последовательности
            PType tp = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)),
                new NamedType("deleted", new PType(PTypeEnumeration.boolean)));

            string dbpath = "C:\\Home\\data\\getstarted\\";
            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbpath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            USequence sequence = new USequence(tp, dbpath + "state.bin", GenStream,
                obj => (bool)((object[])obj)[3], obj => (int)((object[])obj)[0], key => (int)key);
            Polar.Universal.EKeyIndex ageIndex = new EKeyIndex(GenStream, sequence,
                ob => Enumerable.Repeat<IComparable>((int)((object[])ob)[2], 1),
                com => (int)com);
            ESortIndex32 ager = new ESortIndex32(GenStream, sequence,
                ob => Enumerable.Repeat<int>((int)((object[])ob)[2], 1),
                Comparer<int>.Create(new Comparison<int>((int v1, int v2) => Math.Abs(v1 - v2) < 2 ? 0 : v1 - v2 )));
            ESortIndexStr namer = new ESortIndexStr(GenStream, sequence,
                ob => Enumerable.Repeat<string>((string)((object[])ob)[1], 1),
                Comparer<string>.Create(new Comparison<string>((string s1, string s2) =>
                {
                    string a = (string)s1;
                    string b = (string)s2;
                    if (string.IsNullOrEmpty(b)) return 0;
                    int len = b.Length;
                    return string.Compare(
                        a, 0,
                        b, 0, len, StringComparison.Ordinal);
                })));

            sequence.uindexes = new IUIndex[] { ageIndex, ager, namer };
            sequence.Build();

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
        }
    }
}
