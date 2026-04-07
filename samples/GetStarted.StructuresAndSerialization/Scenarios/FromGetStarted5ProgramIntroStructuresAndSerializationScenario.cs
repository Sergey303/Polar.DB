using Polar.DB;

namespace GetStarted.StructuresAndSerialization.Scenarios;

internal sealed class FromGetStarted5ProgramIntroStructuresAndSerializationScenario : ISampleScenario
{
    public string Id => "gs5-intro";
    public string Title => "Структуры и сериализация из package-based sample";
    public string SourcePath => "samples/GetStarted5/Program.cs";
    public bool IsExtractedFragment => true;

    public void Run()
    {
        Console.WriteLine("Start Polar.DB tests");

        // ============ Типы структур и значения с труктур в объектном представлении ===========
        Console.WriteLine("=== Структуры и сериализация ===");
        // Создадим типы записи и последовательности записей
        PType tp1 = new PTypeRecord(
            new NamedType("name", new PType(PTypeEnumeration.sstring)),
            new NamedType("age", new PType(PTypeEnumeration.integer)));
        PType tp2 = new PTypeSequence(tp1);

        // Создадим структурные значения этих типов в объектном представлении
        object val1 = new object[] { "Иванов", 22 };
        object val2 = new object[]
        {
            new object[] { "Иванов", 22 },
            new object[] { "Петров", 33 },
            new object[] { "Сидоров", 44 }
        };

        // Визуализация структур в объектном представлении
        Console.WriteLine(tp1.Interpret(val1));
        Console.WriteLine(tp2.Interpret(val2));

        // ============== Сериализация/Десериализация =============
        // Сериализация выполняет отображение структуры на поток символов (текстовая сериализация) или  
        // поток байтов (бинарная сериализация). Десериализация выполняет обратное преобразование.
        Stream mstream = new MemoryStream();
        // сериализация делается через текстовый райтер 
        TextWriter tw = new StreamWriter(mstream);
        TextFlow.Serialize(tw, val2, tp2);
        tw.Flush();
        // посмотрим что записалось
        mstream.Position = 0L;
        TextReader tr = new StreamReader(mstream);
        string sss = tr.ReadToEnd();
        Console.WriteLine("Накопилось в стриме: " + sss);

        // десериализаця делатеся через текстовый ридер
        mstream.Position = 0L;
        object val = TextFlow.Deserialize(tr, tp2);
        // Теперь надо посмотреть что в объекте
        Console.WriteLine("После цикла сериализация/десериализация: " + tp2.Interpret(val));

        // Бинарная сериализация упаковывает структуры в подряд идущие байты по принципу: 
        // bool - 1 байт
        // byte - 1 байт
        // int - 4 байта
        // long, double - 8 байтов
        // строка - набор байтов определяемый BinaryWriter.Write((string)s)
        // запись - подряд стоящие сериализации полей записи
        // последовательность - long длина последовательности, подряд стоящие развертки элементов
        // 
        // Бинарная сериализация совместима с BinaryWriter и BinaryReader
        // Конкртеные тест отсутствует

        // Дальше в исходном файле начиналась тема UniversalSequenceBase.
        // Она должна переехать в отдельный тематический проект.
    }
}
