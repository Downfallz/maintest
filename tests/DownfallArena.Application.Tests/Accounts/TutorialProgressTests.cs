using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.Tests.Accounts;

/// <summary>What an account keeps of the Tutorials (ADR 0187, onboarding-plan.md O4): done and skipped.</summary>
public sealed class TutorialProgressTests
{
    private const string First = "sc-repo-tutorial-your-first-combat";
    private const string Second = "sc-repo-tutorial-who-acts-first";

    [Fact]
    public void A_tutorial_an_account_neither_finished_nor_skipped_is_new()
    {
        TutorialProgress.None.StateOf(First).ShouldBe(TutorialState.New);
    }

    [Fact]
    public void A_skipped_tutorial_is_skipped_and_the_others_stay_new()
    {
        var kept = TutorialProgress.None.WithSkipped(First);

        (kept.StateOf(First), kept.StateOf(Second)).ShouldBe((TutorialState.Skipped, TutorialState.New));
    }

    [Fact]
    public void A_tutorial_finished_after_it_was_skipped_is_done_and_no_longer_skipped()
    {
        var kept = TutorialProgress.None.WithSkipped(First).WithDone(First);

        kept.StateOf(First).ShouldBe(TutorialState.Done);
        kept.Skipped.ShouldBeEmpty();
    }

    [Fact]
    public void Skipping_a_finished_tutorial_leaves_it_done()
    {
        var kept = TutorialProgress.None.WithDone(First).WithSkipped(First);

        kept.ShouldBe(TutorialProgress.None.WithDone(First));
    }

    [Fact]
    public void Saying_the_same_thing_twice_keeps_it_once()
    {
        TutorialProgress.None.WithDone(First).WithDone(First).Done.ShouldBe([First]);
    }

    [Fact]
    public void Progress_read_from_a_store_keeps_each_id_once_in_order_and_drops_blank_ones()
    {
        var read = TutorialProgress.Of([Second, First, Second, " "], [First, "sc-repo-tutorial-grow"]);

        read.Done.ShouldBe([Second, First]);
        read.Skipped.ShouldBe(["sc-repo-tutorial-grow"]);
    }
}
