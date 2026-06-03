using Polar.DB;
using Polar.Universal;

using System;
using System.Collections.Generic;
using System.Text;

namespace mag_experiments
{
    internal class Exp8Complex
    {
        public static void Run()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Console.WriteLine("Start Exp8Complex");


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
                Comparer<int>.Create(new Comparison<int>((int v1, int v2) => Math.Abs(v1 - v2) < 2 ? 0 : v1 - v2)));
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
        }
    }
}