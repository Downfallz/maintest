namespace DownfallArena.Cli.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void The_studio_command_defaults_to_the_repository_layout()
    {
        var options = CliOptions.Parse(["studio"]);

        options.Command.ShouldBe("studio");
        options.Data.ShouldBe(CliOptions.DefaultData);
        options.Port.ShouldBe(CliOptions.DefaultPort);
        options.SchemaPath.ShouldBe(CliOptions.DefaultSchemaPath);
    }

    [Fact]
    public void The_studio_takes_its_own_port_and_content_directory()
    {
        var options = CliOptions.Parse(["studio", "--port", "5100", "--data", "other/data"]);

        options.Port.ShouldBe(5100);
        options.Data.ShouldBe("other/data");
    }

    /// <summary>A bad port is one line to read, not a stack from the host trying to bind it.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    public void A_port_the_host_cannot_bind_is_refused_while_parsing(string port)
    {
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["studio", "--port", port]))
            .Message.ShouldContain("between 1 and 65535");
    }

    [Fact]
    public void An_unknown_option_names_itself()
    {
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["studio", "--porrt", "5100"]))
            .Message.ShouldContain("--porrt");
    }

    /// <summary>A recording says nothing about traces, and keeps one per match the way it always did.</summary>
    [Fact]
    public void A_recorded_run_keeps_every_trace_unless_it_is_told_a_number()
    {
        CliOptions.Parse(["simulate", "--record", "runs/a"]).Traces.ShouldBeNull();
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("8", 8)]
    public void A_recorded_run_takes_the_traces_it_is_asked_for(string argument, int expected)
    {
        CliOptions.Parse(["simulate", "--record", "runs/a", "--traces", argument]).Traces.ShouldBe(expected);
    }

    /// <summary>Caught here, or a mistyped count silently becomes a run that keeps no trace at all.</summary>
    [Fact]
    public void A_negative_trace_count_is_refused_while_parsing()
    {
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["simulate", "--record", "runs/a", "--traces", "-1"]))
            .Message.ShouldContain("not a count");
    }

    /// <summary>`--trace` writes one match and `--traces` caps a recording; neither is a typo for the other.</summary>
    [Fact]
    public void The_one_trace_of_play_and_the_trace_count_of_a_recording_are_separate_options()
    {
        var options = CliOptions.Parse(["simulate", "--record", "runs/a", "--traces", "2", "--trace", "match.json"]);

        options.Traces.ShouldBe(2);
        options.Trace.ShouldBe("match.json");
    }

    /// <summary>
    /// The table's own three. An option the allowlist does not hold is refused as unknown, so a command line the
    /// documents print would not run at all — which is how the missing <c>--rules</c> was found.
    /// </summary>
    [Fact]
    public void The_table_takes_the_rule_set_the_round_to_hand_over_on_and_the_seats()
    {
        var options = CliOptions.Parse(["table", "--port", "5100", "--rules", "tabletop.json", "--handover", "10", "--p2", "greedy"]);

        options.Rules.ShouldBe("tabletop.json");
        options.Handover.ShouldBe(10);
        options.Player2Named.ShouldBeTrue();
        options.Player1Named.ShouldBeFalse();
    }

    /// <summary>A table told nothing plays the engine's default rule set, and seats a person in both slots.</summary>
    [Fact]
    public void A_table_told_nothing_names_no_rule_set_and_no_agent()
    {
        var options = CliOptions.Parse(["table"]);

        options.Rules.ShouldBeNull();
        options.Handover.ShouldBeNull();
        options.Player1Named.ShouldBeFalse();
        options.Player2Named.ShouldBeFalse();
    }

    /// <summary>Round zero is not a round; the table would hand over to nobody.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    public void A_handover_round_that_does_not_exist_is_refused_while_parsing(string round)
    {
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["table", "--handover", round]))
            .Message.ShouldContain("not a round to hand over on");
    }
}
