using System.Text;

namespace Polar.DB
{
    /// <summary>
    /// Text serializer/deserializer for values described by <see cref="PType"/> schemas.
    /// </summary>
    /// <remarks>
    /// The format is Polar-specific and uses <c>object[]</c> for records/sequences and <c>tag^payload</c> for unions.
    /// It is intended for diagnostics and interchange with the matching parser in this class.
    /// </remarks>
    public class TextFlow
    {
        /// <summary>
        /// Serializes a single value using compact Polar text syntax.
        /// </summary>
        /// <param name="tw">Target writer.</param>
        /// <param name="v">Value to serialize.</param>
        /// <param name="tp">Schema that defines the value shape.</param>
        public static void Serialize(TextWriter tw, object v, PType tp)
        {
            _ = tw ?? throw new ArgumentNullException(nameof(tw));
            _ = tp ?? throw new ArgumentNullException(nameof(tp));
            _ = v ?? throw new ArgumentNullException(nameof(v));


            switch (tp.Vid)
            {
                case PTypeEnumeration.none: { return; }
                case PTypeEnumeration.boolean: { tw.Write((bool)v?'t':'f'); return; }
                case PTypeEnumeration.@byte: { tw.Write(((byte)v).ToString()); return; }
                case PTypeEnumeration.character: { tw.Write((char)v); return; }
                case PTypeEnumeration.integer: { tw.Write((int)v); return; }
                case PTypeEnumeration.longinteger: { tw.Write((long)v); return; }
                case PTypeEnumeration.real: { tw.Write(((double)v).ToString("G", System.Globalization.CultureInfo.InvariantCulture)); return; }
                case PTypeEnumeration.sstring:
                    {
                        tw.Write('\"');
                        tw.Write(((string)v).Replace("\\", "\\\\").Replace("\"", "\\\""));
                        tw.Write('\"');
                        return;
                    }
                case PTypeEnumeration.record:
                    {
                        object[] rec = (object[])v;
                        PTypeRecord tp_rec = (PTypeRecord)tp;
                        if (rec.Length != tp_rec.Fields.Length) throw new Exception("Err in Serialize: wrong record field number");
                        tw.Write('{');
                        for (int i = 0; i < rec.Length; i++)
                        {
                            if (i != 0) tw.Write(',');
                            Serialize(tw, rec[i], tp_rec.Fields[i].Type);
                        }
                        tw.Write('}');
                        return;
                    }
                case PTypeEnumeration.sequence:
                    {
                        PType tp_element = ((PTypeSequence)tp).ElementType;
                        object[] elements = (object[])v;
                        tw.Write('[');
                        bool isfirst = true;
                        foreach (object el in elements)
                        {
                            if (!isfirst) tw.Write(','); isfirst = false;
                            Serialize(tw, el, tp_element);
                        }
                        tw.Write(']');
                        return;
                    }
                case PTypeEnumeration.union:
                    {
                        PTypeUnion tp_uni = (PTypeUnion)tp;
                        // тег - 1 байт
                        int tag = (int)((object[])v)[0];
                        object subval = ((object[])v)[1];
                        if (tag < 0 || tag >= tp_uni.Variants.Length) throw new Exception("Err in Serialize: wrong union tag");
                        tw.Write(tag);
                        tw.Write('^');
                        Serialize(tw, subval, tp_uni.Variants[tag].Type);
                        return;
                    }
            }
        }

        private static int intend = 4;

        private static void Intend(TextWriter tw, int nspaces)
        {
            _ = tw ?? throw new ArgumentNullException(nameof(tw));
            tw.Write('\n');
            for (int i = 0; i < nspaces; i++) tw.Write(' ');
        }

        private static bool IsSimple(PType tp)
        {
            _ = tp ?? throw new ArgumentNullException(nameof(tp));

            if (tp.IsAtom || tp.Vid == PTypeEnumeration.sstring) return true;

            if (tp.Vid == PTypeEnumeration.record)
            {
                PTypeRecord rec = (PTypeRecord)tp;
                bool simple = true;
                for (int i = 0; i < rec.Fields.Length; i++)
                {
                    var t = rec.Fields[i].Type;
                    if (!(t.IsAtom || t.Vid == PTypeEnumeration.sstring))
                    {
                        simple = false;
                        break;
                    }
                }
                if (simple) return true;
            }

            return false;
        }

