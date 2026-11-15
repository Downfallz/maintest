using System.Text.Json;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Infrastructure.Matches.Scenarios;

/// <summary>
/// A match scenario as a file holds it (docs/tabletop/scenarios-plan.md, stage S3): the cut itself, the seat a
/// person plays, the content it was cut on by hash, and what a person reading the file wants to know.
/// </summary>
/// <param name="Scenario">The cut: the source's creation, the entries before and after it, the boards there.</param>
/// <param name="Seat">The seat the person plays; the other is the scripted opponent.</param>
/// <param name="Content">The hash of the content the source was played on, which the scenario is replayed on.</param>
/// <param name="Session">The recorded session it was cut from, when it was cut from one.</param>
/// <param name="Engine">The engine the cut was verified on, when it was known.</param>
/// <param name="Title">What the scenario is called, for a person picking one.</param>
/// <param name="Goal">What the person is asked to try, if anything.</param>
/// <param name="Schema">The content itself, when the file carries it, so it plays where that content was never seen.</param>
public sealed record ScenarioDocument(
    ScenarioCut Scenario,
    PlayerSlot Seat,
    string Content,
    string? Session = null,
    string? Engine = null,
    string? Title = null,
    string? Goal = null,
    JsonElement? Schema = null);
