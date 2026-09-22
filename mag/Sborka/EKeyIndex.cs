using Polar.DB;

namespace Sborka
{
    public class EKeyIndex : IEIndex
    {
        // Есть опорная последовательность
        private readonly USequ bearing;
        // Есть ключевая функция, вырабатывающая на элементах набор ключей. Ключи можно сравнивать!
        private Func<object, IEnumerable<IComparable>> keysFunc;
        // Есть преобразователь ключа в целое, это может быть хеш-функция или тожлественная
        private Func<IComparable, int> hashOfKey;
        // Статическая часть индекса
        private int[] hkey_arr;
        private long[]? offset_arr;
        private USequenceBase hkeys;
        private USequenceBase offsets;
        // Динамическая часть пока не намечена

        public EKeyIndex(Func<Stream> streamGen, USequ bearing,
            Func<object, IEnumerable<IComparable>> keysFunc, Func<IComparable, int> hashOfKey)
        {
            this.bearing = bearing;
            this.keysFunc = keysFunc;
            this.hashOfKey = hashOfKey;

            hkeys = new USequenceBase(new PType(PTypeEnumeration.integer), streamGen());
            offsets = new USequenceBase(new PType(PTypeEnumeration.longinteger), streamGen());
            hkey_arr = new int[0];
            offset_arr = new long[0];
        }

        public void Build()
        {
            Clear();
            // Цикл по элементам опорной последовательности
            List<int> hkey_list = new List<int>();
            List<long> offset_list = new List<long>();
            foreach (var (obj, off) in bearing.ElementOffsetValuePairs())
            {
                foreach (var key in keysFunc(obj))
                {
                    int hkey = hashOfKey(key);
                    hkey_list.Add(hkey);
                    offset_list.Add(off);
                } 
            }
            hkey_arr = hkey_list.ToArray();
            offset_arr = offset_list.ToArray();
            GC.Collect();
            Array.Sort<int, long>(hkey_arr, offset_arr);
            foreach (var hkey in hkey_arr) hkeys.AppendElement(hkey);
            hkeys.Flush();
            foreach (var offset in offset_arr) offsets.AppendElement(offset);
            offsets.Flush();
        }
        public IEnumerable<object> GetManyByKey(IComparable key)
        {
            if (offset_arr == null) return Enumerable.Empty<object>();
            int hkey = hashOfKey(key);
            var ind = Array.BinarySearch(hkey_arr, hkey);
            if (ind < 0) return Enumerable.Empty<object>();
            // находим мин индекс minindex такой, minindex >= 0 && hkey_arr[minindex] == hkey
            int m = ind - 1;
            while (m >= 0 && hkey_arr[m] == hkey) { m--; }
            int minindex = m + 1;
            m = ind + 1;
            while (m < hkey_arr.Length && hkey_arr[m] == hkey) { m++; }
            int maxindex = m - 1;
            var query = Enumerable.Range(minindex, maxindex - minindex + 1)
                .Select(i => bearing.GetElement(offset_arr[i]))
                .Where(ob  => keysFunc(ob).Any(c => c.CompareTo(key) == 0));
            return query;
        }
        public void Clear() { hkeys.Clear(); hkey_arr = new int[0]; offsets.Clear(); offset_arr = new long[0] }
        public void Flush() { hkeys.Flush(); offsets.Flush(); }
        public void Close() { hkeys.Close(); offsets.Close(); }
        public void Refresh()
        {
            hkey_arr = hkeys.ElementValues().Cast<int>().ToArray();
            offset_arr = offsets.ElementValues().Cast<long>().ToArray();
            //offsets.Refresh();
        }
        public void OnAppendElement(object element, long offset)
        {
            throw new NotImplementedException();
        }
    }
}
