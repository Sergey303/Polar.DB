using System.Diagnostics;

namespace Legacy2021Replay;

// Port of PolarDB.BDLearningCourse/Task01.cs Run(): historical timing boundaries.
internal static class BinaryReplay
{
    internal static void Run(string dir, long nelements, long nreads, int? seed, bool toload)
    {
        Stopwatch sw = new Stopwatch();
        using FileStream fs = File.Open(Path.Combine(dir, "data.bin"), FileMode.OpenOrCreate);
        using BinaryWriter bw = new BinaryWriter(fs, System.Text.Encoding.UTF8, leaveOpen: true);
        using BinaryReader br = new BinaryReader(fs, System.Text.Encoding.UTF8, leaveOpen: true);

        if (toload)
        {
            sw.Restart();
            for (long ii = 0L; ii < nelements; ii++) bw.Write(ii);
            fs.Flush();
            sw.Stop();
            Console.WriteLine($"load time {sw.ElapsedMilliseconds}");
        }
        else
        {
            fs.Position = 0L;
            sw.Restart();
            byte[] buffer = new byte[100000];
            int nblocks = (int)(nelements * 8 / buffer.Length);
            for (int i = 0; i < nblocks; i++) fs.Read(buffer, 0, buffer.Length);
            sw.Stop();
            Console.WriteLine($"warm up time {sw.ElapsedMilliseconds}");
        }

        fs.Position = (nelements * 2 / 3) * 8;
        long v = br.ReadInt64();
        if (v != nelements * 2 / 3) throw new InvalidDataException("Single-point verification failed");
        Console.WriteLine($"v = {v}");

        sw.Restart();
        Random rnd = seed.HasValue ? new Random(seed.Value) : new Random();
        for (long ii = 0; ii < nreads; ii++)
        {
            long ind = rnd.Next((int)nelements);
            fs.Position = ind * 8;
            long val = br.ReadInt64();
            if (val != ind) throw new Exception($"Err: ind={ind} val={val}");
        }
        sw.Stop();
        Console.WriteLine($"elapsed {sw.ElapsedMilliseconds} ms.");
        Console.WriteLine($"verified_read_count={nreads}");
    }
}
