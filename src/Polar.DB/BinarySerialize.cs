namespace Polar.DB
{
    /// <summary>
    /// Binary serializer/deserializer for values described by <see cref="PType"/> schemas.
    /// </summary>
    /// <remarks>
    /// Record and sequence values are represented as <c>object[]</c> in schema order.
    /// Union values are represented as two-item arrays: <c>[tag, payload]</c>.
    /// </remarks>
    public class ByteFlow
    {
        /// <summary>
        /// Writes a value to a binary writer according to the provided schema.
        /// </summary>
        /// <param name="bw">Target binary writer.</param>
        /// <param name="v">Value to serialize.</param>
        /// <param name="tp">Schema describing value shape.</param>
        public static void Serialize(BinaryWriter bw, object v, PType tp)
        {
            switch (tp.Vid)
            {
                case PTypeEnumeration.none:
                    return;
                case PTypeEnumeration.boolean:
                    bw.Write((bool)v);
                    return;
                case PTypeEnumeration.@byte:
                    bw.Write((byte)v);
                    return;
                case PTypeEnumeration.character:
                    bw.Write((char)v);
                    return;
                case PTypeEnumeration.integer:
                    bw.Write((int)v);
                    return;
                case PTypeEnumeration.longinteger:
                    bw.Write((long)v);
                    return;
                case PTypeEnumeration.real:
                    bw.Write((double)v);
                    return;
                case PTypeEnumeration.sstring:
                    if (v == null) v = string.Empty;
                    bw.Write((string)v);
                    return;
                case PTypeEnumeration.record:
                {
                    object[] rec = (object[])v;
                    PTypeRecord tpRec = (PTypeRecord)tp;
                    if (rec.Length != tpRec.Fields.Length)
                        throw new Exception("Err in Serialize: wrong record field number");

                    for (int i = 0; i < rec.Length; i++)
                    {
                        Serialize(bw, rec[i], tpRec.Fields[i].Type);
                    }

                    return;
                }
                case PTypeEnumeration.sequence:
                {
                    PType tpElement = ((PTypeSequence)tp).ElementType;
                    object[] elements = (object[])v;
                    bw.Write((long)elements.Length);
                    foreach (object el in elements)
                    {
                        Serialize(bw, el, tpElement);
                    }

                    return;
                }
                case PTypeEnumeration.union:
                {
                    PTypeUnion tpUni = (PTypeUnion)tp;
                    int tag = (int)((object[])v)[0];
                    object subval = ((object[])v)[1];
                    if (tag < 0 || tag >= tpUni.Variants.Length)
                        throw new Exception("Err in Serialize: wrong union tag");

                    bw.Write((byte)tag);
                    Serialize(bw, subval, tpUni.Variants[tag].Type);
                    return;
                }
            }
        }

        /// <summary>
        /// Reads a value from a binary reader according to the provided schema.
        /// </summary>
        /// <param name="br">Source binary reader.</param>
        /// <param name="tp">Schema describing expected value shape.</param>
        /// <returns>Deserialized value in Polar object representation.</returns>
        public static object Deserialize(BinaryReader br, PType tp)
        {
            switch (tp.Vid)
            {
                case PTypeEnumeration.none:
                    return null!;
                case PTypeEnumeration.boolean:
                    return br.ReadBoolean();
                case PTypeEnumeration.@byte:
                    return br.ReadByte();
                case PTypeEnumeration.character:
                    return br.ReadChar();
                case PTypeEnumeration.integer:
                    return br.ReadInt32();
                case PTypeEnumeration.longinteger:
                    return br.ReadInt64();
                case PTypeEnumeration.real:
                    return br.ReadDouble();
                case PTypeEnumeration.sstring:
                    return br.ReadString();
                case PTypeEnumeration.record:
                {
                    PTypeRecord tpRec = (PTypeRecord)tp;
                    object[] rec = new object[tpRec.Fields.Length];
                    for (int i = 0; i < rec.Length; i++)
                    {
                        object v = Deserialize(br, tpRec.Fields[i].Type);
                        rec[i] = v;
                    }

                    return rec;
                }
                case PTypeEnumeration.sequence:
                {
                    PType tpElement = ((PTypeSequence)tp).ElementType;
                    long nelements = br.ReadInt64();
                    if (nelements < 0 || nelements > int.MaxValue)
                        throw new Exception($"Err in Deserialize: sequense has too many ({nelements}) elements");

                    object[] elements = new object[nelements];
                    for (int i = 0; i < nelements; i++)
                    {
                        elements[i] = Deserialize(br, tpElement);
                    }

                    return elements;
                }
                case PTypeEnumeration.union:
                {
                    PTypeUnion tpUni = (PTypeUnion)tp;
                    int tag = br.ReadByte();
                    object subval = Deserialize(br, tpUni.Variants[tag].Type);
                    return new object[] { tag, subval };
                }
                default:
                    throw new Exception($"Err in Deserialize: unknown type variant {tp.Vid}");
            }
        }
    }
}
