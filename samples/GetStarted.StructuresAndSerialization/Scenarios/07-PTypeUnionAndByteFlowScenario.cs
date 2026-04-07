using Polar.DB;

namespace GetStarted.StructuresAndSerialization.Scenarios;

internal sealed class PTypeUnionAndByteFlowScenario : ISampleScenario
{
    public string Id => "union-byteflow";
    public string Title => "PTypeUnion and ByteFlow round-trip";
    public string SourcePath => "Scenarios/07-PTypeUnionAndByteFlowScenario.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        var type = SampleGeometry.GeometrySceneType;
        var value = SampleGeometry.SampleScene();

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            ByteFlow.Serialize(writer, value, type);
            writer.Flush();
        }

        stream.Position = 0;
        object restored;
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
        {
            restored = ByteFlow.Deserialize(reader, type);
        }

        Console.WriteLine("Schema:");
        Console.WriteLine(PType.TType.Interpret(type.ToPObject(8)));
        Console.WriteLine();
        Console.WriteLine("Original value:");
        Console.WriteLine(type.Interpret(value, withfieldnames: true));
        Console.WriteLine();
        Console.WriteLine("Restored value after ByteFlow round-trip:");
        Console.WriteLine(type.Interpret(restored, withfieldnames: true));
    }
}
