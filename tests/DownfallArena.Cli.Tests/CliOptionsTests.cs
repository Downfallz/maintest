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
    /// The table's own four. An option the allowlist does not hold is refused as unknown, so a command line the
    /// documents print would not run at all — which is how the missing <c>--rules</c> was found.
    /// </summary>
    [Fact]
    public void The_table_takes_the_rule_set_the_round_to_hand_over_on_the_seats_and_who_is_playing()
    {
        var options = CliOptions.Parse(["table", "--port", "5100", "--rules", "tabletop.json", "--handover", "10", "--p2", "greedy", "--who", "mk"]);

        options.Rules.ShouldBe("tabletop.json");
        options.Handover.ShouldBe(10);
        options.Player2Named.ShouldBeTrue();
        options.Player1Named.ShouldBeFalse();
        options.Who.ShouldBe("mk");
    }

    /// <summary>
    /// A table that leaves the disk as it found it. Recording stays on by default, because a playtest nobody
    /// recorded teaches nothing and the flag is the one thing anybody would forget; turning it off is said.
    /// </summary>
    [Fact]
    public void A_table_records_unless_it_is_told_not_to()
    {
        CliOptions.Parse(["table", "--rules", "tabletop.json"]).Recording.ShouldBeTrue();
        CliOptions.Parse(["table", "--rules", "tabletop.json", "--no-record"]).Recording.ShouldBeFalse();
    }

    /// <summary>A valueless flag must not swallow whatever follows it, which is how one becomes a path.</summary>
    [Fact]
    public void The_flag_that_records_nothing_carries_no_value()
    {
        var options = CliOptions.Parse(["table", "--no-record", "--rules", "tabletop.json", "--who", "mk"]);

        options.Recording.ShouldBeFalse();
        options.Rules.ShouldBe("tabletop.json");
        options.Who.ShouldBe("mk");
    }

    /// <summary>Asking for both is asking for opposite things, and is refused by name rather than resolved.</summary>
    [Fact]
    public void Recording_nowhere_and_recording_somewhere_cannot_both_be_asked_for()
    {
        var both = Should.Throw<ArgumentException>(() => CliOptions.Parse(["table", "--no-record", "--record", "runs/playtest"]));

        both.Message.ShouldContain("--no-record");
        both.Message.ShouldContain("--record");
    }

    /// <summary>The line docs/learning/artifacts.md prints, which has to run.</summary>
    [Fact]
    public void The_recorded_session_command_line_the_documents_print_parses()
    {
        var options = CliOptions.Parse(["table", "--rules", "tabletop.json", "--who", "mk"]);

        options.Who.ShouldBe("mk");
        options.Record.ShouldBeNull();
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

    /// <summary>
    /// The default is this machine and no other. A playtest on a phone is the reason the option exists, and a
    /// table that bound the network without being asked would be the wrong default in the other direction.
    /// </summary>
    [Fact]
    public void A_table_binds_this_machine_unless_it_is_told_an_address()
    {
        CliOptions.Parse(["table"]).Bind.ShouldBe("127.0.0.1");
        CliOptions.Parse(["table", "--bind", "192.168.1.12"]).Bind.ShouldBe("192.168.1.12");
    }

    /// <summary>A typo in an address is one line here, not a stack out of the listener.</summary>
    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("localhost")]
    [InlineData("192.168.1")]
    public void An_address_no_host_can_bind_is_refused_while_parsing(string address)
    {
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["table", "--bind", address]));
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