        /// <summary>
        /// Serializes a single value with indentation and line breaks.
        /// </summary>
        /// <param name="tw">Target writer.</param>
        /// <param name="v">Value to serialize.</param>
        /// <param name="tp">Schema that defines the value shape.</param>
        /// <param name="level">Current indentation level used as a left margin.</param>
        public static void SerializeFormatted(TextWriter tw, object v, PType tp, int level)
        {
            _ = tw ?? throw new ArgumentNullException(nameof(tw));
            _ = tp ?? throw new ArgumentNullException(nameof(tp));
            _ = v ?? throw new ArgumentNullException(nameof(v));

            Intend(tw, level * intend);

            switch (tp.Vid)
            {
                case PTypeEnumeration.none: { return; }
                case PTypeEnumeration.boolean: { tw.Write((bool)v ? 't' : 'f'); return; }
                case PTypeEnumeration.@byte: { tw.Write(((byte)v).ToString()); return; }
                case PTypeEnumeration.character: { tw.Write((char)v); return; }
                case PTypeEnumeration.integer: { tw.Write((int)v); return; }
                case PTypeEnumeration.longinteger: { tw.Write((long)v); return; }
                case PTypeEnumeration.real: { tw.Write(((double)v).ToString("G", System.Globalization.CultureInfo.InvariantCulture)); return; }
                case PTypeEnumeration.sstring:
                    {
                        tw.Write('\"');
                        tw.Write(((string)v).Replace("\\", "\\\\").Replace("\"", "\\\""));
                        tw.Write('\"');
                        return;
                    }
                case PTypeEnumeration.record:
                    {
                        object[] rec = (object[])v;
                        PTypeRecord tp_rec = (PTypeRecord)tp;
                        if (rec.Length != tp_rec.Fields.Length) throw new Exception("Err in Serialize: wrong record field number");
                        bool simple = IsSimple(tp);
                        if (simple) { Serialize(tw, v, tp); return; }
                        tw.Write('{');
                        for (int i = 0; i < rec.Length; i++)
                        {
                            if (i != 0) tw.Write(',');
                            SerializeFormatted(tw, rec[i], tp_rec.Fields[i].Type, level+1);
                        }

                        Intend(tw, level * intend);
                        tw.Write('}');
                        return;
                    }
                case PTypeEnumeration.sequence:
                    {
                        PType tp_element = ((PTypeSequence)tp).ElementType;
                        object[] elements = (object[])v;
                        tw.Write('[');
                        bool isfirst = true;
                        foreach (object el in elements)
                        {
                            if (!isfirst) tw.Write(','); isfirst = false;
                            SerializeFormatted(tw, el, tp_element, level+1);
                        }
                        Intend(tw, level * intend);
                        tw.Write(']');
                        return;
                    }
                case PTypeEnumeration.union:
                    {
                        PTypeUnion tp_uni = (PTypeUnion)tp;
                        // тег - 1 байт
                        int tag = (int)((object[])v)[0];
                        object subval = ((object[])v)[1];
                        if (tag < 0 || tag >= tp_uni.Variants.Length) throw new Exception("Err in Serialize: wrong union tag");
                        tw.Write(tag);
                        tw.Write('^');
                        if (IsSimple(tp_uni.Variants[tag].Type))
                        {
                            Serialize(tw, subval, tp_uni.Variants[tag].Type);
                            return;
                        }
                        SerializeFormatted(tw, subval, tp_uni.Variants[tag].Type, level+1);
                        return;
                    }
            }
        }
        /// <summary>
        /// Serializes a flow of elements as a Polar sequence literal.
        /// </summary>
        /// <param name="tw">Target writer.</param>
        /// <param name="flow">Element flow to serialize.</param>
        /// <param name="tp">Element schema.</param>
        public static void SerializeFlowToSequense(TextWriter tw, IEnumerable<object> flow, PType tp)
        {
            _ = tw ?? throw new ArgumentNullException(nameof(tw));
            _ = flow ?? throw new ArgumentNullException(nameof(flow));
            _ = tp ?? throw new ArgumentNullException(nameof(tp));

            tw.Write('[');
            bool ft = true;
            foreach (object ob in flow)
            {
                if (!ft) tw.Write(',');
                ft = false;
                Serialize(tw, ob, tp);
            }
            tw.Write(']');
        }

