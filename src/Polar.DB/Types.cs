using System.Globalization;
using System.Text;

namespace Polar.DB
{
    /// <summary>
    /// Kind of a Polar schema node used by <see cref="PType"/> and derived descriptors.
    /// </summary>
    public enum PTypeEnumeration
    {
        /// <summary>No payload value.</summary>
        none,

        /// <summary>Boolean value.</summary>
        boolean,

        /// <summary>UTF-16 character value.</summary>
        character,

        /// <summary>32-bit signed integer value.</summary>
        integer,

        /// <summary>64-bit signed integer value.</summary>
        longinteger,

        /// <summary>Double-precision floating-point value.</summary>
        real,

        /// <summary>Fixed-length UTF-16 string represented by <see cref="PTypeFString"/>.</summary>
        fstring,

        /// <summary>Variable-length string.</summary>
        sstring,

        /// <summary>Record value represented as <c>object[]</c> in field order.</summary>
        record,

        /// <summary>Sequence value represented as <c>object[]</c>.</summary>
        sequence,

        /// <summary>Tagged union value represented as <c>[tag, payload]</c>.</summary>
        union,

        /// <summary>Single byte value.</summary>
        @byte,

        /// <summary>Object pair marker used by legacy formats.</summary>
        objPair
    }

    /// <summary>
    /// Base schema descriptor for Polar values.
    /// </summary>
    public class PType
    {
        /// <summary>
        /// Creates a schema descriptor for the specified kind.
        /// </summary>
        /// <param name="vid">Schema kind.</param>
        public PType(PTypeEnumeration vid)
        {
            this.vid = vid;
        }

        private readonly PTypeEnumeration vid;

        /// <summary>
        /// Gets schema kind.
        /// </summary>
        public PTypeEnumeration Vid => vid;

        /// <summary>
        /// Performs deferred schema translation for nested descriptors.
        /// </summary>
        /// <remarks>
        /// Derived classes use this method to precompute fixed header sizes and validate nested definitions.
        /// </remarks>
        public virtual void Translate() { }

        /// <summary>
        /// Gets fixed-size header length in bytes for this schema, or <c>-1</c> when not fixed.
        /// </summary>
        public virtual int HeadSize
        {
            get
            {
                switch (Vid)
                {
                    case PTypeEnumeration.none: return 0;
                    case PTypeEnumeration.boolean: return 1;
                    case PTypeEnumeration.character: return 2;
                    case PTypeEnumeration.integer: return 4;
                    case PTypeEnumeration.longinteger: return 8;
                    case PTypeEnumeration.real: return 8;
                    case PTypeEnumeration.fstring: return ((PTypeFString)this).Size;
                    case PTypeEnumeration.sstring: return 12;
                    case PTypeEnumeration.sequence: return 24;
                    case PTypeEnumeration.union: return 9;
                    case PTypeEnumeration.@byte: return 1;
                    case PTypeEnumeration.objPair: return 16;
                    default: return -1;
                }
            }
        }

