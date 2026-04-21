using Polar.DB;

namespace GetStarted.SequencesAndStorage.Scenarios;

internal sealed class FromGetStartedProgramUniversalSequenceAndUSequenceScenario : ISampleScenario
{
    public string Id => "gs-legacy-seq";
    public string Title => "UniversalSequenceBase, массив офсетов и USequence (из samples/GetStarted/Program.cs)";
    public string SourcePath => "samples/GetStarted/Program.cs";
    public bool IsExtractedFragment => true;

    public void Run()
    {
        // ===== Последовательности =====
        // Создаем тип последовательности персон
        PType tp_person = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)),
            new NamedType("age", new PType(PTypeEnumeration.integer)));
        PType tp_persons = new PTypeSequence(tp_person);

        // Сделаем генератор персон
        Random rnd = new Random(1);
        Func<int, IEnumerable<object>> GenPers = nper => Enumerable.Range(0, nper)
            .Select(i => new object[] { i, "Иванов_" + i, rnd.Next(130) });

        // Сгенерируем пробу и проинтерпретируем
        object sequobj = GenPers(20).ToArray();
        Console.WriteLine(tp_persons.Interpret(sequobj));
        Console.WriteLine();

        // Чем плохо такое решение? Тем, что весь большой объект (последовательность записей) разворачивается в ОЗУ
        // Более экономным, как правило, является использование последовательностей

        string dbpath = SamplePaths.DataDirectory + Path.DirectorySeparatorChar;
        Stream filestream = new FileStream(Path.Combine(dbpath, "legacy-file0.bin"), FileMode.OpenOrCreate, FileAccess.ReadWrite);
        UniversalSequenceBase usequence = new UniversalSequenceBase(tp_person, filestream);

        // Последовательность можно очистить, в нее можно добавлять элементы, в конце добавлений нужно сбросить буфер
        // В исходном файле было 1_000_000. Для учебного проекта возьмем более мягкий объём.
        int npersons = 5_000;
        usequence.Clear();
        foreach (object record in GenPers(npersons))
        {
            usequence.AppendElement(record);
        }
        usequence.Flush();

        // Теперь можно сканировать последовательность
        int totalages = 0;
        usequence.Scan((_, ob) => { totalages += (int)((object[])ob)[2]; return true; });
        Console.WriteLine($"total ages = {totalages}");

        // Можно прочитать i-ый элемент
        int nom = npersons * 2 / 3;

        // Но нет - облом: Размер элемента не фиксирован (есть строка), к таким элементам по индексу обращаться не надо
        // Чтобы организовать прямой доступ к элементам последовательности с нефиксированными размерами, нужен индекс
        // Простейший индекс - массив офсетов
        long[] offsets = new long[usequence.Count()];
        int i = 0;
        foreach (var pair in usequence.ElementOffsetValuePairs())
        {
            offsets[i] = pair.Item1;
            i++;
        }

        // Теперь мы можем читать из последовательности элемент по номеру
        long offset = offsets[nom];
        object res = usequence.GetElement(offset);
        Console.WriteLine($"element={tp_person.Interpret(res)}");

        // Правильнее хранить индексный массив также в последовательности
        Stream filestream2 = new FileStream(Path.Combine(dbpath, "legacy-file11.bin"), FileMode.OpenOrCreate, FileAccess.ReadWrite);
        UniversalSequenceBase offset_seq = new UniversalSequenceBase(
            new PType(PTypeEnumeration.longinteger), filestream2);
        offset_seq.Clear();
        foreach (var pair in usequence.ElementOffsetValuePairs())
        {
            offset_seq.AppendElement(pair.Item1);
        }
        offset_seq.Flush();

        // Теперь получение офсета еще проще
        offset = (long)offset_seq.GetByIndex(nom);
        // далее, как уже было
        res = usequence.GetElement(offset);
        Console.WriteLine($"element={tp_person.Interpret(res)}");

        // ============= Универсальная последовательность =============
        // Сигнатура конструктора:
        //public USequence(PType tp_el, Func<Stream> streamGen, Func<object, bool> isEmpty,
        //    Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey, bool optimise = true);
        // где:
        // tp_el - тип элементов последовательности
        // streamGen - генератор стримов
        // isEmpty - функция, определяющая что элемент пустой
        // keyFunc - функция, дающая ключ (идентификатор) элемента
        // hashOfKey - функция, задающая целочисленный хеш от ключа
        int cnt = 0;
        Func<Stream> GenStream = () => new FileStream(Path.Combine(dbpath, $"f{cnt++}.bin"), FileMode.OpenOrCreate, FileAccess.ReadWrite);
        USequence usequ = new USequence(tp_person, Path.Combine(dbpath, "statefile.bin"), GenStream, _ => false, ob => (int)((object[])ob)[0], id => (int)id, false);
        usequ.Load(GenPers(npersons));
        usequ.Build();
        var obj = usequ.GetByKey(nom);
        Console.WriteLine($"element={tp_person.Interpret(obj)}"); // Мы получили требуемый элемент!
    }
}
