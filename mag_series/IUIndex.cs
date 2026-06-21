namespace mag_series
{
    //public struct ObjOff
    //{
    //    public object obj;
    //    public long off;
    //    public ObjOff(object obj, long off) { this.obj = obj; this.off = off; }
    //}

    public interface IUIndex
    {
        void Clear();
        void Flush();
        void Close();
        void Refresh();
        void Build();
        void OnAppendElement(object element, long offset);
    }
}
