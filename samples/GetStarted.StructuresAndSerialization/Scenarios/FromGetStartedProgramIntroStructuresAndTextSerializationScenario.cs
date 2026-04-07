using Polar.DB;

namespace GetStarted.StructuresAndSerialization.Scenarios;

internal sealed class FromGetStartedProgramIntroStructuresAndTextSerializationScenario : ISampleScenario
{
    public string Id => "gs-legacy";
    public string Title => "Базовые структуры и текстовая сериализация (из samples/GetStarted/Program.cs)";
    public string SourcePath => "samples/GetStarted/Program.cs";
    public bool IsExtractedFragment => true;

    public void Run()
    {
        Console.WriteLine("GetStarted Start!");

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

        // Дальше в исходном файле начиналась тема последовательностей.
        // Она должна переехать в отдельный тематический проект.
    }
}
