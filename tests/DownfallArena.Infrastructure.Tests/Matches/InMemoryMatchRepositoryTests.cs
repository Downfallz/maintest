using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Randomness;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Infrastructure.Tests.Matches;

public sealed class InMemoryMatchRepositoryTests
{
    [Fact]
    public async Task A_saved_match_is_found_by_its_id_and_an_unknown_id_is_not()
    {
        var repository = new InMemoryMatchRepository();
        var match = Match.Create(MatchId.New(), Substitute.For<IGameResources>(), RuleSet.Default, new SeededRandomSource(1));

        await repository.SaveAsync(match, TestContext.Current.CancellationToken);

        (await repository.FindAsync(match.Id, TestContext.Current.CancellationToken)).ShouldBeSameAs(match);
        (await repository.FindAsync(MatchId.New(), TestContext.Current.CancellationToken)).ShouldBeNull();
        repository.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Saving_again_keeps_one_entry_per_match()
    {
        var repository = new InMemoryMatchRepository();
        var match = Match.Create(MatchId.New(), Substitute.For<IGameResources>(), RuleSet.Default, new SeededRandomSource(1));

        await repository.SaveAsync(match, TestContext.Current.CancellationToken);
        await repository.SaveAsync(match, TestContext.Current.CancellationToken);

        repository.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_null_match_is_rejected()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => new InMemoryMatchRepository().SaveAsync(null!, TestContext.Current.CancellationToken));
    }
}
