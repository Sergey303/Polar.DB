//using PolarDB;

namespace Polar.DB
{
    public class ByteFlow
    {
        public static void Serialize(BinaryWriter bw, object v, PType tp)
        {
            switch (tp.Vid)
            {
                case PTypeEnumeration.none: { return; }
                case PTypeEnumeration.boolean: { bw.Write((bool)v); return; }
                case PTypeEnumeration.@byte: { bw.Write((byte)v); return; }
                case PTypeEnumeration.character: { bw.Write((char)v); return; }
                case PTypeEnumeration.integer: { bw.Write((int)v); return; }
                case PTypeEnumeration.longinteger: { bw.Write((long)v); return; }
                case PTypeEnumeration.real: { bw.Write((double)v); return; }
                case PTypeEnumeration.fstring:
                    {
                        var type = (PTypeFString)tp;
                        string value = (string)v;
                        if (value.Length > type.Length) throw new ArgumentException("Fixed string value exceeds declared length.", nameof(v));
                        for (int i = 0; i < type.Length; i++) bw.Write((ushort)(i < value.Length ? value[i] : '\0'));
                        return;
                    }
                case PTypeEnumeration.sstring: { if (v == null) v = ""; bw.Write((string)v); return; }
                case PTypeEnumeration.record:
                    {
                        object[] rec = (object[])v;
                        PTypeRecord tp_rec = (PTypeRecord)tp;
                        if (rec.Length != tp_rec.Fields.Length) throw new Exception("Err in Serialize: wrong record field number");
                        for (int i = 0; i < rec.Length; i++)
                        {
                            Serialize(bw, rec[i], tp_rec.Fields[i].Type);
                        }
                        return;
                    }
                case PTypeEnumeration.sequence:
                    {
                        PType tp_element = ((PTypeSequence)tp).ElementType;
                        object[] elements = (object[])v;
                        bw.Write((long)elements.Length);
                        foreach (object el in elements) Serialize(bw, el, tp_element);
                        return;
                    }
                case PTypeEnumeration.union:
                    {
                        PTypeUnion tp_uni = (PTypeUnion)tp;
                        int tag = (int)((object[])v)[0];
                        object subval = ((object[])v)[1];
                        if (tag < 0 || tag >= tp_uni.Variants.Length) throw new Exception("Err in Serialize: wrong union tag");
                        bw.Write((byte)tag);
                        Serialize(bw, subval, tp_uni.Variants[tag].Type);
                        return;
                    }
                default: throw new NotSupportedException($"Binary serialization does not support type {tp.Vid}.");
            }
        }

        public static object Deserialize(BinaryReader br, PType tp)
        {
            switch (tp.Vid)
            {
                case PTypeEnumeration.none: { return null; }
                case PTypeEnumeration.boolean: { return br.ReadBoolean(); }
                case PTypeEnumeration.@byte: { return br.ReadByte(); }
                case PTypeEnumeration.character: { return br.ReadChar(); }
                case PTypeEnumeration.integer: { return br.ReadInt32(); }
                case PTypeEnumeration.longinteger: { return br.ReadInt64(); }
                case PTypeEnumeration.real: { return br.ReadDouble(); }
                case PTypeEnumeration.fstring:
                    {
                        var type = (PTypeFString)tp;
                        var chars = new char[type.Length];
                        for (int i = 0; i < chars.Length; i++) chars[i] = (char)br.ReadUInt16();
                        return new string(chars).TrimEnd('\0');
                    }
                case PTypeEnumeration.sstring: { return br.ReadString(); }
                case PTypeEnumeration.record:
                    {
                        PTypeRecord tp_rec = (PTypeRecord)tp;
                        object[] rec = new object[tp_rec.Fields.Length];
                        for (int i = 0; i < rec.Length; i++)
                        {
                            object v = Deserialize(br, tp_rec.Fields[i].Type);
                            rec[i] = v;
                        }
                        return rec;
                    }
                case PTypeEnumeration.sequence:
                    {
                        PType tp_element = ((PTypeSequence)tp).ElementType;
                        long nelements = br.ReadInt64();
                        if (nelements < 0 || nelements > Int32.MaxValue) throw new Exception($"Err in Deserialize: sequense has too many ({nelements}) elements");
                        object[] elements = new object[nelements];
                        for (int i = 0; i < nelements; i++)
                        {
                            elements[i] = Deserialize(br, tp_element);
                        }
                        return elements;
                    }
                case PTypeEnumeration.union:
                    {
                        PTypeUnion tp_uni = (PTypeUnion)tp;
                        int tag = br.ReadByte();
                        object subval = Deserialize(br, tp_uni.Variants[tag].Type);
                        return new object[] { tag, subval };
                    }
                default: { throw new Exception($"Err in Deserialize: unknown type variant {tp.Vid}"); }
            }
        }

