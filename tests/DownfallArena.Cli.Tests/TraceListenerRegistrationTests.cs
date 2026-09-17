using DownfallArena.Application.Learning.Tracing;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Tests;

/// <summary>
/// Which commands get a <see cref="MatchTraceRecorder"/>. It keeps every event of every match it sees and only
/// gives one back when something completes it, so registering it where nothing will is a leak and not a waste.
/// </summary>
public sealed class TraceListenerRegistrationTests
{
    [Theory]
    [InlineData("play", "--trace", "match.json")]
    [InlineData("human", "--trace", "match.json")]
    public void The_commands_that_write_one_trace_get_the_recorder(string command, string option, string value)
    {
        Registered([command, option, value]).ShouldBeTrue();
    }

    [Fact]
    public void A_recorded_run_that_keeps_traces_gets_the_recorder()
    {
        Registered(["simulate", "--record", "runs/a"]).ShouldBeTrue();
        Registered(["simulate", "--record", "runs/a", "--traces", "4"]).ShouldBeTrue();
    }

    /// <summary>The whole point of `--traces 0`: the board projections are never built, not built and dropped.</summary>
    [Fact]
    public void A_recorded_run_that_keeps_no_trace_does_not_get_the_recorder()
    {
        Registered(["simulate", "--record", "runs/a", "--traces", "0"]).ShouldBeFalse();
    }

    /// <summary>
    /// `--trace` is read by `play` alone, so on `simulate` it names no one who would complete a match. With the
    /// recorder registered and the run keeping no trace, every event of every match in the batch would be held
    /// until the process ended.
    /// </summary>
    [Fact]
    public void A_recording_that_keeps_no_trace_is_not_given_the_recorder_by_an_unread_trace_option()
    {
        Registered(["simulate", "--record", "runs/a", "--traces", "0", "--trace", "match.json"]).ShouldBeFalse();
    }

    [Fact]
    public void A_command_that_records_nothing_does_not_get_the_recorder()
    {
        Registered(["evaluate", "--p1", "greedy", "--p2", "random"]).ShouldBeFalse();
        Registered(["simulate", "--matches", "10"]).ShouldBeFalse();
    }

    private static bool Registered(string[] arguments)
    {
        var services = new ServiceCollection();
        GameSession.AddListeners(services, CliOptions.Parse(arguments));
        return services.Any(service => service.ServiceType == typeof(MatchTraceRecorder));
    }
}
