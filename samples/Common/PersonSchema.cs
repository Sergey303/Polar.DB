using Polar.DB;
using Polar.DB.ExternalKey;
using Polar.Universal;

namespace Common;

public static class PersonSchema
{
    public const string Id = nameof(Id);
    public const string Age = nameof(Age);
    public const string Name = nameof(Name);
    public const string IsDeleted = nameof(IsDeleted);
    public const int IdIndex = 0;
    public const int AgeIndex = 1;
    public const int NameIndex = 2;
    public const int DeletedIndex = 3;
    
    public static readonly PTypeRecord Type = new(
        new NamedType(Id, new PType(PTypeEnumeration.integer)),
        new NamedType(Age, new PType(PTypeEnumeration.integer)),
        new NamedType(Name, new PType(PTypeEnumeration.sstring)),
        new NamedType(IsDeleted, new PType(PTypeEnumeration.boolean)));

    public static readonly RecordAccessor Accessor = new(Type);

    public static object Create(int id, int age, string name)
    {
        return Accessor.CreateRecord(id, age, name, false);
    }

    public static object Tombstone(int id)
    {
        // Удаление в append-модели: новая запись с тем же primary key.
        return Accessor.CreateRecord(id, 0, "", true);
    }

    public static int GetId(object record) => Accessor.Get<int>(record, Id);
    public static int GetAge(object record) => Accessor.Get<int>(record, Age);
    public static string GetName(object record) => Accessor.Get<string>(record, Name);
    public static bool Deleted(object record) => Accessor.Get<bool>(record, IsDeleted);

    public static string Format(object record) =>
        $"#{GetId(record)} {GetName(record)} | age={GetAge(record)}";


    public static USequence Create(string dbPath)
    {
        var sequence = Open(dbPath);
        sequence.Build();
        
        return sequence;
    }

    public static USequence Open(string dbPath)
    {
        int cnt = 0;
        Func<Stream> genStream = () =>
            new FileStream(Path.Combine(dbPath, "f" + (cnt++) + ".bin"), FileMode.OpenOrCreate, FileAccess.ReadWrite);

        USequence sequence = new USequence(Type, Path.Combine(dbPath, "state.bin"), genStream,
            Deleted,
            obj => GetId(obj),
            key => (int)key);

        ExternalKeyIndex<int> ageIndex = new ExternalKeyIndex<int>(genStream, sequence,
            obj => Enumerable.Repeat(GetAge(obj), 1));

        ExternalKeyIndex<int> ager = new ExternalKeyIndex<int>(genStream, sequence,
            obj => Enumerable.Repeat(GetAge(obj), 1),
            Comparer<int>.Create((int v1, int v2) => Math.Abs(v1 - v2) < 2 ? 0 : v1 - v2));
        ExternalKeyIndex<string> namer = new ExternalKeyIndex<string>(genStream, sequence,
            obj => Enumerable.Repeat(GetName(obj), 1),
            Comparer<string>.Create((string s1, string s2) =>
            {
                string a = (string)s1;
                string b = (string)s2;
                if (string.IsNullOrEmpty(b)) return 0;
                int len = b.Length;
                return string.Compare(
                    a, 0,
                    b, 0, len, StringComparison.Ordinal);
            }));

        sequence.uindexes = new IUIndex[] { ageIndex, ager, namer };
        return sequence;
    }
}
