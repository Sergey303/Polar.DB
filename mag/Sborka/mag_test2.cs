using Polar.DB;

namespace Sborka
{
    internal class mag_test2
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

            usequence.Clear();
            usequence.Load(Enumerable.Range(0, 10).Where(ii => ii % 2 == 0).Select(ii => new object[] { ii, ii.ToString(), 22 }).Reverse());
            usequence.AppendElement(new object[] { 3, "ae", 33 });
            usequence.AppendElement(new object[] { 1, "ae", 33 });
            usequence.AppendElement(new object[] { 5, "ae", 33 });
            usequence.AppendElement(new object[] { 9, "ae", 33 });
            usequence.AppendElement(new object[] { 7, "ae", 33 });

            //foreach (var v in usequence.ElementValues().OrderBy(x => (int)((object[])x)[0])) { Console.WriteLine(tp.Interpret(v)); }
            Console.WriteLine("key=4 " + tp.Interpret(usequence.GetByKey(4)));
            Console.WriteLine("key=9 " + tp.Interpret(usequence.GetByKey(9)));

            // ======= тест на изменение элементов
            usequence.Clear();
            usequence.Load(new object[]
            {
    new object[] { 3, "a", 0 }, new object[] { 2, "a", 0 }, new object[] { 1, "a", 0 }, new object[] { 0, "a", 0 },
    new object[] { 0, "b", 0 }, new object[] { 2, "b", 0 },
            });
            usequence.AppendElement(new object[] { 0, "c", 3 });
            for (int i = 0; i < 4; i++)
            {
                Console.WriteLine($"code {i} result {tp.Interpret(usequence.GetByKey(i))}");
            }

        }
    }
}
