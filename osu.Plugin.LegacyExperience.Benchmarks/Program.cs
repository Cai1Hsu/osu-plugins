using BenchmarkDotNet.Running;

namespace osu.Plugin.LegacyExperience.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .Run(args);
        }
    }
}
