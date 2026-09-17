using System.Text;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The short code is the only part of the boundary a person handles: they read it off a screen across a table
/// and type it into a phone. What these hold is that it reaches exactly one seat, that reading it slightly
/// wrong still works, and that it is never a way to reach a seat it does not name.
/// </summary>
public sealed class JoinCodesTests : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
    }

    [Fact]
    public void A_seat_a_person_holds_gets_a_code_and_a_seat_a_bot_plays_does_not()
    {
        var person = Seat(PlayerSlot.Player1, "token-1", held: true);
        var bot = Seat(PlayerSlot.Player2, "token-2", held: false);

        var codes = new JoinCodes([person, bot]);

        codes.Of(person).ShouldNotBeNullOrWhiteSpace();
        codes.Of(bot).ShouldBeNull();
    }

    /// <summary>
    /// Eight characters of an alphabet without I, L, O or U: short enough to read across a table, and 40 bits,
    /// which is not a thing anybody guesses at network speed.
    /// </summary>
    [Fact]
    public void A_code_is_eight_characters_a_person_can_read_aloud()
    {
        var seat = Seat(PlayerSlot.Player1, "token-1", held: true);

        var code = new JoinCodes([seat]).ShouldNotBeNull().Of(seat)!;

        code.Length.ShouldBe(8);
        code.ShouldNotContain("I");
        code.ShouldNotContain("L");
        code.ShouldNotContain("O");
        code.ShouldNotContain("U");
        code.ShouldAllBe(character => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(character, StringComparison.Ordinal));
    }

    [Fact]
    public void Two_seats_are_two_codes()
    {
        var first = Seat(PlayerSlot.Player1, "token-1", held: true);
        var second = Seat(PlayerSlot.Player2, "token-2", held: true);

        var codes = new JoinCodes([first, second]);

        codes.Of(first).ShouldNotBe(codes.Of(second));
    }

    /// <summary>The page is reached with the seat's token on it, which is what it then keeps.</summary>
    [Fact]
    public void A_typed_code_leads_to_the_page_carrying_that_seat_s_token()
    {
        var seat = Seat(PlayerSlot.Player2, "token-2", held: true);
        var codes = new JoinCodes([seat]);

        var answer = codes.Answer($"{JoinCodes.Prefix}{codes.Of(seat)}");

        answer.Status.ShouldBe(303);
        answer.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Location" && header.Value == "/?player2=token-2");
    }

    /// <summary>
    /// A code is read off a screen and typed on a phone keyboard. Case is not information, and the characters
    /// the alphabet leaves out are exactly the ones a reader puts in their place.
    /// </summary>
    [Fact]
    public void A_code_read_slightly_wrong_still_reaches_its_seat()
    {
        var seat = Seat(PlayerSlot.Player1, "token-1", held: true);
        var codes = new JoinCodes([seat]);
        var code = codes.Of(seat)!;

        var lower = codes.Answer($"{JoinCodes.Prefix}{code.ToLowerInvariant()}");
        var mistaken = codes.Answer($"{JoinCodes.Prefix}{code.Replace('0', 'O').Replace('1', 'l')}");
        var trailing = codes.Answer($"{JoinCodes.Prefix}{code}/");

        lower.Status.ShouldBe(303);
        mistaken.Status.ShouldBe(303);
        trailing.Status.ShouldBe(303);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ZZZZZZZZ")]
    [InlineData("../api/seat/player1")]
    public void A_code_this_table_never_minted_names_no_seat(string code)
    {
        var codes = new JoinCodes([Seat(PlayerSlot.Player1, "token-1", held: true)]);

        var answer = codes.Answer($"{JoinCodes.Prefix}{code}");

        answer.Status.ShouldBe(404);
        Encoding.UTF8.GetString(answer.Body).ShouldNotContain("token-1");
    }

    [Theory]
    [InlineData("/j/ABCD1234", true)]
    [InlineData("/api/session", false)]
    [InlineData("/", false)]
    public void The_host_knows_a_join_path_from_everything_else_it_serves(string path, bool expected)
    {
        JoinCodes.Names(path).ShouldBe(expected);
    }

    private TableSeat Seat(PlayerSlot slot, string token, bool held) =>
        new(slot, token, held ? new HumanSeat(_stopping.Token) : null);
}
