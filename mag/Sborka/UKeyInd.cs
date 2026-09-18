using Polar.DB;
using System.IO;
using System.Xml.Linq;

namespace Sborka
{
    public class UKeyInd
    {
        private USequenceBase hkey_seq;
        private List<int> hkey_list = new List<int>();
        internal int[] hkey_arr = new int[0];
        private USequenceBase offset_seq;
        private List<long> offset_list = new List<long>();
        internal long[] offset_arr = new long[0];
        private USequ usequence; // Опорная последовательность элементов типа tp
        private Func<object, IComparable> keyFunc;
        private Func<IComparable, int> hashOfKey;
        private Dictionary<IComparable, long> keyoff_dic;
        private Stream statestream;
        private BinaryReader statereader;
        private BinaryWriter statewriter;
        
        public UKeyInd(USequ bearing, PType tp, Func<Stream> genStreams, Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey)
        {
            this.usequence = bearing;
            this.keyFunc = keyFunc;
            this.hashOfKey = hashOfKey;
            hkey_seq = new USequenceBase(new PType(PTypeEnumeration.integer), genStreams());
            offset_seq = new USequenceBase(new PType(PTypeEnumeration.longinteger), genStreams());

            statestream = genStreams();
            statereader = new BinaryReader(statestream);
            statewriter = new BinaryWriter(statestream);
            
            keyoff_dic = new Dictionary<IComparable, long>();
        }

        public void OnAppendElement(object element, long offset)
        {
            var key = keyFunc(element);
            if (keyoff_dic.ContainsKey(key))
            {
                keyoff_dic.Remove(key);
            }
            keyoff_dic.Add(key, offset);
        }