        /// <summary>
        /// Gets whether schema is atomic (no nested shape).
        /// </summary>
        public bool IsAtom
        {
            get
            {
                switch (Vid)
                {
                    case PTypeEnumeration.none:
                    case PTypeEnumeration.boolean:
                    case PTypeEnumeration.character:
                    case PTypeEnumeration.integer:
                    case PTypeEnumeration.longinteger:
                    case PTypeEnumeration.real:
                    case PTypeEnumeration.@byte:
                        return true;
                    case PTypeEnumeration.fstring:
                    case PTypeEnumeration.sstring:
                    case PTypeEnumeration.record:
                    case PTypeEnumeration.sequence:
                    case PTypeEnumeration.union:
                    case PTypeEnumeration.objPair:
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets whether values of this schema never have a variable-length tail.
        /// </summary>
        public bool HasNoTail
        {
            get
            {
                switch (Vid)
                {
                    case PTypeEnumeration.none:
                    case PTypeEnumeration.boolean:
                    case PTypeEnumeration.character:
                    case PTypeEnumeration.integer:
                    case PTypeEnumeration.longinteger:
                    case PTypeEnumeration.real:
                    case PTypeEnumeration.fstring:
                    case PTypeEnumeration.@byte:
                        return true;
                    case PTypeEnumeration.sstring:
                    case PTypeEnumeration.sequence:
                    case PTypeEnumeration.union:
                    case PTypeEnumeration.objPair:
                        return false;
                    case PTypeEnumeration.record:
                        return ((PTypeRecord)this).Fields.Select(pair => pair.Type).All(tp => tp.HasNoTail);
                    default:
                        return false;
                }
            }
        }

        private static int ToInt(PTypeEnumeration pte)
        {
            switch (pte)
            {
                case PTypeEnumeration.none: return 0;
                case PTypeEnumeration.boolean: return 1;
                case PTypeEnumeration.character: return 2;
                case PTypeEnumeration.integer: return 3;
                case PTypeEnumeration.longinteger: return 4;
                case PTypeEnumeration.real: return 5;
                case PTypeEnumeration.fstring: return 6;
                case PTypeEnumeration.sstring: return 7;
                case PTypeEnumeration.record: return 8;
                case PTypeEnumeration.sequence: return 9;
                case PTypeEnumeration.union: return 10;
                case PTypeEnumeration.@byte: return 11;
                case PTypeEnumeration.objPair: return 12;
                default: return -1;
            }
        }

        /// <summary>
        /// Converts schema to a compact Polar object representation.
        /// </summary>
        /// <param name="level">Recursion depth limit; negative value returns <see langword="null"/>.</param>
        /// <returns>Compact schema representation compatible with <see cref="FromPObject(object)"/>.</returns>
        public object ToPObject(int level)
        {
            if (level < 0) return null!;

            switch (vid)
            {
                case PTypeEnumeration.fstring:
                    return new object[] { ToInt(vid), ((PTypeFString)this).Length };

                case PTypeEnumeration.record:
                {
                    PTypeRecord ptr = (PTypeRecord)this;
                    var query = ptr.Fields
                        .Select(pair => new object[] { pair.Name, pair.Type.ToPObject(level - 1)! })
                        .ToArray();
                    return new object[] { ToInt(vid), query };
                }

                case PTypeEnumeration.sequence:
                {
                    PTypeSequence pts = (PTypeSequence)this;
                    return new object[]
                    {
                        ToInt(vid),
                        new object[]
                        {
                            pts.Growing,
                            pts.ElementType.ToPObject(level - 1)!
                        }
                    };
                }

                case PTypeEnumeration.union:
                {
                    PTypeUnion ptu = (PTypeUnion)this;
                    var query = ptu.Variants
                        .Select(pair => new object[] { pair.Name, pair.Type.ToPObject(level - 1)! })
                        .ToArray();
                    return new object[] { ToInt(vid), query };
                }

                default:
                    return new object[] { ToInt(vid), null! };
            }
        }

        /// <summary>
        /// Recreates schema descriptor from compact Polar object representation.
        /// </summary>
        /// <param name="po">Schema object produced by <see cref="ToPObject(int)"/>.</param>
        /// <returns>Parsed schema descriptor.</returns>
        public static PType FromPObject(object po)
        {
            object[] uni = (object[])po;
            int tg = (int)uni[0];
            switch (tg)
            {
                case 0: return new PType(PTypeEnumeration.none);
                case 1: return new PType(PTypeEnumeration.boolean);
                case 2: return new PType(PTypeEnumeration.character);
                case 3: return new PType(PTypeEnumeration.integer);
                case 4: return new PType(PTypeEnumeration.longinteger);
                case 5: return new PType(PTypeEnumeration.real);
                case 6: return new PTypeFString((int)uni[1]);
                case 7: return new PType(PTypeEnumeration.sstring);
                case 8:
                {
                    object[] fieldsDef = (object[])uni[1];
                    var query = fieldsDef.Select(fd =>
                    {
                        object[] f = (object[])fd;
                        return new NamedType((string)f[0], FromPObject(f[1]));
                    });
                    return new PTypeRecord(query.ToArray());
                }
                case 9:
                {
                    object[] payload = (object[])uni[1];
                    bool growing = (bool)payload[0];
                    PType elementType = FromPObject(payload[1]);
                    return new PTypeSequence(elementType, growing);
                }
                case 10:
                {
                    object[] variantsDef = (object[])uni[1];
                    var query = variantsDef.Select(vd =>
                    {
                        object[] v = (object[])vd;
                        return new NamedType((string)v[0], FromPObject(v[1]));
                    });
                    return new PTypeUnion(query.ToArray());
                }
                case 11:
                    return new PType(PTypeEnumeration.@byte);
                default:
                    throw new Exception("unknown tag for pobject");
            }
        }

        private static readonly PTypeUnion ttype;

        /// <summary>
        /// Gets recursive meta-schema describing Polar schema definitions themselves.
        /// </summary>
        public static PType TType => ttype;

        static PType()
        {
            ttype = new PTypeUnion();
            ttype.variants =
                new[]
                {
                    new NamedType("none", new PType(PTypeEnumeration.none)),
                    new NamedType("boolean", new PType(PTypeEnumeration.none)),
                    new NamedType("character", new PType(PTypeEnumeration.none)),
                    new NamedType("integer", new PType(PTypeEnumeration.none)),
                    new NamedType("longinteger", new PType(PTypeEnumeration.none)),
                    new NamedType("real", new PType(PTypeEnumeration.none)),
                    new NamedType("fstring", new PType(PTypeEnumeration.integer)),
                    new NamedType("sstring", new PType(PTypeEnumeration.none)),
                    new NamedType(
                        "record",
                        new PTypeSequence(
                            new PTypeRecord(
                                new NamedType("Name", new PType(PTypeEnumeration.sstring)),
                                new NamedType("Type", ttype)))),
                    new NamedType(
                        "sequence",
                        new PTypeRecord(
                            new NamedType("growing", new PType(PTypeEnumeration.boolean)),
                            new NamedType("Type", ttype))),
                    new NamedType(
                        "union",
                        new PTypeSequence(
                            new PTypeRecord(
                                new NamedType("Name", new PType(PTypeEnumeration.sstring)),
                                new NamedType("Type", ttype)))),
                    new NamedType("byte", new PType(PTypeEnumeration.@byte))
                };
        }

        /// <summary>
        /// Renders a schema-bound value into Polar textual representation.
        /// </summary>
        /// <param name="v">Value to render.</param>
        /// <param name="withfieldnames">Whether to include record field names in output.</param>
        /// <returns>Textual representation for diagnostics and debugging.</returns>
        public string Interpret(object v, bool withfieldnames = false)
        {
            switch (vid)
            {
                case PTypeEnumeration.none: return string.Empty;
                case PTypeEnumeration.boolean: return ((bool)v).ToString();
                case PTypeEnumeration.character: return "'" + ((char)v).ToString() + "'";
                case PTypeEnumeration.integer: return ((int)v).ToString();
                case PTypeEnumeration.longinteger: return ((long)v).ToString();
                case PTypeEnumeration.real: return ((double)v).ToString("G", CultureInfo.InvariantCulture);
                case PTypeEnumeration.fstring: return "\"" + ((string)v).Replace("\"", "\\\"") + "\"";
                case PTypeEnumeration.sstring: return "\"" + ((string)v).Replace("\"", "\\\"") + "\"";
                case PTypeEnumeration.record:
                {
                    PTypeRecord ptr = (PTypeRecord)this;
                    object[] arr = (object[])v;
                    StringBuilder sb = new StringBuilder();
                    sb.Append('{');
                    for (int i = 0; i < ptr.Fields.Length; i++)
                    {
                        if (i > 0) sb.Append(',');
                        if (withfieldnames)
                        {
                            sb.Append(ptr.Fields[i].Name);
                            sb.Append(':');
                        }

                        sb.Append(ptr.Fields[i].Type.Interpret(arr[i]));
                    }

                    sb.Append('}');
                    return sb.ToString();
                }
                case PTypeEnumeration.sequence:
                {
                    PTypeSequence pts = (PTypeSequence)this;
                    PType tel = pts.ElementType;
                    object[] arr = (object[])v;
                    StringBuilder sb = new StringBuilder();
                    sb.Append('[');
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append(tel.Interpret(arr[i]));
                    }

                    sb.Append(']');
                    return sb.ToString();
                }
                case PTypeEnumeration.union:
                {
                    PTypeUnion ptu = (PTypeUnion)this;
                    object[] arr = (object[])v;
                    if (arr.Length != 2) throw new Exception("incorrect data for union");
                    int tag = (int)arr[0];
                    if (tag < 0 || tag >= ptu.Variants.Length) throw new Exception("incorrect data for union");
                    NamedType nt = ptu.Variants[tag];
                    return nt.Name + "^" + nt.Type.Interpret(arr[1]);
                }
                case PTypeEnumeration.@byte:
                    return ((byte)v).ToString();
                default:
                    throw new Exception("Can't interpret value by type");
            }
        }
    }

    /// <summary>
    /// Named schema part used for record fields and union variants.
    /// </summary>
    public struct NamedType
    {
        /// <summary>
        /// Field or variant name.
        /// </summary>
        public string Name;

        /// <summary>
        /// Schema type associated with <see cref="Name"/>.
        /// </summary>
        public PType Type;

        /// <summary>
        /// Creates a named schema part.
        /// </summary>
        /// <param name="name">Field or variant name.</param>
        /// <param name="tp">Associated schema type.</param>
        public NamedType(string name, PType tp)
        {
            Name = name;
            Type = tp;
        }
    }

    /// <summary>
    /// Fixed-length UTF-16 string schema descriptor.
    /// </summary>
    public class PTypeFString : PType
    {
        /// <summary>
        /// Creates a fixed-string descriptor.
        /// </summary>
        /// <param name="length">String length in UTF-16 characters.</param>
        public PTypeFString(int length)
            : base(PTypeEnumeration.fstring)
        {
            this.length = length;
        }

        private readonly int length;

        /// <summary>
        /// Gets fixed payload size in bytes.
        /// </summary>
        public int Size => length * 2;

        /// <summary>
        /// Gets fixed payload length in characters.
        /// </summary>
        public int Length => length;
    }

    /// <summary>
    /// Record schema descriptor.
    /// </summary>
    public class PTypeRecord : PType
    {
        /// <summary>
        /// Creates a record descriptor from ordered field definitions.
        /// </summary>
        /// <param name="fields">Record fields in physical serialization order.</param>
        public PTypeRecord(params NamedType[] fields)
            : base(PTypeEnumeration.record)
        {
            this.fields = fields;
        }

        private int size = -1;
        private readonly NamedType[] fields;

        /// <summary>
        /// Gets field definitions in serialization order.
        /// </summary>
        public NamedType[] Fields => fields;

        /// <summary>
        /// Gets fixed record header size when all fields are fixed-size; otherwise derived from nested descriptors.
        /// </summary>
        public override int HeadSize
        {
            get
            {
                if (size == -1) Translate();
                return size;
            }
        }

        /// <summary>
        /// Calculates cumulative fixed header size from field schemas.
        /// </summary>
        public override void Translate()
        {
            if (fields == null) throw new Exception("VType Err: no fields in record def");
            size = 0;
            foreach (var fieldDef in fields)
            {
                size += fieldDef.Type.HeadSize;
            }
        }
    }

    /// <summary>
    /// Sequence schema descriptor.
    /// </summary>
    public class PTypeSequence : PType
    {
        /// <summary>
        /// Creates a sequence descriptor.
        /// </summary>
        /// <param name="elementtype">Element schema type.</param>
        /// <param name="growing">Legacy flag preserved in the schema payload.</param>
        public PTypeSequence(PType elementtype, bool growing = false)
            : base(PTypeEnumeration.sequence)
        {
            this.elementtype = elementtype;
            this.growing = growing;
        }

        private readonly PType elementtype;

        /// <summary>
        /// Gets element schema type.
        /// </summary>
        public PType ElementType => elementtype;

        private readonly bool growing;

        /// <summary>
        /// Gets legacy "growing" schema flag.
        /// </summary>
        public bool Growing => growing;

        /// <summary>
        /// Translates nested element schema.
        /// </summary>
        public override void Translate()
        {
            elementtype.Translate();
        }
    }

    /// <summary>
    /// Tagged union schema descriptor.
    /// </summary>
    public class PTypeUnion : PType
    {
        /// <summary>
        /// Creates a union descriptor from ordered variant definitions.
        /// </summary>
        /// <param name="variants">Variant list where index is the serialized tag value.</param>
        public PTypeUnion(params NamedType[] variants)
            : base(PTypeEnumeration.union)
        {
            this.variants = variants;
        }

        internal NamedType[] variants = Array.Empty<NamedType>();

        /// <summary>
        /// Gets or sets union variants where array index is the variant tag.
        /// </summary>
        public NamedType[] Variants
        {
            get => variants;
            set => variants = value;
        }

        /// <summary>
        /// Translates all variant payload schemas.
        /// </summary>
        public override void Translate()
        {
            if (variants == null) throw new Exception("VType Err: no variants in union def");
            foreach (var variant in variants)
            {
                variant.Type.Translate();
            }
        }
    }
}
