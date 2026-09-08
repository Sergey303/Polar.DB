using Polar.DB;

namespace Sborka
{
    public class UKeyInd
    {
        private USequenceBase hkeys;
        private USequenceBase offsets;
        private List<int> hkey_list = new List<int>();
        internal int[] hkey_arr = new int[0];
        private List<long> offset_list = new List<long>();
        private USequ bearing; // Опорная последовательность элементов типа tp
        private Func<object, IComparable> keyFunc;
        private Func<IComparable, int> hashOfKey;
        Dictionary<IComparable, long> keyoff_dic;

        public UKeyInd(USequ bearing, PType tp, Func<Stream> genStreams, Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey)
        {
            this.bearing = bearing;
            this.keyFunc = keyFunc;
            this.hashOfKey = hashOfKey;
            hkeys = new USequenceBase(new PType(PTypeEnumeration.integer), genStreams());
            offsets = new USequenceBase(new PType(PTypeEnumeration.longinteger), genStreams());
        
            keyoff_dic = new Dictionary<IComparable, long>();
        }

        public void OnAppendElement(object element, long offset)
        {
            var key = keyFunc(element);
            if (keyoff_dic.ContainsKey(key))
            {
                keyoff_dic.Remove(key);
                //TODO: можно и по-другому типа: keyoff_dic[key] = offset; с соответствующей коррекцией логики 
            }
            keyoff_dic.Add(key, offset);
        }

        public void Clear() { hkeys.Clear(); offsets.Clear(); hkey_arr = new int[0]; }
        public void AddObjOffset(object obj, long offset)
        {
            hkey_list.Add(hashOfKey(keyFunc(obj)));
            offset_list.Add(offset);
        }
        public void Build()
        {
            hkeys.Clear();
            offsets.Clear();

            hkey_arr = hkey_list.ToArray();
            hkey_list = new List<int>();
            long[] offset_arr = offset_list.ToArray();
            offset_list = new List<long>();
            Array.Sort<int, long>(hkey_arr, offset_arr);

            for (int i = 0; i < hkey_arr.Length; i++) hkeys.AppendElement(hkey_arr[i]);
            hkeys.Flush();
            for (int i = 0; i < hkey_arr.Length; i++) offsets.AppendElement(offset_arr[i]);
            offsets.Flush();
            offset_arr = new long[0];

            GC.Collect();
        }
        public void Refresh()
        {
            int size = (int)hkeys.Count();
            hkey_arr = new int[size];
            for (int i = 0; i < size; i++) hkey_arr[i] = (int)hkeys.GetByIndex(i);
        }
        public object? GetByKey(IComparable keysample)
        {
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
                long offset = (long)offsets.GetByIndex(pos);
                object? val = bearing.GetElement(offset);
                if (val == null) return null; // Непонятно, нужно ли?
                var k = keyFunc(val);
                if (k.CompareTo(keysample) == 0) return val;
                pos++;
            }
            return null;
        }
    }
}
