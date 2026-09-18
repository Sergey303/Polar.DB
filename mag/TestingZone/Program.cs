using Polar.DB;
using Polar.Universal;
//using Sborka;

int npersons = 5_000_000;
bool toload = true;
if (args.Length == 2) { npersons = int.Parse(args[0]); toload = (args[1] == "true"); }

Console.WriteLine("Старт моей базы данных");

Random rnd = new Random();
System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

string dbPath = $"C:\\Home\\data\\getstarted\\";
if (!Directory.Exists(dbPath)) Directory.CreateDirectory(dbPath);

if (toload) foreach (var file in Directory.GetFiles(dbPath)) File.Delete(file);

PType tp = new PTypeRecord(
    new NamedType("code", new PType(PTypeEnumeration.integer)),
    new NamedType("name", new PType(PTypeEnumeration.sstring)),
    new NamedType("age", new PType(PTypeEnumeration.integer)));

// Генератор потока
IEnumerable<object> flow = Enumerable.Range(0, npersons)
    .Select(i => new object[] { npersons - i - 1, i.ToString(), 22 });

// Генератор стримов
int cnt = 0;
Func<Stream> GenStream = () => new System.IO.FileStream(dbPath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

//USequ usequence = new USequ(tp, GenStream, ob => (int)((object[])ob)[0], k => (int)k);
USequence usequence = new USequence(tp, "state.bin", GenStream, ob => false);
usequence.SetPrimaryKey<int>(ob => (int)((object[])ob)[0]);

if (toload)
{
    sw.Restart();
    usequence.Load(flow);
    usequence.Build();
    sw.Stop();
    Console.WriteLine("Load ok. duration=" + sw.ElapsedMilliseconds);
}
else
{
    sw.Restart();
    usequence.Refresh();
    sw.Stop();
    Console.WriteLine("Refresh ok. duration=" + sw.ElapsedMilliseconds);
}

//foreach (object ob in usequence.ElementValues()) Console.WriteLine(tp.Interpret(ob));
int cod = npersons * 2 / 3;
object? ob = usequence.GetByKey(cod);

if (ob != null) Console.WriteLine(tp.Interpret(ob));
else Console.WriteLine($"item with cod {cod} not found");

sw.Restart();
for (int i = 0; i < 10000; i++)
{
    int k = rnd.Next(npersons);
    object? v = usequence.GetByKey(k);
    if (v != null) { }//Console.WriteLine(tp.Interpret(v));
    else Console.WriteLine($"item with cod {k} not found");
}
sw.Stop();
Console.WriteLine("10K GetByKey ok. duration=" + sw.ElapsedMilliseconds);



