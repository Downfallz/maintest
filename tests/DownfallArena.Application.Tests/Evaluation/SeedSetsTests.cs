using DownfallArena.Application.Evaluation;

namespace DownfallArena.Application.Tests.Evaluation;

public sealed class SeedSetsTests
{
    [Fact]
    public void The_same_list_has_the_same_identity_and_order_matters()
    {
        SeedSets.IdentityOf([1, 2, 3]).ShouldBe(SeedSets.IdentityOf([1, 2, 3]));
        SeedSets.IdentityOf([1, 2, 3]).ShouldNotBe(SeedSets.IdentityOf([3, 2, 1]));
        SeedSets.IdentityOf([1, 2, 3]).ShouldNotBe(SeedSets.IdentityOf([1, 2]));
        SeedSets.IdentityOf([]).ShouldBe(17);
        SeedSets.IdentityOf([int.MaxValue, int.MaxValue]).ShouldBe(SeedSets.IdentityOf([int.MaxValue, int.MaxValue]));
        Should.Throw<ArgumentNullException>(() => SeedSets.IdentityOf(null!));
    }
}
