using GetStarted.AdvancedFlowsAndExperiments;

namespace GetStarted3
{
    partial class Program
    {
        // Эта директория делается для всех экспериментов
        internal static string datadirectory_path = SamplePaths.Combine("GetStarted3");
        internal static System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        static void Main()
        {
            //Main301();
            Main302();
            //Main303();
            //Main305();
            //Main306();
            //Main307();
            //Main304SQLite();
        }
    }
}
