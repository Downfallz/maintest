using DownfallArena.Domain.Resources;

namespace DownfallArena.Domain.Tests.Resources;

public sealed class TargetingSpecTests
{
    [Fact]
    public void A_single_target_spec_takes_exactly_one_target()
    {
        var spec = TargetingSpec.Single(TargetOrigin.Enemy);

        spec.Origin.ShouldBe(TargetOrigin.Enemy);
        spec.Scope.ShouldBe(TargetScope.SingleTarget);
        spec.MaxTargets.ShouldBe(1);
    }

    [Fact]
    public void A_multi_target_spec_may_be_unbounded_or_bounded_above_one()
    {
        TargetingSpec.Multi(TargetOrigin.Ally).MaxTargets.ShouldBeNull();
        TargetingSpec.Multi(TargetOrigin.Any, 3).MaxTargets.ShouldBe(3);

        Should.Throw<ArgumentOutOfRangeException>(() => TargetingSpec.Multi(TargetOrigin.Any, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => TargetingSpec.Multi(TargetOrigin.Any, 0));
    }

    [Fact]
    public void Specs_are_compared_by_value()
    {
        TargetingSpec.Single(TargetOrigin.Self).ShouldBe(TargetingSpec.Single(TargetOrigin.Self));
        TargetingSpec.Single(TargetOrigin.Self).ShouldNotBe(TargetingSpec.Multi(TargetOrigin.Self));
    }
}
