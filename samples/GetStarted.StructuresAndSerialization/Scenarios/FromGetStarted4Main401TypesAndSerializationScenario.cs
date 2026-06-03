using Polar.DB;

namespace GetStarted.StructuresAndSerialization.Scenarios;

internal sealed class FromGetStarted4Main401TypesAndSerializationScenario : ISampleScenario
{
    public string Id => "gs4-401";
    public string Title => "Типы структур и сериализация (вариант Main401)";
    public string SourcePath => "samples/GetStarted4/Main401.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        Console.WriteLine("Start Main401");
        // ============ Типы структур и значения с труктур в объектном представлении ===========
        // Создадим типы записи и последовательности записей
        PType tp1 = new PTypeRecord(
            new NamedType("name", new PType(PTypeEnumeration.sstring)),
            new NamedType("age", new PType(PTypeEnumeration.integer)) );
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
        Stream stream = new MemoryStream();
        // сериализация делается через текстовый райтер 
        TextWriter tw = new StreamWriter(stream);
        TextFlow.Serialize(tw, val2, tp2);
        tw.Flush();
        // посмотрим что записалось
        stream.Position = 0L;
        TextReader tr = new StreamReader(stream);
        string sss = tr.ReadToEnd();
        Console.WriteLine("Накопилось в стриме: " + sss);

        // десериализаця делатеся через текстовый ридер
        stream.Position = 0L;
        object val = TextFlow.Deserialize(tr, tp2);
        // Теперь надо посмотреть что в объекте
        Console.WriteLine("После цикла сериализация/десериализация: " + tp2.Interpret(val));

        // Бинарная сериализация 
        // bool - 1 байт
        // byte - 1 байт
        // int - 4 байта
        // long, double - 8 байта
        // строка - набор байтов определяемый BinaryWriter.Write((string)s)
        // запись - подряд стоящие сериализации полей записи
        // последовательность - long длина последовательности, подряд стоящие развертки элементов
        // 
        // Бинарная сериализация совместима с BinaryWriter и BinaryReader

        // выполняется точно также, как текстовая сериализация (пример сделаю позже)
    }
}