        /// <summary>
        /// Serializes a flow of elements as a formatted Polar sequence literal.
        /// </summary>
        /// <param name="tw">Target writer.</param>
        /// <param name="flow">Element flow to serialize.</param>
        /// <param name="tp">Element schema.</param>
        /// <param name="level">Current indentation level used as a left margin.</param>
        public static void SerializeFlowToSequenseFormatted(TextWriter tw, IEnumerable<object> flow, PType tp, int level)
        {
            _ = tw ?? throw new ArgumentNullException(nameof(tw));
            _ = flow ?? throw new ArgumentNullException(nameof(flow));
            _ = tp ?? throw new ArgumentNullException(nameof(tp));

            tw.Write('\n');
            for (int i = 0; i < level * intend; i++) tw.Write(' ');
            tw.Write('[');

            bool ft = true;
            foreach (object ob in flow)
            {
                if (!ft) tw.Write(',');
                ft = false;
                SerializeFormatted(tw, ob, tp, level + 1);
            }
            tw.Write('\n'); for (int i = 0; i < level * intend; i++) tw.Write(' ');
            tw.Write(']');
            tw.Flush();

        }

        /// <summary>
        /// Deserializes a single value from Polar text syntax.
        /// </summary>
        /// <param name="tr">Source reader.</param>
        /// <param name="tp">Expected schema.</param>
        /// <returns>Deserialized value in Polar object representation.</returns>
        public static object Deserialize(TextReader tr, PType tp)
        {
            _ = tr ?? throw new ArgumentNullException(nameof(tr));
            _ = tp ?? throw new ArgumentNullException(nameof(tp));

            TextFlow tf = new TextFlow(tr);
            tf.Skip();
            return tf.Des(tp)!;
        }

        /// <summary>
        /// Deserializes a sequence literal into an element flow.
        /// </summary>
        /// <param name="tr">Source reader.</param>
        /// <param name="tp">Element schema.</param>
        /// <returns>Lazy stream of deserialized elements.</returns>
        public static IEnumerable<object> DeserializeSequenseToFlow(TextReader tr, PType tp)
        {
            _ = tr ?? throw new ArgumentNullException(nameof(tr));
            _ = tp ?? throw new ArgumentNullException(nameof(tp));

            TextFlow tf = new TextFlow(tr);
            tf.Skip();
            char c = tf.ReadChar();
            if (c != '[') throw new Exception("Err in DeserializeSequenseToFlow");

            bool firsttime = true;
            while (true)
            {
                tf.Skip();

                // выхожу по закрывающей скобке
                if (firsttime && tr.Peek() == ']') { c = (char)tr.Read(); break; }
                firsttime = false;
                yield return tf.Des(tp);
                tf.Skip();
                c = (char)tr.Read();

                if (c == ']') break;
                if (c == ',') continue;

                throw new Exception("Polar syntax error 19333");
            }
        }

        // Более удобный объект для парсинга TextFlow
        private TextReader tr;

        internal TextFlow(TextReader tr)
        {
            this.tr = tr ?? throw new ArgumentNullException(nameof(tr));
        }

        /// <summary>
        /// Advances the reader over whitespace characters.
        /// </summary>
        public void Skip()
        {
            while (char.IsWhiteSpace((char)tr.Peek())) tr.Read();
        }

        /// <summary>
        /// Reads a boolean token encoded as <c>t</c> or <c>f</c>.
        /// </summary>
        public bool ReadBoolean()
        {
            int c = tr.Read();
            return c == 't';
        }

