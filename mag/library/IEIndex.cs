
namespace Polar.DB
{
    public interface IEIndex
    {
        void Clear();
        void Flush();
        void Close();
        void Refresh();
        void Build();
        void OnAppendElement(object element, long offset);
    }
}
