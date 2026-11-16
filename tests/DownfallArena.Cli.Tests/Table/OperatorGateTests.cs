using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Who may open a table, and how a request proves it: the console's token on a laptop, the platform's stamp
/// in the container, and never the other one (ADR 0081).
/// </summary>
public sealed class OperatorGateTests
{
    [Fact]
    public void At_a_console_the_token_it_printed_is_the_operator_and_nothing_else_is()
    {
        var gate = OperatorGate.WithToken("printed");

        gate.Admit("printed", principal: null).ShouldBe("operator");
        gate.Admit("printed", principal: "mark@example.test").ShouldBe("operator");
        gate.Admit("printe", principal: null).ShouldBeNull();
        gate.Admit(null, principal: "mark@example.test").ShouldBeNull("a header anybody can type is not a door on a laptop");
        gate.Admit("", principal: null).ShouldBeNull();
        gate.TrustsPlatform.ShouldBeFalse();
    }

    [Fact]
    public void Behind_the_platform_the_stamped_principal_is_the_operator_by_name()
    {
        var gate = OperatorGate.BehindPlatform();

        gate.Admit(token: null, principal: " mark@example.test ").ShouldBe("mark@example.test");
        gate.Admit(token: "anything", principal: null).ShouldBeNull("a token is not a sign-in");
        gate.Admit(token: null, principal: "  ").ShouldBeNull();
        gate.TrustsPlatform.ShouldBeTrue();
        gate.Token.ShouldBeNull();
    }

    [Fact]
    public void A_signed_in_admin_account_is_the_operator_by_its_name_behind_either_door()
    {
        var owner = Signed("Downfallz");
        var stranger = Signed("Someone");

        foreach (var gate in new[] { OperatorGate.WithToken("printed"), OperatorGate.BehindPlatform() })
        {
            var admins = gate.WithAdmins([owner.Id]);

            admins.Admit(token: null, principal: null, owner).ShouldBe("Downfallz");
            admins.Admit(token: null, principal: null, stranger).ShouldBeNull("a player is not the operator unless named");
            gate.Admit(token: null, principal: null, owner).ShouldBeNull("no account is an admin unless the host says so");
            admins.TrustsPlatform.ShouldBe(gate.TrustsPlatform, "the other door stays as it was");
        }

        OperatorGate.WithToken("printed").WithAdmins([owner.Id]).Admit("printed", principal: null, stranger).ShouldBe("operator");
    }

    [Fact]
    public void Admin_accounts_are_named_by_their_ids_and_a_name_is_refused()
    {
        var id = AccountId.New();

        OperatorGate.ParseAdmins($" {id.Value} , ").Admins.ShouldBe([id]);
        OperatorGate.ParseAdmins(null).Admins.ShouldBeEmpty();
        OperatorGate.ParseAdmins("downfallz").Problem.ShouldNotBeNull().ShouldContain("'downfallz' is not an account id");
    }

    private static Account Signed(string name) =>
        new(AccountId.New(), new ExternalIdentity(ExternalIdentity.Development, name), Username.Restore(name), DateTimeOffset.UnixEpoch, 0);

    [Fact]
    public void A_gate_needs_a_token_to_be_a_token_gate()
    {
        Should.Throw<ArgumentException>(() => OperatorGate.WithToken(" "));
    }
}
