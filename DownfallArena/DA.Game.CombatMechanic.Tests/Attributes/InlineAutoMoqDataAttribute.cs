using AutoFixture.Xunit2;

namespace DA.Game.CombatMechanic.Tests.Attributes
{
    public class InlineAutoMoqDataAttribute : InlineAutoDataAttribute
    {
        public InlineAutoMoqDataAttribute(params object[] values)
            : base(new AutoMoqDataAttribute(), values)
        {
        }
    }
}
