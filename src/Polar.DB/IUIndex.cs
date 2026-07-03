namespace Polar.Universal
{
    public struct ObjOff
    {
        public object obj;
        public long off;

        public ObjOff(object obj, long off)
        {
            this.obj = obj;
            this.off = off;
        }
    }

    public interface IUIndex : System.IDisposable
    {
        void Clear();
        void Flush();
        void Close();
        void Refresh();
        void Build();
        void OnAppendElement(object element, long offset);
    }
}