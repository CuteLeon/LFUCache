using System;
using System.Threading.Tasks;

namespace LFUCache
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            LFUCacheSet<int, string> cacheSet = new LFUCacheSet<int, string>(5);
            cacheSet.Log += (s, e) => Console.WriteLine(e);

            Parallel.For(0, 100, (index) =>
            {
                cacheSet.Add(index, index.ToString());
            });
            _ = cacheSet.GetFrequencyList();

            for (int index = -5; index <= 5; index++)
            {
                cacheSet.Add(index, index.ToString());
                _ = cacheSet.GetFrequencyList();
            }

            cacheSet.Remove(-1);

            cacheSet.Get(1);
            _ = cacheSet.GetFrequencyList();

            cacheSet.Add(6, "6");
            _ = cacheSet.GetFrequencyList();

            cacheSet.Add(1, "0");
            _ = cacheSet.GetFrequencyList();

            cacheSet.Get(5);
            _ = cacheSet.GetFrequencyList();


            cacheSet.Get(2);
            _ = cacheSet.GetFrequencyList();

            cacheSet.Remove(3);
            cacheSet.Remove(4);
            _ = cacheSet.GetFrequencyList();

            _ = cacheSet.GetFrequencyList();

            cacheSet.Add(1, "1");
            _ = cacheSet.GetFrequencyList();

            Console.Read();
        }
    }
}
