using Polar.DB;
using Polar.Universal;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace TestingZone
{
    internal class test3
    {
        public static void Run(string dbPath, int npersons, bool toload)
        {
            Console.WriteLine("0");

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            var rnd = new Random();

            PType tp = new PTypeRecord(
                new NamedType("code", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            // Генератор стримов
            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbPath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            Console.WriteLine("01");
            using UniversalSequenceBase usb = new UniversalSequenceBase(tp, GenStream());
            Console.WriteLine("02");

            IEnumerable<object> flow = Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, i.ToString(), 22 });

            Console.WriteLine("1");

            if (toload)
            {
                sw.Restart();
                usb.Clear();
                foreach (var element  in flow)
                {
                    usb.AppendElement(element);
                }
                usb.Flush();
                sw.Stop();
                Console.WriteLine("Load ok. duration=" + sw.ElapsedMilliseconds);
            }
            // The constructor has already opened and restored the sequence.
            // Calling Refresh here would needlessly validate every record again.


            Console.WriteLine("2");

            sw.Restart();
            long sum = 0L;
            foreach (object element in usb.ElementValues())
            {
                //Console.WriteLine(tp.Interpret(element));
                sum += (int)((object[])element)[2];
            }
            sw.Stop();
            Console.WriteLine($"ElementValues() scan ok. sum={sum} duration={sw.ElapsedMilliseconds}");


            Console.WriteLine("3");

            sw.Restart();
            sum = 0L;
            usb.Scan((off, ob) =>
            {
                sum += (int)((object[])ob)[2];
                return true;
            });
            sw.Stop();
            Console.WriteLine($"Scan() ok. sum={sum} duration={sw.ElapsedMilliseconds}");

            Console.WriteLine("4");

        }
    }
}
