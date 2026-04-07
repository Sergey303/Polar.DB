using Xunit;

namespace Polar.DB.Tests
{

    public class TestPType
    {
        PType tp_rec = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)),
            new NamedType("age", new PType(PTypeEnumeration.real)));
        [Fact]
        public void TestPTypeInterpret()
        {
            object[] orec = new object[] { 777, "Pupkin", 9.9999 };
            string val = tp_rec.Interpret(orec);
            Assert.Equal(val, "{777,\"Pupkin\",9.9999}");
        }
        [Fact]
        public void TestPTypeToPObject()
        {
            object otype = tp_rec.ToPObject(3);
            string val = PType.TType.Interpret(otype);
            Assert.Equal(val, "record^[{\"id\",integer^},{\"name\",sstring^},{\"age\",real^}]");
        }
        [Fact]
        public void TestPTypeFromPObject()
        {
            object otype = tp_rec.ToPObject(3);
            PType tp = PType.FromPObject(otype);
            string val = tp.Interpret(new object[] { 777, "Pupkin", 9.9999 });
            Assert.Equal(val, "{777,\"Pupkin\",9.9999}");
        }
        [Fact]
        public void TestScale()
        {
            int[] arr1 = Enumerable.Range(0, 160).ToArray();
            var scale_fun = Scale.GetDiaFunc32(arr1);
            int index = 81;
            Diapason dia = scale_fun(index);
            Assert.True(dia.start <= index && dia.start + dia.numb > index , "" + index + " in " + dia.start + " " + dia.numb);
        }
        [Fact]
        public void TestTextFlowSerializeDeserialize()
        {
            MemoryStream stream = new MemoryStream();
            TextWriter tw = new StreamWriter(stream);
            TextFlow.Serialize(tw, new object[] { 777, "Pupkin", 9.9999 }, tp_rec);
            tw.Flush();

            byte[] bytes = stream.ToArray();
            string res = new string(bytes.Select(b => System.Convert.ToChar(b)).ToArray());
            Assert.Equal(res, "{777,\"Pupkin\",9.9999}");

            TextReader tr = new StreamReader(stream);
            stream.Position = 0L;
            object oval = TextFlow.Deserialize(tr, tp_rec);
            string val = tp_rec.Interpret(oval);
            Assert.Equal(val, "{777,\"Pupkin\",9.9999}");
        }
        [Fact]
        public void TestBinarySerialize()
        {
            MemoryStream mem = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(mem);
            BinaryReader br = new BinaryReader(mem);

            ByteFlow.Serialize(bw, new object[] { 777, "Pupkin", 9.9999 }, tp_rec);
            bw.Flush();
            mem.Position = 0L;
            object oval = ByteFlow.Deserialize(br, tp_rec);
            string val = tp_rec.Interpret(oval);
            Assert.Equal(val, "{777,\"Pupkin\",9.9999}");
        }
    }
}
