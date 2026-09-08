using Polar.DB;

namespace Sborka
{
    public class USequ
    {
        private USequenceBase bearing;
        private UKeyInd primaryIndex;
        private Func<object, IComparable> keyFunc;
        private Func<IComparable, int> hashOfKey;
        public USequ(PType tp, Func<Stream> genStreams, Func<object, IComparable> keyFunc, Func<IComparable, int> hashOfKey)
        {
            bearing = new USequenceBase(tp, genStreams());
            this.keyFunc = keyFunc;
            this.hashOfKey = hashOfKey;
            primaryIndex = new UKeyInd(this, tp, genStreams, keyFunc, hashOfKey);
        }
        public void Clear() {  bearing.Clear(); }
        public void Load(IEnumerable<object> flow)
        {
            bearing.Clear();
            foreach (object item in flow)
            {
                long off = bearing.AppendElement(item);
                primaryIndex.AddObjOffset(item, off);
            }
            bearing.Flush();
            primaryIndex.Build();
        }
        public void Refresh() { bearing.Refresh(); primaryIndex.Refresh(); }
        public IEnumerable<object> ElementValues()
        {
            return bearing.ElementValues();
        }
        public object GetElement(long offset)
        {
            return bearing.GetElement(offset);
        }
        public object? GetByKey(IComparable key)
        {
            return primaryIndex.GetByKey(key);
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
        }
    }
}
