using DownfallArena.Application.Matches.Scenarios;

namespace DownfallArena.Application.Tests.Matches.Scenarios;

/// <summary>
/// A scenario's recorded boards against the boards of the match rebuilt from it: every value recorded found as it was,
/// and nothing asked of what the boards gained since.
/// </summary>
public sealed class ScenarioCheckpointTests
{
    private static readonly ScenarioCheckpoint Recorded = new(4, """{"roundNumber":4,"allies":[{"id":1,"health":20}]}""", """{"roundNumber":4,"allies":[{"id":3,"health":12}]}""");

    [Fact]
    public void Boards_that_gained_a_field_since_the_cut_still_stand_where_the_source_stood()
    {
        var rebuilt = Recorded with { Player1 = """{"roundNumber":4,"allies":[{"id":1,"health":20,"ward":0}],"tempo":"Quick"}""" };

        Recorded.StandsIn(rebuilt).ShouldBeTrue();
    }

    [Fact]
    public void A_recorded_value_that_changed_is_not_where_the_source_stood()
    {
        var rebuilt = Recorded with { Player2 = """{"roundNumber":4,"allies":[{"id":3,"health":11}]}""" };

        Recorded.StandsIn(rebuilt).ShouldBeFalse();
    }

    [Fact]
    public void A_recorded_field_the_boards_no_longer_have_is_not_where_the_source_stood()
    {
        var rebuilt = Recorded with { Player1 = """{"roundNumber":4,"allies":[{"id":1}]}""" };

        Recorded.StandsIn(rebuilt).ShouldBeFalse();
    }

    [Fact]
    public void A_creature_more_or_less_is_not_where_the_source_stood()
    {
        var rebuilt = Recorded with { Player1 = """{"roundNumber":4,"allies":[{"id":1,"health":20},{"id":2,"health":20}]}""" };

        Recorded.StandsIn(rebuilt).ShouldBeFalse();
    }

    [Fact]
    public void Another_round_is_not_where_the_source_stood()
    {
        Recorded.StandsIn(Recorded with { Round = 5 }).ShouldBeFalse();
    }
}
