using DownfallArena.Cli.Table;

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
    public void A_gate_needs_a_token_to_be_a_token_gate()
    {
        Should.Throw<ArgumentException>(() => OperatorGate.WithToken(" "));
    }
}
