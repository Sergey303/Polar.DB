using Polar.DB;
using Polar.Universal;

namespace mag_experiments
{
    internal class Exp5EKeyIndex
    {
        public static void Run()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Console.WriteLine("Start Exp5EkeyIndex");


            // Тип элемента последовательности
            PType tp = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            string dbpath = "C:\\Home\\data\\getstarted\\";
            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbpath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            USequence sequence = new USequence(tp, dbpath + "state.bin", GenStream,
                obj  => false,  obj => (int)((object[])obj)[0], key => (int)key);
            Polar.Universal.EKeyIndex ageIndex = new EKeyIndex(GenStream, sequence, 
                ob => Enumerable.Repeat<IComparable>((int)((object[])ob)[2], 1),
                com => (int)com);
            ESortIndex32 ager = new ESortIndex32(GenStream, sequence,
                ob => Enumerable.Repeat<int>((int)((object[])ob)[2], 1),
                comp_near);
                
            sequence.uindexes = new IUIndex[] { ageIndex, ager };
            sequence.Build();

            object[] flow0 = new object[]
            {
                new object[] { 5, "five", 55 },
                new object[] { 4, "4", 44 },
                new object[] { 3, "3", 31 },
                new object[] { 2, "2", 33 },
                new object[] { 1, "1___0", 32 },
                new object[] { 1, "1___1", 33 },
                new object[] { 1, "1___2", 34 },
                new object[] { 1, "1___3", 35 },
            };
            sequence.Clear();
            sequence.Load(flow0);
            sequence.Flush();
            sequence.Build();

            int k = 5;
            var res = sequence.GetByKey(k);
            if (res != null) Console.WriteLine(tp.Interpret(res));
            Console.WriteLine();

            int age = 33;
            var qu = ageIndex.GetManyByKey(age).ToArray();
            if (qu != null) foreach (var item in qu) Console.WriteLine(tp.Interpret(item));

            Console.WriteLine();
            // Испытание индекса ager
            var qu2 = ager.GetManyByValue(age).ToArray();
            if (qu2 != null) foreach (var item in qu2) Console.WriteLine(tp.Interpret(item));
        }
        // Компараторы для целочисленного режима
        public static Comparer<int> comp_near = Comparer<int>.Create(new Comparison<int>((int v1, int v2) =>
        {
            //string a = (string)v1;
            //string b = (string)v2;
            //if (string.IsNullOrEmpty(b)) return 0;
            //return string.Compare(a, b, StringComparison.Ordinal);
            return Math.Abs(v1 - v2) < 9 ? 0 : v1 - v2;
        }));
    }
}
