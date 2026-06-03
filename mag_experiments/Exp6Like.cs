using System;
using System.Collections.Generic;
using System.Text;

namespace mag_experiments
{
    internal class Exp6Like
    {
        public static void Run()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Console.WriteLine("Start Exp6Like");
            string[] db = 
            {
                "_1111",
                "8888",
                "_1",
                "_11",
                "_111",
                "0000"
            };
            var cs = comp_string;
            Array.Sort(db, cs);
            string sample = "_111";
            int pos = Array.BinarySearch(db, sample, comp_string_like);
            int i = 0;
            foreach (string s in db)
            {
                Console.WriteLine($"{i} {s}");
                i++;
            }
            Console.WriteLine($"\n  sample={sample}  pos={pos}\n");
            if (pos >= 0) 
            {
                int start = pos;
                while (start > 0 && comp_string_like.Compare(db[start - 1], sample) == 0) { start--; }
                int last = pos;
                while (last + 1 < db.Length && comp_string_like.Compare(db[last + 1], sample) == 0) { last++; }

                for (int j = start; j <= last; j++) 
                {
                    Console.WriteLine($"{j} {db[j]}");
                }
            }

        }


        // Компараторы для строк
        public static Comparer<string> comp_string = Comparer<string>.Create(new Comparison<string>((string v1, string v2) =>
        {
            string a = (string)v1;
            string b = (string)v2;
            //if (string.IsNullOrEmpty(b)) return 0;
            return string.Compare(a, b, StringComparison.Ordinal);
        }));
        public static Comparer<string> comp_string_like = Comparer<string>.Create(new Comparison<string>((string v1, string v2) =>
        {
            string a = (string)v1;
            string b = (string)v2;
            if (string.IsNullOrEmpty(b)) return 0;
            int len = b.Length;
            return string.Compare(
                a, 0,
                b, 0, len, StringComparison.Ordinal);
        }));
    }
}
