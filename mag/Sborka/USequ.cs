using Polar.DB;

namespace Sborka
{
    public class USequ
    {
        private USequenceBase bearing;
        private UKeyInd primaryIndex;
        private IEIndex[] externalIndexes;
        private Func<object, IComparable> keyFunc;
        private Func<IComparable, int> hashOfKey;
        private Func<object, bool> isEmpty;
        public USequ(PType tp, Func<Stream> genStreams, Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey, Func<object, bool> isEmpty)
        {
            bearing = new USequenceBase(tp, genStreams());
            this.keyFunc = keyFunc;
            this.hashOfKey = hashOfKey;
            this.isEmpty = isEmpty;
            primaryIndex = new UKeyInd(this, tp, genStreams, keyFunc, hashOfKey);
            externalIndexes = new IEIndex[0];
        }
        public void AppendIndexes(IEnumerable<IEIndex> indexes) { externalIndexes = indexes.ToArray(); }
        public void Clear() { bearing.Clear(); primaryIndex.Clear(); foreach (var ee in externalIndexes) ee.Clear(); }
        public void Load(IEnumerable<object> flow)
        {
            this.Clear();
            foreach (object item in flow)
            {
                long off = bearing.AppendElement(item);
                primaryIndex.AddObjOffset(item, off);
            }
            bearing.Flush();
            primaryIndex.Build();
            foreach (var ee in externalIndexes) ee.Build();
        }
        public void Connect()
        {
            bearing.Refresh(); primaryIndex.Connect();
        }
        public long Count() => bearing.Count();
        public long CurrenOffset() => bearing.ElementOffset();
        public void Refresh() { bearing.Refresh(); primaryIndex.Refresh(); }
        public IEnumerable<object> ElementValues()
        {
            var flow = bearing.ElementOffsetValuePairs()
                    .Where(p => primaryIndex.IsOriginal(p.Item1, p.Item2))
                    .Select(obofpair => obofpair.Item1)
                    .Concat(primaryIndex.DynFlow().Select(p => p.Item1))
                    .Where(ob => !(bool)((object[])ob)[1]);
            return flow;
        }
        public object GetElement(long offset)
        {
            return bearing.GetElement(offset);
        }
        public object? GetByKey(IComparable key)
        {
            object? v = primaryIndex.GetByKey(key);
            if (v == null || (v != null && isEmpty(v))) return null;
            return v;
        }
        public void Scan(Func<long, object, bool> handler)
        {
            bearing.Scan((off, ob) =>
            {
                bool ok = handler(off, ob);
                return ok;
            });
        }
        public void AppendElement(object ob)
        {
            long offset = bearing.AppendElement(ob);
            primaryIndex.OnAppendElement(ob, offset);
            foreach (var ee in externalIndexes) ee.OnAppendElement(ob, offset);
        }

        public IEnumerable<(object, long)> ElementOffsetValuePairs()
        {
            return bearing.ElementOffsetValuePairs();
        }
        public IEnumerable<(object, long)> ElementOffsetValuePairs(long offset, long number)
        {
            return bearing.ElementOffsetValuePairs(offset, number);
        }
    }
}
