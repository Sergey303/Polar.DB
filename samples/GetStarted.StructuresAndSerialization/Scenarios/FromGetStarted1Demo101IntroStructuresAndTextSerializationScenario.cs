using Polar.DB;

namespace GetStarted.StructuresAndSerialization.Scenarios;

internal sealed class FromGetStarted1Demo101IntroStructuresAndTextSerializationScenario : ISampleScenario
{
    public string Id => "gs1-demo101";
    public string Title => "Базовые структуры, текстовая сериализация и мостик к последовательностям (из Demo101)";
    public string SourcePath => "samples/GetStarted1/Demo101.cs";
    public bool IsExtractedFragment => true;

    public void Run()
    {
        Console.WriteLine("Start Demo101");
        // === Демонстрация базовых действий со структурами ===
        // Создаем тип персоны
        PType tp_person = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)),
            new NamedType("age", new PType(PTypeEnumeration.integer)));
        // делаем персону в объектном представлении
        object ivanov = new object[] { 7001, "Иванов", 20 };
        // интерпретируем объект в контексте типа
        Console.WriteLine(tp_person.Interpret(ivanov, true));
        // то же, но без имен полей
        Console.WriteLine(tp_person.Interpret(ivanov));
        Console.WriteLine();

        // Создадим поток байтов. Это мог бы быть файл:
        MemoryStream mstream = new MemoryStream();
        // Поработаем через текстовый интерфейс
        TextWriter tw = new StreamWriter(mstream);
        TextFlow.Serialize(tw, ivanov, tp_person);
        tw.Flush();
        // Прочитаем то что записали
        TextReader tr = new StreamReader(mstream);
        mstream.Position = 0L;
        string instream = tr.ReadToEnd();
        Console.WriteLine($"======== instream={instream}");
        Console.WriteLine();

        // Теперь десериализуем
        ivanov = null!;
        mstream.Position = 0L;
        ivanov = TextFlow.Deserialize(tr, tp_person);
        // проинтерпретируем объект и посмотрим
        Console.WriteLine(tp_person.Interpret(ivanov));
        Console.WriteLine();

        // ===== Последовательности =====
        // Создаем тип последовательности персон
        PType tp_persons = new PTypeSequence(tp_person);
        // Сделаем генератор персон
        Random rnd = new Random(1);
        Func<int, IEnumerable<object>> GenPers = nper => Enumerable.Range(0, nper)
            .Select(i => new object[] { i, "Иванов_" + i, rnd.Next(130) });

        // Сгенерируем пробу и проинтерпретируем
        object sequobj = GenPers(5).ToArray();
        Console.WriteLine(tp_persons.Interpret(sequobj));
        Console.WriteLine();

        // Чем плохо такое решение? Тем, что весь большой объект (последовательность записей) разворачивается в ОЗУ
        // Более экономным, как правило, является использование последовательностей
        // Следующий шаг исходного файла уже переходил к UniversalSequenceBase и файловому хранилищу.
        // Эта часть должна жить в отдельном проекте про последовательности.
    }
}
