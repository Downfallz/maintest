using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;

namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// What a recorded step is written with: the board as features, the action as an index, and the terms each
/// candidate scored (ADR 0051). The three always come from the same schema, content and rule set, so a recorder
/// takes them as one.
/// </summary>
public sealed record StepEncoding(ObservationBuilder Observations, ActionEncoder Actions, CandidateTerms Terms)
{
    /// <summary>The encoding every recorder uses: the schema's features and actions, and the content's terms.</summary>
    public static StepEncoding For(FeatureSchema schema, IGameResources resources, RuleSet rules) =>
        new(new ObservationBuilder(schema), new ActionEncoder(schema), new CandidateTerms(resources, rules));
}
