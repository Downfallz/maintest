using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>What a table keeps of its reactions and when a seat may send the next one (ADR 0173).</summary>
public sealed class TableReactionsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_seat_reacts_again_once_the_cooldown_has_passed()
    {
        var clock = new Clock(Start);
        var reactions = new TableReactions(clock);
        await reactions.SendAsync("player1", "hello", Cancel);
        clock.Now = Start + TableReactions.Cooldown;

        var sent = await reactions.SendAsync("player1", "thanks", Cancel);

        sent.Refusal.ShouldBeNull();
        sent.Reaction.ShouldNotBeNull().Sequence.ShouldBe(2);
    }

    [Fact]
    public async Task The_table_keeps_only_its_last_reactions()
    {
        var clock = new Clock(Start);
        var reactions = new TableReactions(clock);
        for (var sent = 0; sent < TableReactions.Kept + 3; sent++)
        {
            clock.Now = Start + (TableReactions.Cooldown * sent);
            await reactions.SendAsync("player1", "hello", Cancel);
        }

        var recent = await reactions.RecentAsync(Cancel);

        recent.Count.ShouldBe(TableReactions.Kept);
        recent[0].Sequence.ShouldBe(4);
        recent[^1].Sequence.ShouldBe(TableReactions.Kept + 3);
    }

    [Fact]
    public async Task A_reaction_another_process_kept_first_is_read_and_the_new_one_goes_after_it()
    {
        var keeper = new Keeper();
        var reactions = new TableReactions(new Clock(Start), keeper);
        await reactions.RecentAsync(Cancel);
        keeper.Elsewhere(new TableReaction(1, "player2", "hello", Start));

        var sent = await reactions.SendAsync("player1", "good-luck", Cancel);

        sent.Reaction.ShouldNotBeNull().Sequence.ShouldBe(2);
        keeper.Kept.Select(one => one.Reaction).ShouldBe(["hello", "good-luck"]);
    }

    [Fact]
    public async Task A_kept_reaction_the_set_no_longer_offers_is_not_served()
    {
        var keeper = new Keeper();
        keeper.Elsewhere(new TableReaction(1, "player2", "retired", Start));
        var reactions = new TableReactions(new Clock(Start), keeper);

        var recent = await reactions.RecentAsync(Cancel);

        recent.ShouldBeEmpty();
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A record kept in memory, whose tag moves with every write, the way a blob's does.</summary>
    private sealed class Keeper : IReactionKeeper
    {
        private int _version;

        public IReadOnlyList<TableReaction> Kept { get; private set; } = [];

        public void Elsewhere(TableReaction reaction)
        {
            Kept = [.. Kept, reaction];
            _version++;
        }

        public Task<(IReadOnlyList<TableReaction> Items, string? Tag)> ReactionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult((Kept, _version == 0 ? null : (string?)_version.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        public Task<string?> WriteReactionsIfAsync(IReadOnlyList<TableReaction> items, string? expected, CancellationToken cancellationToken = default)
        {
            var current = _version == 0 ? null : _version.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (current != expected)
            {
                return Task.FromResult<string?>(null);
            }

            Kept = items;
            _version++;
            return Task.FromResult<string?>(_version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
