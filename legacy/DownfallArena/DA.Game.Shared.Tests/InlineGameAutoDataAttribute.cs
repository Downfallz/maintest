using AutoFixture.Xunit2;

namespace DA.Game.Shared.Tests;

public sealed class InlineGameAutoDataAttribute : InlineAutoDataAttribute
{
    public InlineGameAutoDataAttribute(params object[] values)
        : base(new GameAutoDataAttribute(), values)
    {
    }
}