        private void WriteState(long count, long offset)
        {
            statestream.SetLength(0L);
            statestream.Position = 0L;
            //BinaryWriter statewriter = new BinaryWriter(statestream);
            statewriter.Write(count);
            statewriter.Write(offset);
            statewriter.Flush();
        }
        private (long, long) ReadState()
        {
            statestream.Position = 0L;
            //BinaryReader statereader = new BinaryReader(statestream);
            long c = statereader.ReadInt64();
            long o = statereader.ReadInt64();
            return (c, o);
        }
        public void Clear() 
        { 
            hkey_seq.Clear(); offset_seq.Clear(); hkey_arr = new int[0]; offset_arr = new long[0];
            keyoff_dic.Clear(); 
            WriteState(0, 8);
        }
        public void AddObjOffset(object obj, long offset)
        {
            hkey_list.Add(hashOfKey(keyFunc(obj)));
            offset_list.Add(offset);
        }
        private HashSet<int> multi_hkey_set = new HashSet<int>();
        private long static_board_count;
        private long static_board_offset;
        public void Build()
        {
            hkey_seq.Clear();
            offset_seq.Clear();

            hkey_arr = hkey_list.ToArray();
            hkey_list = new List<int>();
            offset_arr = offset_list.ToArray();
            offset_list = new List<long>();
            Array.Sort<int, long>(hkey_arr, offset_arr);

            bool tomake_mhs = true;
            if (tomake_mhs) 
            {
                static_board_count = usequence.Count();
                static_board_offset= usequence.CurrenOffset();
                multi_hkey_set = new HashSet<int>();
                for (int i = 0; i < hkey_arr.Length; i++)
                {
                    int hkey = hkey_arr[i];
                    // Условие единственности
                    if ((i == 0 || hkey != hkey_arr[i - 1]) &&
                        (i == hkey_arr.Length - 1 || hkey != hkey_arr[hkey_arr.Length - 1])) { }
                    else { multi_hkey_set.Add(hkey); }
                }
            }
            bool tocompress = true;
            if (tocompress)
            {
                // ========== Сканирую получившиеся массивы, получаю массивы с выброшенными неактуальными значениями, это будут массивы первичного ключевого индекса
                List<int> hkey_list2 = new List<int>(); // список для накапливания
                List<long> offset_list2 = new List<long>(); // синхронный список для накапливания

                // Маленький накопитель пар ключ - офсет при одном хеш-ключе, при этом офсет - максимальный встретившийся
                int hkey_saved = int.MinValue; // Неправильно, но чтобы компилятор не ругался
                Dictionary<IComparable, long> key_maxoffset_dic = new Dictionary<IComparable, long>();
                Action sbros = () => // сброс накопленных в словаре офсетов вместе с hkey_saved, ключ не используется
                {
                    if (key_maxoffset_dic.Count > 0)
                    {
                        foreach (var pair in key_maxoffset_dic)
                        {
                            hkey_list2.Add(hkey_saved);
                            offset_list2.Add(pair.Value);
                        }
                        key_maxoffset_dic = new Dictionary<IComparable, long>();
                    }
                };

                int hkey_current = hkey_arr[hkey_arr.Length - 1];  // беру последнее, чтобы начать
                for (int i = 0; i < hkey_arr.Length; i++)
                {
                    int hkey = hkey_arr[i]; long offset = offset_arr[i];
                    // === hkey может быть единственным или не единственным (повторным или другим
                    // Единственное если (первое или не предыдущее) и (последнее или не следующее)
                    if ((i == 0 || hkey_arr[i - 1] != hkey) && ((i + 1) == hkey_arr.Length || hkey != hkey_arr[i + 1]))
                    {
                        sbros(); // сброс предыдущего

                        hkey_list2.Add(hkey);
                        offset_list2.Add(offset);
                    }
                    else if (i > 0 && hkey_arr[i - 1] == hkey) // предыдущее hkey было таким же, надо корректировать входы в словарь key_maxoffset_dic 
                    {
                        // надо заглянуть в значение элемента и вычислить ключ
                        object element = usequence.GetElement(offset);
                        IComparable key = (IComparable)keyFunc((object[])element);

                        if (key_maxoffset_dic.TryGetValue(key, out long offset_in_dic)) // вход есть в словаре и offset перебивает
                        {
                            if (offset > offset_in_dic)
                            {
                                key_maxoffset_dic.Remove(key); // убираем вход и пишем новый
                                key_maxoffset_dic.Add(key, offset);
                            }

                        }
                        else // входа нет в словаре, тогда пишем новый
                        {
                            key_maxoffset_dic.Add(key, offset);
                        }
                    }
                    else // изменился hkey, надо фиксировать сбросить накопленное и начать новое
                    {
                        sbros();
                        hkey_saved = hkey;
                        // надо заглянуть в значение элемента и вычислить ключ
                        object element = usequence.GetElement(offset);
                        IComparable key = (IComparable)keyFunc((object[])element);
                        key_maxoffset_dic.Add(key, offset);
                    }
                }
                // После конца цикла надо сбросить значения
                sbros();
                // ========= конец сканирования
                hkey_arr = hkey_list2.ToArray();
                offset_arr = offset_list2.ToArray();
            }

            for (int i = 0; i < hkey_arr.Length; i++) hkey_seq.AppendElement(hkey_arr[i]);
            hkey_seq.Flush();
            for (int i = 0; i < hkey_arr.Length; i++) offset_seq.AppendElement(offset_arr[i]);
            offset_seq.Flush();
            offset_arr = new long[0]; //TODO: нужнен режим в ОЗУ 

            GC.Collect();

            long nelements = usequence.Count();
            long currentoffset = usequence.CurrenOffset();
            WriteState(nelements, currentoffset);

            return;
        }
        public void Refresh()
        {
            int size = (int)hkey_seq.Count();
            hkey_arr = new int[size];
            for (int i = 0; i < size; i++) hkey_arr[i] = (int)hkey_seq.GetByIndex(i);
        }
        public void Connect()
        {
            Refresh();
            // Концовка построения: достройка динамического индекса. 
            var countoffset = ReadState();
            // А текущий размер:
            long nelements = usequence.Count();
            // Динамику надо воспроизводить только если размер увеличился
            Console.WriteLine($"{nelements - countoffset.Item1} elements added");
            if (nelements > countoffset.Item1)
            {
                var additional = usequence.ElementOffsetValuePairs(countoffset.Item2, nelements - countoffset.Item1);
                foreach (var pair in additional)
                {
                    this.OnAppendElement(pair.Item1, pair.Item2);                    
                }
            }
        }
        private (long, object?)? GetOffObjByKey(IComparable keysample)
        {
            if (keyoff_dic.ContainsKey(keysample))
            {
                long o = keyoff_dic[keysample];
                return (o, usequence.GetElement(o));
            }
            int hkey = hashOfKey(keysample);
            int pos = Array.BinarySearch<int>(hkey_arr, hkey);
            if (pos < 0) return null;
            // ищем самую левую позицию 
            int p = pos;
            while (p >= 0 && hkey_arr[p] == hkey)
            {
                pos = p;
                p--;
            }

            // движемся вправо
            while (pos < hkey_arr.Length && hkey_arr[pos] == hkey)
            {
                long offset = (long)offset_seq.GetByIndex(pos);
                object? val = usequence.GetElement(offset);
                if (val == null) return null; // Непонятно, нужно ли?
                var k = keyFunc(val);
                if (k.CompareTo(keysample) == 0) return (offset, val);
                pos++;
            }
            return null;
        }
        public object? GetByKey(IComparable keysample)
        {
            (long, object?)? pair = GetOffObjByKey(keysample);
            if (pair == null || pair.Value.Item2 == null) return null;
            return pair.Value.Item2;
        }
        // ============ Процедуры организации генерирования потока элементов универсальной последовательности.
        // метод-предикат IsOriginal, выявляющий для статических элементов являются ли они оригиналом
        // метод-генератор DynFlow, генерирующий поток элементов из динамической части универсальной последовательности 

        /// <summary>
        /// Предикат, определяющий для объекта элемента и его офсета является ли он оригиналом или есть более поздний 
        /// (с тем же ключом, но большим значением офсета). Вычисление значения предиката опирается на: multi_hkey_set 
        /// </summary>
        /// <param name="ob"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool IsOriginal(object ob, long offset)
        {
            IComparable key = keyFunc(ob);
            int hkey = hashOfKey(key);
            // Варианты - статический элемент или динамический
            if (offset < static_board_offset)
            {
                // Принадлежность словарю - не пропускает статический элемент
                if (keyoff_dic.ContainsKey(key)) return false;

                // Варианты - одиночный hkey или множественный
                if ( !multi_hkey_set.Contains(hkey)) // Этот элемент пропускаем!
                {
                    return true;
                } else // Самая длительная цепочка действий нахождение элемента по ключу и выяснение оригинал ли
                {
                    var offobj_pair = GetOffObjByKey(key);
                    if (offobj_pair == null || offobj_pair.Value.Item1 != offset) return false;
                    return true;
                }
            }
            else
            {
                return false;
            }
        }
        public IEnumerable<(object, long)> DynFlow()
        {
            var flow = keyoff_dic.Values.Select(off => (usequence.GetElement(off), off));
            return flow;
        }
    }
}
