namespace DownfallArena.Application.Agents;

/// <summary>
/// The part of a value policy's prediction that the position alone explains (ADR 0015): one weight row over
/// the features and one bias, shared by every action key at a state. Its value is added to every score, so a
/// score stays a predicted return while each action row carries only what its own action adds to the
/// position. Being the same number for every candidate, it never changes which one wins.
/// </summary>
public sealed record PolicyBaseline
{
    public required IReadOnlyList<double> Weights { get; init; }

    public required double Bias { get; init; }

    /// <summary>What the position is worth before any action is weighed. The caller checks the width.</summary>
    public double Value(IReadOnlyList<float> features)
    {
        ArgumentNullException.ThrowIfNull(features);

        var value = Bias;
        for (var index = 0; index < Weights.Count; index++)
        {
            value += Weights[index] * features[index];
        }

        return value;
    }
}
