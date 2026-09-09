using System.Globalization;
using DownfallArena.Application.Learning;

namespace DownfallArena.Application.Agents;

/// <summary>
/// A trained policy as the engine reads it (<c>policy.json</c>, docs/learning/training.md): one weight row and
/// one bias per action key, read under one feature schema. The score of a candidate action is its row's dot
/// product with the observation plus its bias, or <see cref="Fallback"/> when the policy never saw that key.
/// A <c>value</c> policy also carries a <see cref="PolicyBaseline"/>, added to every score (ADR 0016); a file
/// without one scores exactly as before. No ML runtime: a <c>clone</c> policy holds classifier logits, a
/// <c>value</c> policy predicted returns, and both are read the same way.
/// </summary>
public sealed record PolicyFile
{
    public static IReadOnlyList<string> Kinds { get; } = ["clone", "value"];

    private Dictionary<string, int>? _index;
    private IReadOnlyList<string>? _indexedKeys;

    public required string Kind { get; init; }

    /// <summary>The feature schema id the policy was trained under; an agent only plays under that exact id.</summary>
    public required string SchemaId { get; init; }

    public required string SchemaVersion { get; init; }

    public required IReadOnlyList<string> FeatureNames { get; init; }

    public required IReadOnlyList<string> ActionKeys { get; init; }

    public required IReadOnlyList<IReadOnlyList<double>> Weights { get; init; }

    public required IReadOnlyList<double> Bias { get; init; }

    public required double Fallback { get; init; }

    /// <summary>What the position alone is worth, added to every score; absent on a policy trained without one.</summary>
    public PolicyBaseline? Baseline { get; init; }

    /// <summary>Eight hex digits of the file's content, the version a policy agent's spec carries.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>
    /// Refuses a file of another shape than the contract, and a schema version this engine does not know: an
    /// observation of another layout would feed the rows meaningless numbers.
    /// </summary>
    public PolicyFile Validated()
    {
        if (!Kinds.Contains(Kind, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Unknown policy kind '{Kind}'; known kinds: {string.Join(", ", Kinds)}.");
        }

        if (SchemaVersion != FeatureSchema.CurrentVersion)
        {
            throw new InvalidDataException($"The policy was trained under feature schema version '{SchemaVersion}'; this engine reads '{FeatureSchema.CurrentVersion}'.");
        }

        if (!SchemaId.StartsWith(SchemaVersion + "+", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The policy's schema id '{SchemaId}' does not belong to version '{SchemaVersion}'.");
        }

        if (ActionKeys.Distinct(StringComparer.Ordinal).Count() != ActionKeys.Count)
        {
            throw new InvalidDataException("The policy's action keys repeat.");
        }

        if (Weights.Count != ActionKeys.Count || Bias.Count != ActionKeys.Count)
        {
            throw new InvalidDataException("The policy needs one weight row and one bias per action key.");
        }

        if (Weights.Any(row => row.Count != FeatureNames.Count))
        {
            throw new InvalidDataException("Every weight row of the policy must hold one value per feature.");
        }

        if (!double.IsFinite(Fallback) || Bias.Any(value => !double.IsFinite(value)) || Weights.Any(row => row.Any(value => !double.IsFinite(value))))
        {
            throw new InvalidDataException("The policy's weights, bias, and fallback must be finite numbers.");
        }

        ValidateBaseline();

        return this;
    }

    private void ValidateBaseline()
    {
        if (Baseline is null)
        {
            return;
        }

        if (Baseline.Weights.Count != FeatureNames.Count)
        {
            throw new InvalidDataException("The policy's baseline must hold one weight per feature.");
        }

        if (!double.IsFinite(Baseline.Bias) || Baseline.Weights.Any(value => !double.IsFinite(value)))
        {
            throw new InvalidDataException("The policy's baseline weights and bias must be finite numbers.");
        }
    }

    /// <summary>
    /// The score of an action key on an observation: the baseline, when there is one, plus its row against
    /// the features, or plus the fallback when the policy never saw that key.
    /// </summary>
    public double Score(string key, IReadOnlyList<float> features)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(features);

        var known = Index().TryGetValue(key, out var row);
        if (!known && Baseline is null)
        {
            return Fallback;
        }

        if (features.Count != FeatureNames.Count)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"The observation has {features.Count} features, the policy {FeatureNames.Count}."), nameof(features));
        }

        var score = Baseline?.Value(features) ?? 0.0;
        if (!known)
        {
            return score + Fallback;
        }

        var weights = Weights[row];
        score += Bias[row];
        for (var index = 0; index < weights.Count; index++)
        {
            score += weights[index] * features[index];
        }

        return score;
    }

    private Dictionary<string, int> Index()
    {
        // A copy made with 'with' shares the cached index, so the index is tied to the keys it was built from.
        if (_index is null || !ReferenceEquals(_indexedKeys, ActionKeys))
        {
            _index = ActionKeys.Select((actionKey, index) => (actionKey, index)).ToDictionary(pair => pair.actionKey, pair => pair.index, StringComparer.Ordinal);
            _indexedKeys = ActionKeys;
        }

        return _index;
    }
}
