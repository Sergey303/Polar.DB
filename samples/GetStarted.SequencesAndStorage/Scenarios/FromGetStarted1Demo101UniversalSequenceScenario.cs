using Polar.DB;

namespace GetStarted.SequencesAndStorage.Scenarios;

internal sealed class FromGetStarted1Demo101UniversalSequenceScenario : ISampleScenario
{
    public string Id => "gs1-demo101-seq";
    public string Title => "Базовая UniversalSequenceBase: загрузка и сканирование (из Demo101)";
    public string SourcePath => "samples/GetStarted1/Demo101.cs";
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
        Random rnd = new Random(2);
        Func<int, IEnumerable<object>> GenPers = nper => Enumerable.Range(0, nper)
            .Select(i => new object[] { i, "Иванов_" + i, rnd.Next(130) });

        // Сгенерируем пробу и проинтерпретируем
        object sequobj = GenPers(20).ToArray();
        Console.WriteLine(tp_persons.Interpret(sequobj));
        Console.WriteLine();

        // Чем плохо такое решение? Тем, что весь большой объект (последовательность записей) разворачивается в ОЗУ
        // Более экономным, как правило, является использование последовательностей

        Stream filestream = new FileStream(SamplePaths.File("demo101-db0.bin"), FileMode.OpenOrCreate, FileAccess.ReadWrite);
        UniversalSequenceBase usequence = new UniversalSequenceBase(tp_person, filestream);

        // Последовательность можно очистить, в нее можно добавлять элементы, в конце добавлений нужно сбросить буфер
        int npersons = 5_000;
        usequence.Clear();
        foreach (object record in GenPers(npersons))
        {
            usequence.AppendElement(record);
        }
        usequence.Flush();

        // Теперь можно сканировать последовательность
        int totalages = 0;
        usequence.Scan((_, ob) => {
            object o = (((object[]?)ob)?[2]) ?? throw new NullReferenceException(nameof(o));
            totalages += (int)o; return true; });
        Console.WriteLine($"total ages = {totalages}");

        // Следующим шагом в исходном файле уже шли варианты индексного доступа.
        // Они разложены по отдельным сценариям этого проекта.
    }
}
