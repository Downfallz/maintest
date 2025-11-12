using DA.Game.Shared;

namespace DA.Game.Tests.TestDoubles
{
    internal class FixedRandom : IRandom
    {
        public int Next(int min, int max)
        {
            var rng = new Random(1);
            return rng.Next(min, max);
        }
    }
}
