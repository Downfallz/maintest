using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A table's keys name the table (ADR 0159, phase 4.3): a host that does not hold it reads which one off the key.</summary>
public sealed class TableKeysTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry = new();

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

    [Fact]
    public async Task Every_key_a_table_opens_with_names_it()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.TwoPeople, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();

        string?[] keys = [table.Pilot.Token, .. table.Seats.Select(seat => seat.Token), .. table.Seats.Select(seat => seat.Invitation)];

        keys.ShouldAllBe(key => TableKeys.TableOf(key) == table.Id);
    }

    [Fact]
    public async Task A_seat_offered_again_gets_keys_that_name_its_table()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.TwoPeople, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();
        var seat = table.Seats[1];
        var before = seat.Token;

        _registry.Rekey(table, seat, SeatKeys.Offered(table.Id)).ShouldBeTrue();

        seat.Token.ShouldNotBe(before);
        TableKeys.TableOf(seat.Token).ShouldBe(table.Id);
        TableKeys.TableOf(seat.Invitation).ShouldBe(table.Id);
        _registry.ByToken(seat.Token).ShouldBeSameAs(table);
    }
}