        private string ReadWhile(Func<char, bool> yesFunc)
        {
            _ = yesFunc ?? throw new ArgumentNullException(nameof(yesFunc));

            StringBuilder sb = new StringBuilder();
            char c;
            while (yesFunc(c = (char)tr.Peek()))
            {
                c = (char)tr.Read();
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Reads a byte token in decimal or hexadecimal-like digit form.
        /// </summary>
        public byte ReadByte()
        {
            string s = ReadWhile(c => { if (char.IsDigit(c)) return true; char cc = char.ToLower(c); return cc >= 'a' && cc <= 'f'; });
            return byte.Parse(s);
        }
        /// <summary>
        /// Reads a single character from the stream.
        /// </summary>
        public char ReadChar() { return (char)tr.Read(); }

        /// <summary>
        /// Reads a signed 32-bit integer token.
        /// </summary>
        public int ReadInt32()
        {
            int sign = 1;
            if (tr.Peek() == '-') { sign = -1; tr.Read(); }
            string s = ReadWhile(c => c >= '0' && c <= '9');
            int v = Int32.Parse(s);
            return sign * v;
        }

        /// <summary>
        /// Reads a signed 64-bit integer token.
        /// </summary>
        public long ReadInt64()
        {
            int sign = 1;
            if (tr.Peek() == '-') { sign = -1; tr.Read(); }
            string s = ReadWhile(c => c >= '0' && c <= '9');
            long v = Int64.Parse(s);
            return sign * v;
        }

        /// <summary>
        /// Reads a floating-point token using invariant culture conventions.
        /// </summary>
        public double ReadDouble()
        {
            // Наверное, это неправильно, но пока сойдет
            string s = ReadWhile(c => (c >= '0' && c <= '9') || c == '-' || c == 'e' || c == '.');
            double v = double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
            return v;
        }

        /// <summary>
        /// Reads a quoted Polar string token with escape-sequence support.
        /// </summary>
        public string ReadString()
        {
            StringBuilder sb = new StringBuilder();

            // Маленький конечный автомат
            // начальная точка, сюда уже не вернемся
            if (tr.Peek() != '\"') throw new Exception("Err: wrong string construction");
            int c = tr.Read();

            // Внутри строки очередной символ прочитан
            c = tr.Read();
            while (c != '\"')
            {
                if (c == '\\')
                {
                    c = tr.Read();
                    if (c == 'n') sb.Append('\n');
                    else if (c == 'r') sb.Append('\r');
                    else if (c == 't') sb.Append('\t');
                    else
                    {
                        sb.Append((char)c);
                    }
                }
                else
                {
                    sb.Append((char)c);
                }

                c = tr.Read();
            }

            return sb.ToString();
        }

        private object Des(PType tp)
        {
            _ = tp ?? throw new ArgumentNullException(nameof(tp));

            switch (tp.Vid)
            {
                case PTypeEnumeration.none: { return PType.NoneValue; }
                case PTypeEnumeration.boolean: { return ReadBoolean(); }
                case PTypeEnumeration.@byte: { return ReadByte(); }
                case PTypeEnumeration.character: { return ReadChar(); }
                case PTypeEnumeration.integer: { return ReadInt32(); }
                case PTypeEnumeration.longinteger: { return ReadInt64(); }
                case PTypeEnumeration.real: { return ReadDouble(); }
                case PTypeEnumeration.sstring: { return ReadString(); }
                case PTypeEnumeration.record:
                    {
                        PTypeRecord tp_rec = (PTypeRecord)tp;
                        object[] rec = new object[tp_rec.Fields.Length];
                        char c = (char)tr.Read();
                        if (c != '{') throw new Exception("Polar syntax error 19327");
                        for (int i = 0; i < rec.Length; i++)
                        {
                            Skip();
                            object v = Des(tp_rec.Fields[i].Type);
                            rec[i] = v;
                            if (i < rec.Length - 1)
                            {
                                Skip();
                                c = (char)tr.Read();
                                if (c != ',') throw new Exception("Polar syntax error 19329");
                            }
                            Skip();
                        }
                        c = (char)tr.Read();
                        if (c != '}') throw new Exception("Polar syntax error 19328");
                        return rec;
                    }
                case PTypeEnumeration.sequence:
                    {
                        PType tp_element = ((PTypeSequence)tp).ElementType;
                        List<object> lsequ = new List<object>();
                        char c = (char)tr.Read();
                        if (c != '[') throw new Exception("Polar syntax error 19331");
                        bool firsttime = true;
                        while (true)
                        {
                            Skip();
                            //TODO: неудачно, что дважды проверяю и выхожу по закрывающей скобке
                            if (firsttime && tr.Peek() == ']') { c = (char)tr.Read(); break; }
                            firsttime = false;
                            lsequ.Add(Des(tp_element));
                            Skip();
                            c = (char)tr.Read();
                            if (c == ']') break;
                            else if (c == ',') continue;
                            throw new Exception("Polar syntax error 19333");
                        }
                        if (c != ']') throw new Exception("Polar syntax error 19332");
                        object[] elements = lsequ.ToArray();
                        return elements;
                    }
                case PTypeEnumeration.union:
                    {
                        PTypeUnion tp_uni = (PTypeUnion)tp;
                        // тег - 1 байт
                        int tag = ReadInt32();
                        Skip(); int c = tr.Read(); if (c != '^') throw new Exception("Polar syntax error 19335");
                        Skip();
                        object subval = Des(tp_uni.Variants[tag].Type);
                        return new object[] { tag, subval };
                    }
                default: { throw new Exception($"Err in Deserialize: unknown type variant {tp.Vid}"); }
            }
        }
    }
}
