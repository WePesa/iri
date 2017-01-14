namespace iri.utils
{
    using System;
    using System.Threading;

    public static class ThreadLocalRandom
    {
        static int seed = Environment.TickCount;

        static readonly ThreadLocal<Random> random =
            new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

        public static int Next(int num)
        {
            return random.Value.Next(num);
        }
    }
}