        /// <summary>
        /// Advances over one serialized value without materializing its object graph.
        /// The byte layout is exactly the same one written by <see cref="Serialize"/>.
        /// </summary>
        public static void Skip(BinaryReader br, PType tp)
        {
            if (br == null) throw new ArgumentNullException(nameof(br));
            if (tp == null) throw new ArgumentNullException(nameof(tp));

            switch (tp.Vid)
            {
                case PTypeEnumeration.none:
                    return;
                case PTypeEnumeration.boolean:
                case PTypeEnumeration.@byte:
                    SkipBytes(br, 1L);
                    return;
                case PTypeEnumeration.character:
                    // Polar.DB's existing character slot is the two-byte char value written
                    // by BinaryWriter.Write(char). Skipping raw bytes avoids invoking the
                    // BinaryReader text decoder and preserves the historical on-disk layout.
                    SkipBytes(br, sizeof(char));
                    return;
                case PTypeEnumeration.integer:
                    SkipBytes(br, sizeof(int));
                    return;
                case PTypeEnumeration.longinteger:
                case PTypeEnumeration.real:
                    SkipBytes(br, sizeof(long));
                    return;
                case PTypeEnumeration.fstring:
                    SkipBytes(br, checked((long)((PTypeFString)tp).Length * sizeof(ushort)));
                    return;
                case PTypeEnumeration.sstring:
                    SkipBytes(br, Read7BitEncodedStringByteCount(br));
                    return;
                case PTypeEnumeration.record:
                    foreach (var field in ((PTypeRecord)tp).Fields)
                        Skip(br, field.Type);
                    return;
                case PTypeEnumeration.sequence:
                    {
                        long count = br.ReadInt64();
                        if (count < 0L || count > Int32.MaxValue)
                            throw new InvalidDataException($"Serialized sequence has invalid element count {count}.");

                        var elementType = ((PTypeSequence)tp).ElementType;
                        for (long i = 0; i < count; i++)
                            Skip(br, elementType);
                        return;
                    }
                case PTypeEnumeration.union:
                    {
                        var union = (PTypeUnion)tp;
                        int tag = br.ReadByte();
                        if (tag < 0 || tag >= union.Variants.Length)
                            throw new InvalidDataException($"Serialized union has invalid tag {tag}.");
                        Skip(br, union.Variants[tag].Type);
                        return;
                    }
                default:
                    throw new NotSupportedException($"Binary serialization does not support type {tp.Vid}.");
            }
        }

        private static int Read7BitEncodedStringByteCount(BinaryReader br)
        {
            uint value = 0U;
            for (var shift = 0; shift < 28; shift += 7)
            {
                byte current = br.ReadByte();
                value |= (uint)(current & 0x7F) << shift;
                if ((current & 0x80) == 0)
                    return checked((int)value);
            }

            byte last = br.ReadByte();
            if (last > 0x07)
                throw new InvalidDataException("Serialized string has an invalid 7-bit encoded byte length.");

            value |= (uint)last << 28;
            return checked((int)value);
        }

        private static void SkipBytes(BinaryReader br, long count)
        {
            if (count < 0L) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0L) return;

            Stream stream = br.BaseStream;
            if (stream.CanSeek)
            {
                long remaining = stream.Length - stream.Position;
                if (remaining < count)
                    throw new EndOfStreamException();
                stream.Seek(count, SeekOrigin.Current);
                return;
            }

            var buffer = new byte[4096];
            long remainingBytes = count;
            while (remainingBytes > 0L)
            {
                int requested = (int)Math.Min(buffer.Length, remainingBytes);
                int read = br.Read(buffer, 0, requested);
                if (read <= 0)
                    throw new EndOfStreamException();
                remainingBytes -= read;
            }
        }
    }
}
