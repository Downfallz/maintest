using System.Security.Cryptography;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A table's keys name the table (ADR 0159, phase 4.3): a host that does not hold it reads which one off the key.</summary>
public sealed class TableKeysTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry;

    public TableKeysTests()
    {
        _registry = _hosted.Registry();
    }

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
    }

    [Fact]
    public void A_key_names_its_table_and_two_keys_of_one_table_differ()
    {
        var first = TableKeys.For("20261110-120000-aaaaaaaa");
        var second = TableKeys.For("20261110-120000-aaaaaaaa");

        TableKeys.TableOf(first).ShouldBe("20261110-120000-aaaaaaaa");
        second.ShouldNotBe(first);
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef")]
    [InlineData(".0123456789abcdef")]
    [InlineData("20261110-120000-aaaaaaaa.")]
    [InlineData("")]
    [InlineData(null)]
    public void A_key_minted_before_keys_named_their_table_names_none(string? key)
    {
        TableKeys.TableOf(key).ShouldBeNull();
    }

    /// <summary>What a request carries is nobody's word, and the table part of a key names a run in the store.</summary>
    [Theory]
    [InlineData("../runs.0123456789abcdef")]
    [InlineData("a table.0123456789abcdef")]
    [InlineData("20261110/120000.0123456789abcdef")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.0123456789abcdef")]
    public void A_key_whose_table_part_could_not_be_a_table_s_id_names_none(string key)
    {
        TableKeys.TableOf(key).ShouldBeNull();
    }

    [Fact]
    public async Task Every_key_a_table_opens_with_names_it()
    {
        var table = await Opened(_registry);

        string?[] keys = [table.Pilot.Token, .. table.Seats.Select(seat => seat.Token), .. table.Seats.Select(seat => seat.Invitation)];

        keys.ShouldAllBe(key => TableKeys.TableOf(key) == table.Id);
    }

    [Fact]
    public async Task A_seat_offered_again_gets_keys_that_name_its_table()
    {
        var table = await Opened(_registry);
        var seat = table.Seats[1];
        var before = seat.Token;

        (await _registry.RekeyAsync(table, seat, SeatKeys.Offered(table.Id), TestContext.Current.CancellationToken)).ShouldBeTrue();

        seat.Token.ShouldNotBe(before);
        TableKeys.TableOf(seat.Token).ShouldBe(table.Id);
        TableKeys.TableOf(seat.Invitation).ShouldBe(table.Id);
        (await _registry.ByTokenAsync(seat.Token, TestContext.Current.CancellationToken)).ShouldBeSameAs(table);
    }

    /// <summary>
    /// A host that does not hold a table -- it started after the one that opened it, or another host opened it --
    /// reads which table off the key and picks it up from its record and journal (ADR 0161).
    /// </summary>
    [Fact]
    public async Task A_key_of_a_table_this_host_does_not_hold_picks_it_up_from_its_record()
    {
        string id;
        string token;
        using (var before = _hosted.Registry())
        {
            var table = await Opened(before);
            await table.RecordAsync(TestContext.Current.CancellationToken);
            id = table.Id;
            token = table.Seats[0].Token;
        }

        var picked = await _registry.ByTokenAsync(token, TestContext.Current.CancellationToken);

        picked.ShouldNotBeNull().Id.ShouldBe(id);
        _registry.Held(id).ShouldBeSameAs(picked);
    }

    /// <summary>A key that names its table is still a key: one the table never had picks nothing up and reaches nothing.</summary>
    [Fact]
    public async Task A_key_naming_a_recorded_table_that_never_had_it_reaches_nothing()
    {
        string id;
        using (var before = _hosted.Registry())
        {
            var table = await Opened(before);
            await table.RecordAsync(TestContext.Current.CancellationToken);
            id = table.Id;
        }

        var reached = await _registry.ByTokenAsync(TableKeys.For(id), TestContext.Current.CancellationToken);

        reached.ShouldBeNull();
    }

    /// <summary>
    /// A key minted before keys named their table (4.3a) says nothing of where it goes: the host is told, as it
    /// starts, from the open records, and the key reaches its table for the life of that table.
    /// </summary>
    [Fact]
    public async Task A_key_minted_before_keys_named_their_table_reaches_it_once_the_host_is_told()
    {
        var legacy = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        string id;
        using (var before = _hosted.Registry())
        {
            var table = await Opened(before);
            await before.RekeyAsync(table, table.Seats[0], SeatKeys.TakenBy(legacy), TestContext.Current.CancellationToken);
            await table.RecordAsync(TestContext.Current.CancellationToken);
            id = table.Id;
        }

        _registry.Remember(legacy, id);
        var reached = await _registry.ByTokenAsync(legacy, TestContext.Current.CancellationToken);

        reached.ShouldNotBeNull().Id.ShouldBe(id);
    }

    [Fact]
    public async Task A_key_minted_before_keys_named_their_table_reaches_nothing_the_host_was_not_told_of()
    {
        var legacy = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        using (var before = _hosted.Registry())
        {
            var table = await Opened(before);
            await before.RekeyAsync(table, table.Seats[0], SeatKeys.TakenBy(legacy), TestContext.Current.CancellationToken);
            await table.RecordAsync(TestContext.Current.CancellationToken);
        }

        var reached = await _registry.ByTokenAsync(legacy, TestContext.Current.CancellationToken);

        reached.ShouldBeNull();
    }

    /// <summary>A table of two people, composed and added as a host adds one.</summary>
    private async Task<PlayedTable> Opened(TableRegistry registry)
    {
        using var place = (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var table = await _hosted.Composer.ComposeAsync(HostedTables.TwoPeople, TestContext.Current.CancellationToken);
        await registry.AddAsync(table, place, TestContext.Current.CancellationToken);
        return table;
    }
}
