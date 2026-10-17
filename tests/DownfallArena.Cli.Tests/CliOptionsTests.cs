using DownfallArena.Domain.Matches;

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

    [Theory]
    [InlineData("3v3", 3)]
    [InlineData("2v2", 2)]
    [InlineData("1v1", 1)]
    [InlineData("1", 1)]
    public void A_command_plays_the_format_it_is_given(string text, int teamSize) =>
        CliOptions.Parse(["play", "--format", text]).Format!.Value.TeamSize.ShouldBe(teamSize);

    /// <summary>
    /// The option absent is not 3v3: it is "play the rule set as it stands", which for a table is the file
    /// <c>--rules</c> named. Every command line written before the option existed keeps its meaning.
    /// </summary>
    [Fact]
    public void A_command_given_no_format_plays_the_rule_set_as_it_stands() =>
        CliOptions.Parse(["play"]).Format.ShouldBeNull();

    [Theory]
    [InlineData("3v2")]
    [InlineData("0v0")]
    [InlineData("four")]
    public void A_format_that_is_not_a_side_against_itself_is_refused_while_parsing(string text) =>
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["play", "--format", text]))
            .Message.ShouldContain("not a match format");

    /// <summary>
    /// The digest is committed per content hash (ADR 0013, decision I), so a benchmark of another format would
    /// be verified against 3v3's and read as an engine change. Refused by name rather than compared.
    /// </summary>
    [Fact]
    public void The_benchmark_refuses_a_format_because_its_digest_is_per_content_hash() =>
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["benchmark", "--format", "2v2"]))
            .Message.ShouldContain("digest is committed per content hash");

    [Fact]
    public void The_studio_refuses_a_format_because_it_plays_no_match() =>
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["studio", "--format", "2v2"]))
            .Message.ShouldContain("plays no match of its own");

    /// <summary>Practice supplies its own rules, and a format is one of them.</summary>
    [Fact]
    public void Practice_refuses_a_format_with_the_rest_of_what_it_supplies_itself() =>
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["table", "--practice", "--format", "2v2"]))
            .Message.ShouldContain("--practice");

    [Fact]
    public void A_command_that_plays_matches_records_its_slots_where_it_is_told() =>
        CliOptions.Parse(["evaluate", "--slot-log", "slots.jsonl"]).SlotLog.ShouldBe("slots.jsonl");

    [Fact]
    public void The_table_refuses_a_slot_log_because_it_is_for_the_commands_that_play_bots_against_bots() =>
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["table", "--slot-log", "slots.jsonl"]))
            .Message.ShouldContain("'--slot-log' is for play, simulate and evaluate");

    /// <summary>ADR 0110: a command names the combat declaration it plays, as the usage line spells it.</summary>
    [Theory]
    [InlineData("hidden-intent", CombatDeclaration.HiddenIntent)]
    [InlineData("at-activation", CombatDeclaration.AtActivation)]
    public void A_command_plays_the_combat_declaration_it_is_given(string text, CombatDeclaration declaration) =>
        CliOptions.Parse(["simulate", "--declaration", text]).Declaration.ShouldBe(declaration);

    /// <summary>Absent is not hidden-intent: it is the rule set as it stands, so every earlier command line keeps its meaning.</summary>
    [Fact]
    public void A_command_given_no_declaration_plays_the_rule_set_as_it_stands() =>
        CliOptions.Parse(["play"]).Declaration.ShouldBeNull();

    [Theory]
    [InlineData("AtActivation")]
    [InlineData("free")]
    [InlineData("")]
    public void A_declaration_the_engine_does_not_know_is_refused_while_parsing(string text) =>
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["play", "--declaration", text]))
            .Message.ShouldContain("not a combat declaration");

    /// <summary>
    /// The benchmark's digest is per content hash and measured on the default rule, and the studio plays no match
    /// (ADR 0110): each refuses the option by name. Practice plays its own rules and refuses it too.
    /// </summary>
    [Theory]
    [InlineData("benchmark")]
    [InlineData("studio")]
    public void A_command_that_plays_no_declaration_of_its_own_refuses_one(string command) =>
        Should.Throw<ArgumentException>(() => CliOptions.Parse([command, "--declaration", "at-activation"]))
            .Message.ShouldContain($"'--declaration' is not for {command}");

    [Fact]
    public void Practice_refuses_a_declaration_since_it_plays_rules_of_its_own() =>
        Should.Throw<ArgumentException>(() => CliOptions.Parse(["table", "--practice", "--declaration", "at-activation"]))
            .Message.ShouldContain("--practice");

    [Theory]
    [InlineData("play")]
    [InlineData("human")]
    [InlineData("evaluate")]
    [InlineData("table")]
    public void The_commands_that_play_matches_take_a_declaration(string command) =>
        CliOptions.Parse([command, "--declaration", "at-activation"]).Declaration.ShouldBe(CombatDeclaration.AtActivation);

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

    /// <summary>A lobby host starts with no table, and trusts the platform's sign-in only when told (ADR 0081).</summary>
    [Fact]
    public void A_lobby_host_is_asked_for_and_so_is_trusting_the_platform()
    {
        var options = CliOptions.Parse(["table", "--lobby", "--rules", "tabletop.json", "--platform-auth"]);

        options.Lobby.ShouldBeTrue();
        options.PlatformAuth.ShouldBeTrue();
        options.Rules.ShouldBe("tabletop.json");
    }

    [Fact]
    public void A_table_starts_with_one_session_and_trusts_no_header_unless_asked()
    {
        var options = CliOptions.Parse(["table", "--rules", "tabletop.json"]);

        options.Lobby.ShouldBeFalse();
        options.PlatformAuth.ShouldBeFalse();
    }

    /// <summary>What describes the one table a host starts with has nothing to describe on a lobby host.</summary>
    [Theory]
    [InlineData("--p1")]
    [InlineData("--p2")]
    [InlineData("--who")]
    [InlineData("--handover")]
    [InlineData("--seed")]
    public void A_lobby_refuses_to_be_told_about_the_table_it_does_not_start_with(string argument)
    {
        var refused = Should.Throw<ArgumentException>(() => CliOptions.Parse(["table", "--lobby", argument, "1"]));

        refused.Message.ShouldContain("--lobby");
    }

    [Theory]
    [InlineData("table", "--lobby", "--practice")]
    [InlineData("play", "--lobby")]
    [InlineData("studio", "--platform-auth")]
    public void The_lobby_and_the_platform_s_sign_in_are_the_table_s_alone(params string[] args)
    {
        Should.Throw<ArgumentException>(() => CliOptions.Parse(args));
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
