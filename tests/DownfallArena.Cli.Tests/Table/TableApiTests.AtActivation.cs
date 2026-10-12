using System.Text.Json;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The same API on a rule set that declares no intent (ADR 0110): a person is asked one question at their
/// creature's slot, served every spell it can cast with that spell's legal targets, and answers with the spell
/// and its targets together. Nothing is declared before combat, so nothing is asked then.
/// </summary>
public sealed partial class TableApiTests
{
    // The other file's rules with the declaration changed, spelled out: static fields of a partial class are not
    // initialised in an order one file can rely on.
    private static readonly RuleSet AtActivation = RuleSet.Create(2, 2, 1, 6, 2.0, combatDeclaration: CombatDeclaration.AtActivation);

    [Fact]
    public async Task At_its_slot_a_seat_is_served_every_spell_it_can_cast_with_the_targets_of_each()
    {
        var table = await Seated(AtActivation);
        await Post(table, """{"kind":"Evolution","pass":true}""");
        await AnswerEach(table, PlayerOptionsKind.Speed, creature => $$"""{"kind":"Speed","creature":{{creature}},"speed":"Quick"}""", until: PlayerOptionsKind.Action);

        using var payload = JsonDocument.Parse(Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token)));

        var root = payload.RootElement;
        root.GetProperty("waitingFor").GetString().ShouldBe("Action");
        var action = root.GetProperty("options").GetProperty("action");
        action.GetProperty("actor").GetInt32().ShouldBe(table.Person.Waiting!.Creature!.Value.Value);
        var spells = action.GetProperty("spells").EnumerateArray().ToList();
        spells.ShouldNotBeEmpty();
        spells.ShouldAllBe(spell => spell.GetProperty("legalTargets").GetProperty("candidates").GetArrayLength() > 0);
        root.GetProperty("opponentIntents").GetInt32().ShouldBe(0, "nothing is face down when nothing is declared");
    }

    [Fact]
    public async Task An_action_at_the_slot_resolves_on_the_tap_and_an_intent_is_refused_as_not_asked()
    {
        var table = await Seated(AtActivation);
        await Post(table, """{"kind":"Evolution","pass":true}""");
        await AnswerEach(table, PlayerOptionsKind.Speed, creature => $$"""{"kind":"Speed","creature":{{creature}},"speed":"Quick"}""", until: PlayerOptionsKind.Action);
        var actor = table.Person.Waiting!.Creature!.Value.Value;

        var intent = await table.Api.HandleAsync("POST", "/api/seat/player1/decision", Answering($$"""{"kind":"Intent","creature":{{actor}},"spell":"spell:strike:v1"}""", table.Person), table.Token);
        var action = await table.Api.HandleAsync("POST", "/api/seat/player1/decision", Answering($$"""{"kind":"Action","creature":{{actor}},"spell":"spell:strike:v1","targets":[3]}""", table.Person), table.Token);

        intent.Status.ShouldBe(409);
        action.Status.ShouldBe(204, Text(action));
        (await ResolvedAsync(table)).ShouldContain(resolved => resolved.Actor == actor && resolved.Spell == "spell:strike:v1");
    }

    [Fact]
    public async Task A_person_plays_a_match_that_declares_no_intent_to_its_end_through_the_api()
    {
        var table = await Seated(AtActivation);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        while (!table.Session.IsOver)
        {
            timeout.Token.ThrowIfCancellationRequested();
            switch (table.Person.Waiting)
            {
                case { Kind: PlayerOptionsKind.Evolution }:
                    await Post(table, """{"kind":"Evolution","pass":true}""");
                    break;
                case { Kind: PlayerOptionsKind.Speed, Creature: { } creature }:
                    await Post(table, $$"""{"kind":"Speed","creature":{{creature.Value}},"speed":"Standard"}""");
                    break;
                case { Kind: PlayerOptionsKind.TieOrder }:
                    await KeepTieOrder(table);
                    break;
                case { Kind: PlayerOptionsKind.Action }:
                    await ActFirst(table);
                    break;
                case { Kind: var other }:
                    throw new InvalidOperationException($"A match that declares no intent asked for {other}.");
                default:
                    await Task.Delay(5, timeout.Token);
                    break;
            }
        }

        (await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// The actions the seat's feed shows resolved, once it shows the latest: the driver hands the answer to the
    /// match after the tap is accepted, on its own thread, so the feed catches up a moment later.
    /// </summary>
    private static async Task<IReadOnlyList<(int Actor, string? Spell)>> ResolvedAsync((TableApi Api, TableSession Session, HumanSeat Person, string Token) table)
    {
        var resolved = new List<(int Actor, string? Spell)>();
        for (var attempt = 0; attempt < 150 && table.Person.Waiting is null; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        using var payload = JsonDocument.Parse(Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token)));
        foreach (var entry in payload.RootElement.GetProperty("feed").EnumerateArray().Select(entry => entry.GetProperty("event")))
        {
            if (entry.GetProperty("kind").GetString() == "CombatActionResolved")
            {
                var action = entry.GetProperty("resolution").GetProperty("action");
                resolved.Add((action.GetProperty("actor").GetInt32(), action.GetProperty("spell").GetString()));
            }
        }

        return resolved;
    }

    /// <summary>The first spell the seat is served and the first legal target of it, as a page that takes the first of everything would post.</summary>
    private static async Task ActFirst((TableApi Api, TableSession Session, HumanSeat Person, string Token) table)
    {
        using var payload = JsonDocument.Parse(Text(await table.Api.HandleAsync("GET", "/api/seat/player1", string.Empty, table.Token)));
        var action = payload.RootElement.GetProperty("options").GetProperty("action");
        var spell = action.GetProperty("spells")[0];
        var legal = spell.GetProperty("legalTargets");
        var targets = legal.GetProperty("candidates").EnumerateArray().Take(Math.Max(1, legal.GetProperty("minTargets").GetInt32())).Select(target => target.GetInt32());
        await Post(table, $$"""{"kind":"Action","creature":{{action.GetProperty("actor").GetInt32()}},"spell":"{{spell.GetProperty("spell").GetString()}}","targets":[{{string.Join(",", targets)}}]}""");
    }
}
