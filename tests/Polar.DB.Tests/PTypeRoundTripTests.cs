using Xunit;

namespace Polar.DB.Tests
{
    public class PTypeRoundTripTests
    {
        private static PType RoundTrip(PType type)
        {
            object pobject = type.ToPObject(3);
            return PType.FromPObject(pobject);
        }

        private static void AssertRoundTripInterpretIsStable(PType type)
        {
            object pobject = type.ToPObject(3);
            string before = PType.TType.Interpret(pobject);

            PType restored = PType.FromPObject(pobject);
            object restoredObject = restored.ToPObject(3);
            string after = PType.TType.Interpret(restoredObject);

            Assert.Equal(before, after);
        }

        [Fact]
        public void FixedString_RoundTrip_Preserves_Size()
        {
            var tp = new PTypeFString(16);

            object pobject = tp.ToPObject(3);
            string before = PType.TType.Interpret(pobject);
            PType restored = PType.FromPObject(pobject);
            string after = PType.TType.Interpret(restored.ToPObject(3));

            Assert.Contains("fstring", before);
            Assert.Contains("16", before);
            Assert.Equal(before, after);
        }

        [Fact]
        public void Sequence_With_Growing_RoundTrip_Preserves_Growing()
        {
            var tp = new PTypeSequence(new PType(PTypeEnumeration.integer), true);

            object pobject = tp.ToPObject(3);
            string before = PType.TType.Interpret(pobject);
            PType restored = PType.FromPObject(pobject);
            string after = PType.TType.Interpret(restored.ToPObject(3));

            Assert.Contains("sequence", before);
            Assert.Equal(before, after);
        }

        [Fact]
        public void Union_RoundTrip_Preserves_Cases()
        {
            var tp = new PTypeUnion(
                new NamedType("asInt", new PType(PTypeEnumeration.integer)),
                new NamedType("asText", new PType(PTypeEnumeration.sstring)),
                new NamedType("asReal", new PType(PTypeEnumeration.real)));

            object pobject = tp.ToPObject(3);
            string before = PType.TType.Interpret(pobject);
            PType restored = PType.FromPObject(pobject);
            string after = PType.TType.Interpret(restored.ToPObject(3));

            Assert.Contains("union", before);
            Assert.Contains("asInt", before);
            Assert.Contains("asText", before);
            Assert.Contains("asReal", before);
            Assert.Equal(before, after);
        }

        [Fact]
        public void Record_RoundTrip_Preserves_Field_Names_And_Types()
        {
            var tp = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.real)));

            object pobject = tp.ToPObject(3);
            string before = PType.TType.Interpret(pobject);
            PType restored = PType.FromPObject(pobject);
            string after = PType.TType.Interpret(restored.ToPObject(3));

            Assert.Equal("record^[{\"id\",integer^},{\"name\",sstring^},{\"age\",real^}]", before);
            Assert.Equal(before, after);
        }

        [Fact]
        public void Sequence_Of_Record_RoundTrip_Preserves_Element_Schema_And_Growing()
        {
            var recordType = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            var tp = new PTypeSequence(recordType, true);

            AssertRoundTripInterpretIsStable(tp);
        }

        [Fact]
        public void Nested_Type_RoundTrip_Preserves_Full_Structure()
        {
            var unionType = new PTypeUnion(
                new NamedType("code", new PType(PTypeEnumeration.integer)),
                new NamedType("text", new PTypeFString(12)));

            var recordType = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.longinteger)),
                new NamedType("payload", unionType),
                new NamedType("items", new PTypeSequence(new PType(PTypeEnumeration.integer), true)));

            AssertRoundTripInterpretIsStable(recordType);
        }
    }
}
