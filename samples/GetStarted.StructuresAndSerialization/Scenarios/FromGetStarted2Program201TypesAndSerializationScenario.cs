using Polar.DB;

namespace GetStarted.StructuresAndSerialization.Scenarios;

internal sealed class FromGetStarted2Program201TypesAndSerializationScenario : ISampleScenario
{
    public string Id => "gs2-201";
    public string Title => "Определение типов и цикл сериализация/десериализация";
    public string SourcePath => "samples/GetStarted2/Program201.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        Console.WriteLine("Start Program201"); 
        // Определение поляровских типов
        PType tp_rec = new PTypeRecord(
            new NamedType("имя", new PType(PTypeEnumeration.sstring)),
            new NamedType("возраст", new PType(PTypeEnumeration.integer)),
            new NamedType("мужчина", new PType(PTypeEnumeration.boolean)));
        PType tp_seq = new PTypeSequence(tp_rec);
        // По типу можно определить как будет текстово представляться структурное значение
        Console.WriteLine(tp_rec.Interpret(new object[] { "Пупкин", 22, true }));
        Console.WriteLine(tp_seq.Interpret(new object[] { 
            new object[] { "Пупкин", 22, true }, 
            new object[] { "Занкина", 33, false } }));

        // В объектном представлении последовательность реализуется как object[]
        // В текстовом представлении - как подряд идущие структуры в квадратных скобках, разделенные запятыми
        // (может быть и другой вариант, например XML, JSON и пр., главное, чтобы было взаимнооднозначно)

        // Сериализация выполняет отображение структуры на поток символов (текстовая сериализация) или  
        // поток байтов (бинарная сериализация). Десериализация выполняет обратное преобразование.
        object val2 = new object[]
        {
            new object[] { "Иванов", 22 },
            new object[] { "Петров", 33 },
            new object[] { "Сидоров", 44 }
        };
        PType tp2 = new PTypeSequence(
            new PTypeRecord(
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer))));
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
        // long, double - 8 байтов
        // строка - набор байтов определяемый BinaryWriter.Write((string)s)
        // запись - подряд стоящие сериализации полей записи
        // последовательность - long длина последовательности, подряд стоящие развертки элементов
        // 
        // Бинарная сериализация совместима с BinaryWriter и BinaryReader

        // выполняется точно также, как текстовая сериализация (пример сделаю позже)
    }
}
