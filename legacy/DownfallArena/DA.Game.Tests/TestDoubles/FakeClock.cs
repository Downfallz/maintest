using DA.Game.Shared.Utilities;

namespace DA.Game.Tests.TestDoubles
{
    internal class FakeClock : IClock
    {
        public DateTime UtcNow => new DateTime(2000, 1, 1, 13, 0, 0);
    }
}
