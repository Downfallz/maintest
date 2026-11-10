using System.Text;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The short code is the only part of the boundary a person handles: they read it off a screen across a table
/// and type it into a phone. What these hold is what a code looks like, that reading it slightly wrong still
/// reads it as the code that was drawn, and that its answer carries a seat's key only for a seat a link may name.
/// Which seat a code opens is the index's and the seat's (ADR 0161): <see cref="TableRegistryTests" /> holds that.
/// </summary>
public sealed class JoinCodesTests
{
    /// <summary>
    /// Eight characters of an alphabet without I, L, O or U: short enough to read across a table, and 40 bits,
    /// which is not a thing anybody guesses at network speed.
    /// </summary>
    [Fact]
    public void A_code_is_eight_characters_a_person_can_read_aloud()
    {
        var code = JoinCodes.Draw();

        code.Length.ShouldBe(8);
        code.ShouldNotContain("I");
        code.ShouldNotContain("L");
        code.ShouldNotContain("O");
        code.ShouldNotContain("U");
        code.ShouldAllBe(character => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(character, StringComparison.Ordinal));
    }

    [Fact]
    public void Two_codes_drawn_are_two_codes()
    {
        JoinCodes.Draw().ShouldNotBe(JoinCodes.Draw());
    }

    /// <summary>
    /// A code is read off a screen and typed on a phone keyboard. Case is not information, and the characters
    /// the alphabet leaves out are exactly the ones a reader puts in their place.
    /// </summary>
    [Fact]
    public void A_code_read_slightly_wrong_is_read_back_as_the_code_that_was_drawn()
    {
        const string code = "K7F2Q9X0";

        JoinCodes.In($"{JoinCodes.Prefix}{code.ToLowerInvariant()}").ShouldBe(code);
        JoinCodes.In($"{JoinCodes.Prefix}{code.Replace('0', 'O')}").ShouldBe(code);
        JoinCodes.In($"{JoinCodes.Prefix}{code}/").ShouldBe(code);
        JoinCodes.In($"{JoinCodes.Prefix}1L1I0000").ShouldBe("11110000");
        JoinCodes.In($"{JoinCodes.Prefix}VUVU0000").ShouldBe("VVVV0000");
    }

    [Theory]
    [InlineData("/j/")]
    [InlineData("/j/ABCD123")]
    [InlineData("/j/ABCD12345")]
    [InlineData("/j/../api/seat/player1")]
    [InlineData("/j/ABCD-123")]
    [InlineData("/api/session")]
    public void A_path_that_cannot_carry_a_code_carries_none(string path)
    {
        JoinCodes.In(path).ShouldBeNull();
    }

    /// <summary>The page is reached with the seat's key on it, which is what it then keeps.</summary>
    [Fact]
    public void A_code_s_answer_leads_to_the_page_carrying_its_seat_s_invitation()
    {
        var seat = new TableSeat(PlayerSlot.Player2, "token-2", new SteppedPerson()) { Invitation = "invitation-2" };

        var answer = JoinCodes.Answer(seat);

        answer.Status.ShouldBe(303);
        answer.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Location" && header.Value == "/?player2=invitation-2");
    }

    /// <summary>A seat recorded before invitations existed is reached by its token, as links did then.</summary>
    [Fact]
    public void A_code_s_answer_carries_the_token_of_a_seat_recorded_before_invitations()
    {
        var seat = new TableSeat(PlayerSlot.Player1, "token-1", new SteppedPerson());

        var answer = JoinCodes.Answer(seat);

        answer.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Location" && header.Value == "/?player1=token-1");
    }

    [Fact]
    public void A_code_s_answer_names_no_seat_a_bot_plays()
    {
        var answer = JoinCodes.Answer(new TableSeat(PlayerSlot.Player2, "token-2", null));

        answer.ShouldNameNoSeat();
        Encoding.UTF8.GetString(answer.Body).ShouldNotContain("token-2");
    }

    /// <summary>A seat somebody took is theirs: its code is spent (ADR 0116), and a second browser is not handed its token.</summary>
    [Fact]
    public void A_code_s_answer_names_no_seat_somebody_took()
    {
        var answer = JoinCodes.Answer(new TableSeat(PlayerSlot.Player1, "token-1", new SteppedPerson()) { Taken = true });

        answer.ShouldNameNoSeat();
        Encoding.UTF8.GetString(answer.Body).ShouldNotContain("token-1");
    }

    /// <summary>A seat kept for a friend is accepted signed in, never by a key (ADR 0129).</summary>
    [Fact]
    public void A_code_s_answer_names_no_seat_kept_for_a_friend()
    {
        var seat = new TableSeat(PlayerSlot.Player2, "token-2", new SteppedPerson()) { Invitation = "invitation-2", InvitedAccount = AccountId.New() };

        var answer = JoinCodes.Answer(seat);

        answer.ShouldNameNoSeat();
        Encoding.UTF8.GetString(answer.Body).ShouldNotContain("invitation-2");
    }

    [Fact]
    public void A_code_s_answer_names_no_seat_when_the_code_names_none()
    {
        JoinCodes.Answer(null).ShouldNameNoSeat();
    }

    [Theory]
    [InlineData("/j/ABCD1234", true)]
    [InlineData("/api/session", false)]
    [InlineData("/", false)]
    public void The_host_knows_a_join_path_from_everything_else_it_serves(string path, bool expected)
    {
        JoinCodes.Names(path).ShouldBe(expected);
    }
}
