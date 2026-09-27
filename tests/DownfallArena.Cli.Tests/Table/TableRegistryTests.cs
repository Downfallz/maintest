using System.Text;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A host of tables finds each one by what a request carries (ADR 0081). What these hold is that a token
/// reaches its own table and no other, that a code does the same, and that a table nobody is at is let go of.
/// </summary>
public sealed class TableRegistryTests : IDisposable
{
    private readonly HostedTables _hosted = new();

    public void Dispose() => _hosted.Dispose();

    [Fact]
    public async Task A_seat_token_names_its_table_and_no_other()
    {
        using var registry = new TableRegistry();
        using var first = await Composed(HostedTables.OnePerson);
        using var second = await Composed(HostedTables.OnePerson);
        registry.TryAdd(first).ShouldBeTrue();
        registry.TryAdd(second).ShouldBeTrue();

        registry.ByToken(first.Seats[0].Token).ShouldBeSameAs(first);
        registry.ByToken(second.Seats[0].Token).ShouldBeSameAs(second);
        registry.ByToken(first.Pilot.Token).ShouldBeSameAs(first);
        registry.ByToken("nobody-s-token").ShouldBeNull();
        registry.ByToken(null).ShouldBeNull();
    }

    [Fact]
    public async Task A_code_typed_at_the_host_reaches_the_seat_it_was_minted_for()
    {
        using var registry = new TableRegistry();
        using var first = await Composed(HostedTables.OnePerson);
        using var second = await Composed(HostedTables.OnePerson);
        registry.TryAdd(first);
        registry.TryAdd(second);

        var code = registry.Codes.Of(second.Seats[0]).ShouldNotBeNull();
        var answer = registry.Codes.Answer($"{JoinCodes.Prefix}{code}");

        answer.Status.ShouldBe(303);
        answer.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Location" && header.Value == $"/?player1={second.Seats[0].Token}");
        registry.Codes.Of(first.Seats[0]).ShouldNotBe(code);
        registry.Codes.Of(first.Seats[1]).ShouldBeNull("a bot's seat has nobody to hand a code to");
    }

    [Fact]
    public async Task A_table_removed_is_found_by_nothing_it_used_to_be_found_by()
    {
        using var registry = new TableRegistry();
        var table = await Composed(HostedTables.OnePerson);
        registry.TryAdd(table);
        var token = table.Seats[0].Token;
        var code = registry.Codes.Of(table.Seats[0])!;

        registry.Remove(table.Id).ShouldBeTrue();

        registry.ByToken(token).ShouldBeNull();
        registry.ById(table.Id).ShouldBeNull();
        registry.Codes.Answer($"{JoinCodes.Prefix}{code}").Status.ShouldBe(404);
        registry.Remove(table.Id).ShouldBeFalse();

        // Removing stops the match: the person's seat is released and the driver ends without an outcome.
        await Should.ThrowAsync<OperationCanceledException>(() => table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_host_takes_only_so_many_tables_under_way_and_a_finished_one_does_not_count()
    {
        using var registry = new TableRegistry(mostUnderWay: 1);
        using var finished = await Composed(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        using var first = await Composed(HostedTables.OnePerson);
        using var second = await Composed(HostedTables.OnePerson);

        registry.TryAdd(finished).ShouldBeTrue();
        registry.TryAdd(first).ShouldBeTrue("a finished table is not under way");
        registry.TryAdd(second).ShouldBeFalse("one table under way is as many as this host takes");
    }

    [Fact]
    public async Task A_finished_table_is_let_go_of_an_hour_after_the_last_request_and_a_live_one_is_not()
    {
        using var registry = new TableRegistry();
        var finished = await Composed(HostedTables.Bots);
        await finished.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        using var live = await Composed(HostedTables.OnePerson);
        registry.TryAdd(finished);
        registry.TryAdd(live);

        registry.Sweep(finished.CreatedAt + TimeSpan.FromMinutes(10)).ShouldBeEmpty();
        var gone = registry.Sweep(finished.CreatedAt + TableRegistry.FinishedFor);

        gone.ShouldBe([finished.Id]);
        registry.ById(live.Id).ShouldBeSameAs(live);
        registry.All().ShouldBe([live]);
    }

    [Fact]
    public async Task A_table_nobody_asks_anything_of_for_a_day_is_abandoned()
    {
        using var registry = new TableRegistry();
        var idle = await Composed(HostedTables.OnePerson);
        registry.TryAdd(idle);

        // Touched keeps it: a poll is somebody still at the table.
        idle.Touch(idle.CreatedAt + TableRegistry.IdleFor - TimeSpan.FromMinutes(1));
        registry.Sweep(idle.CreatedAt + TableRegistry.IdleFor).ShouldBeEmpty();

        registry.Sweep(idle.CreatedAt + TableRegistry.IdleFor + TableRegistry.IdleFor).ShouldBe([idle.Id]);
        await Should.ThrowAsync<OperationCanceledException>(() => idle.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_table_says_whose_a_token_is()
    {
        using var table = await Composed(HostedTables.OnePerson);

        table.Holds(table.Seats[0].Token).ShouldBeTrue();
        table.Holds(table.Pilot.Token).ShouldBeTrue();
        table.Holds("another").ShouldBeFalse();
        Encoding.UTF8.GetBytes(table.Id).Length.ShouldBeGreaterThan(0);
    }

    private Task<PlayedTable> Composed(TableRequest request) => _hosted.Composer.ComposeAsync(request, TestContext.Current.CancellationToken);
}
