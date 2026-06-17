using Polar.DB;
//using Polar.Universal;

namespace mag_experiments
{
    internal class BearingSequence
    {
        private USequenceBase _sequence;
        internal Func<Stream> _genStream;
        internal PrimaryKeyIndex pk_index;
        private Func<object, IComparable?> keyFunc;
        private Func<IComparable, int> hashOfKey;
        public BearingSequence(PType etype, Func<Stream> genStre, Func<object, IComparable?> keyFunc, Func<IComparable, int> hashOfKey)
        {
            _genStream = genStre;
            _sequence = new USequenceBase(etype, genStre());
            pk_index = new PrimaryKeyIndex(genStre, this, keyFunc, hashOfKey);
        }
        public void Load(IEnumerable<object> flow)
        {
            pk_index.Clear();
            foreach (var item in flow)
            {
                int key = (int)((object[])item)[0];
                long off = AppendElement(item);
                pk_index.AppendKeyOffset(key, off);
            }
            Flush();
        }
        public void Clear() { _sequence.Clear(); }
        public void Flush() { _sequence.Flush(); }
        public void Refresh() { _sequence.Refresh(); pk_index.Refresh(); }
        internal long AppendElement(object item) { return _sequence.AppendElement(item); }
        public object GetElement(long offset) { return _sequence.GetElement(offset); }
    }
    internal class PrimaryKeyIndex
    {
        private Func<Stream> streamGen;
        private readonly BearingSequence sequence;
        private List<int> key_list;
        private List<long> offset_list;
        private int[] keys_arr = new int[0];
        private long[] offsets_arr = new long[0];
        private USequenceBase hkeys;
        private USequenceBase offsets;

        // Ключом является объект, порождаемый ключевой функцией. Ключи можно сравнивать!
        private Func<object, IComparable?> keyFunc;

        private Func<IComparable, int> hashOfKey;

        // Динамическая часть индекса
        private Dictionary<IComparable, long> keyoff_dic;

        public PrimaryKeyIndex(Func<Stream> streamGen, BearingSequence sequence, Func<object, IComparable?> keyFunc, Func<IComparable, int> hashOfKey)
        {
            this.streamGen = streamGen;
            this.sequence = sequence;
            this.keyFunc = keyFunc;
            this.hashOfKey = hashOfKey;

            hkeys = new USequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new USequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());

            keyoff_dic = new Dictionary<IComparable, long>();
        }
        internal void Clear()
        {
            key_list = new List<int>(); offset_list = new List<long>(); keys_arr = new int[0]; offsets_arr = new long[0];
            keyoff_dic = new Dictionary<IComparable, long>();
        }
        internal void Refresh()
        {
            keys_arr = new int[hkeys.Count()];
            int i = 0;
            foreach (int key in hkeys.ElementValues())
            {
                keys_arr[i] = key;
                i++;
            }
        }
        internal void AppendKeyOffset(int key, long offset)
        {
            key_list.Add(key);
            offset_list.Add(offset);
        }
        internal void SortKeyOffset()
        {

            this.keys_arr = key_list.ToArray();
            key_list = new List<int>();
            this.offsets_arr = offset_list.ToArray();
            offset_list = new List<long>();

            Array.Sort(keys_arr, offsets_arr);

            //hkeys = new USequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            hkeys.Clear();
            foreach (var k in keys_arr) hkeys.AppendElement(k);
            hkeys.Flush();

            //offsets = new USequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
            offsets.Clear();
            foreach (var f in offsets_arr) offsets.AppendElement(f);
            offsets.Flush();
            offsets_arr = new long[0];
        }
        //internal object GetElement(long offset) { return sequence.pk_index.GetElement(offset); }
        public object GetElementByKey(int key)
        {
            int nom = keys_arr.BinarySearch<int>(key);
            long offset = (long)offsets.GetByIndex(nom);
            var obj = sequence.GetElement(offset);
            return obj;
        }
        internal bool ElementChanged(IComparable key) { return keyoff_dic.ContainsKey(key); }
    }
    internal class Experiment2
    {
        public static void Run()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Random rnd = new Random();
            Console.WriteLine("Experiment2");



            // Тип элемента последовательности
            PType tp_pers = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)),
                new NamedType("age", new PType(PTypeEnumeration.integer)));

            // Указываем директорию для файлов базы данных, формируем генератор потоков
            string dbpath = "C:\\Home\\data\\getstarted\\";

            bool toload = true;
            if (toload)
            {
                var files = Directory.GetFiles(dbpath);
                foreach (var file in files) File.Delete(file);
            }

            int cnt = 0;
            Func<Stream> GenStream = () => new System.IO.FileStream(dbpath + "f" + (cnt++) + ".bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);

            // Создаем опорную последовательность
            //USequence useq = new USequence(tp_pers, dbpath + "state.bin", GenStream, ob => false,
            //    ob => (int)((object[])ob)[0], ic => (int)ic, true);
            BearingSequence usb = new BearingSequence(tp_pers, GenStream, obj => (int)((object[])obj)[0], k => (int)k);

            // Загрузка данными
            int npersons = 5_000_000;
            Console.WriteLine($"{npersons} элементов");

            var query = Enumerable.Range(0, npersons)
                .Select(i => new object[] { npersons - i - 1, i.ToString(), 22 });

            if (toload)
            {
                sw.Restart();
                usb.Load(query);
                sw.Stop();
                Console.WriteLine($"Загрузка: {sw.ElapsedMilliseconds} ms.");
                sw.Restart();
                usb.pk_index.SortKeyOffset();
                sw.Stop();
                Console.WriteLine($"Сортировка: {sw.ElapsedMilliseconds} ms.");
            }
            else
            {
                sw.Restart();
                usb.Refresh();
                sw.Stop();
                Console.WriteLine($"Refresh: {sw.ElapsedMilliseconds} ms.");
            }



            int ke = npersons * 2 / 3;
            var obj = usb.pk_index.GetElementByKey(ke);
            Console.WriteLine(tp_pers.Interpret(obj));

            sw.Restart();

            for (int i = 0; i < 10000; i++)
            {
                int k = rnd.Next(npersons);
                var ob = usb.pk_index.GetElementByKey(k);
                //if (i < 100) Console.WriteLine(tp_pers.Interpret(ob));
            }

            sw.Stop();
            Console.WriteLine($"10000 выборок: {sw.ElapsedMilliseconds} ms.");

            // Результаты:
            // 10 млн. элементов загрузка 1168 мс. построение 854 мс. Выборки: 43 мс / 10 тыс. (для массива offsets_arr) 77 мс (нормально)

            // 5 млн. элементов загрузка 746 мс. построение 545 мс. Выборки: 76 мс (нормально)

            // Убрал Build (возможно, он будет для индексов), теперь загрузка включает в себя сортировку.
            // 5 млн. элементов загрузка 1151 мс. Выборки: 83 мс

        }
    }
}
