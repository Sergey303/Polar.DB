using Polar.DB;

namespace GetStarted.SequencesAndStorage.Scenarios;

internal sealed class FromGetStarted3Main306PersistentKeysAndOffsetsScenario : ISampleScenario
{
    public string Id => "gs3-306";
    public string Title => "Хранение keys/offsets в отдельных последовательностях";
    public string SourcePath => "samples/GetStarted3/Main306.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        Console.WriteLine("Start Main306");
        // Создадим типы записи и последовательности записей
        PType tp_rec = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)),
            new NamedType("age", new PType(PTypeEnumeration.integer)));
        // unused PType tp_seq = new PTypeSequence(tp_rec);

        // ======== Универсальная последовательность ==========
        Stream stream = File.Open(SamplePaths.File("data306.bin"), FileMode.OpenOrCreate);
        Stream stream2 = File.Open(SamplePaths.File("keys306.bin"), FileMode.OpenOrCreate);
        Stream stream3 = File.Open(SamplePaths.File("offsets306.bin"), FileMode.OpenOrCreate);
        UniversalSequenceBase sequence = new UniversalSequenceBase(tp_rec, stream);
        UniversalSequenceBase sequence2 = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream2);
        UniversalSequenceBase sequence3 = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), stream3);

        Random rnd = new Random(7);
        // В исходном файле было 100_000_000. Для учебного запуска это слишком тяжело.
        int nelements = 10_000;

        // При заполнении массива, сохраним офсеты элементов в массиве
        long[] offsets = new long[nelements];
        int[] keys = new int[nelements];

        bool toload = true;

        if (toload)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            sequence.Clear();
            for (int i = 0; i < nelements; i++)
            {
                int key = nelements - i - 1;
                offsets[i] = sequence.AppendElement(new object[] { key, "Иванов" + key, rnd.Next(1, 110) });
                keys[i] = key;
            }
            sequence.Flush();
            // отсортируем пару массивов keys, offsets по ключам
            Array.Sort(keys, offsets);

            // запишем массивы в последовательности
            sequence2.Clear();
            for (int i = 0; i < nelements; i++)
            {
                sequence2.AppendElement(keys[i]);
            }
            sequence2.Flush();

            sequence3.Clear();
            for (int i = 0; i < nelements; i++)
            {
                sequence3.AppendElement(offsets[i]);
            }
            sequence3.Flush();

            sw.Stop();
            Console.WriteLine($"Load of {nelements} elements. duration={sw.ElapsedMilliseconds}");
        }

        // Сначала сделаем единичный тест
        int k = nelements * 2 / 3;
        long ind = BinarySequenceSearchFirst(0, nelements, k, sequence2);
        long offf = (long)sequence3.GetByIndex(ind);
        object rec = sequence.GetElement(offf);

        Console.WriteLine($"k={k}, v={tp_rec.Interpret(rec)}");

        // Будем делать выборку элементов по ключу
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        int ntests = 1000;
        for (int j = 0; j < ntests; j++)
        {
            int key = rnd.Next(nelements);
            long nom1 = BinarySequenceSearchFirst(0, nelements, key, sequence2);
            long off = (long)sequence3.GetByIndex(nom1);
            object[] fields = (object[])sequence.GetElement(off);
            if (key != (int)fields[0]) throw new Exception("1233eddf");
        }
        sw2.Stop();
        Console.WriteLine($"duration of {ntests} tests is {sw2.ElapsedMilliseconds} ms.");

        // 10 тыс. элементов загрузка 5.4 сек. выборка 1 тыс. 97 мсек.
        // 100 тыс. элементов загрузка 71 сек. выборка 1 тыс. 25 сек.
    }

    private static long BinarySequenceSearchFirst(long start, long number, int key, UniversalSequenceBase seq)
    {
        long half = number / 2;
        int middle_keyvalue = (int)seq.GetByIndex(start + half);
        if (half == 0) // number = 0, 1
        {
            if ((int)seq.GetByIndex(start) == key) return start;
            else if ((int)seq.GetByIndex(start + 1) == key) return start + 1;
            else return -1;
        }
        if (middle_keyvalue == key) return start + half;

        long middle = start + half;
        long rest = number - half - 1;
        var middle_depth = middle_keyvalue - key;

        if (middle_depth == 0) // Нашли!
        {
            return middle;
        }
        if (middle_depth < 0)
        {
            return BinarySequenceSearchFirst(middle + 1, rest, key, seq);
        }
        else
        {
            return BinarySequenceSearchFirst(start, half, key, seq);
        }
    }
}
