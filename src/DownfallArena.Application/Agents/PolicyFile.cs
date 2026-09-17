using System.Globalization;
using DownfallArena.Application.Learning;

namespace DownfallArena.Application.Agents;

/// <summary>
/// A trained policy as the engine reads it (<c>policy.json</c>, docs/learning/training.md): one weight row and
/// one bias per action key, read under one feature schema. The score of a candidate action is its row's dot
/// product with the observation plus its bias, or <see cref="Fallback"/> when the policy never saw that key.
/// A <c>value</c> policy also carries a <see cref="PolicyBaseline"/>, added to every score (ADR 0016); a file
/// without one scores exactly as before. A policy may also carry <see cref="CandidateWeights"/>, one per
/// scorer term, whose dot product with a candidate's terms is added to its score (ADR 0051): the part of a
/// decision the heuristic reads and no row over the board can express; a file without them scores as before.
/// No ML runtime: a <c>clone</c> policy holds classifier logits, a <c>value</c> policy predicted returns, and
/// both are read the same way.
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

    /// <summary>The name of each candidate term the policy weighs, in order; absent with <see cref="CandidateWeights"/>.</summary>
    public IReadOnlyList<string>? CandidateTermNames { get; init; }

    /// <summary>One weight per candidate term, shared by every action key; absent on a policy trained without them.</summary>
    public IReadOnlyList<double>? CandidateWeights { get; init; }

    /// <summary>Whether a candidate's terms move its score, so an agent only reads them when they do.</summary>
    public bool ReadsCandidateTerms => CandidateWeights is not null;

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
        ValidateCandidateWeights();

        return this;
    }

    private void ValidateCandidateWeights()
    {
        if (CandidateWeights is null && CandidateTermNames is null)
        {
            return;
        }

        if (CandidateWeights is null || CandidateTermNames is null)
        {
            throw new InvalidDataException("The policy's candidate weights and candidate term names come together or not at all.");
        }

        // The names are the engine's own, in its order: a file naming other terms, or the same in another
        // order, would weigh numbers it was not trained on.
        if (!CandidateTermNames.SequenceEqual(ScoreTerms.Names, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"The policy weighs candidate terms '{string.Join(", ", CandidateTermNames)}'; this engine reads '{string.Join(", ", ScoreTerms.Names)}'.");
        }

        if (CandidateWeights.Count != CandidateTermNames.Count || CandidateWeights.Any(value => !double.IsFinite(value)))
        {
            throw new InvalidDataException("The policy needs one finite candidate weight per candidate term.");
        }
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
    /// The score of an action key on an observation without its terms: the baseline, when there is one, plus
    /// its row against the features, or plus the fallback when the policy never saw that key. What a policy
    /// that weighs candidate terms scores here is the part of the score the board alone gives.
    /// </summary>
    public double Score(string key, IReadOnlyList<float> features) => Score(key, features, null);

    /// <summary>
    /// The same, plus the candidate's terms at the candidate weights when the policy carries them (ADR 0051).
    /// The terms are read for a key the policy never saw as well: what an action does is known whether or not
    /// its key was, which is the point of weighing it. Terms handed to a policy that weighs none are ignored.
    /// </summary>
    public double Score(string key, IReadOnlyList<float> features, IReadOnlyList<float>? terms)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(features);

        var known = Index().TryGetValue(key, out var row);
        if (!known && Baseline is null)
        {
            return Fallback + TermsValue(terms);
        }

        if (features.Count != FeatureNames.Count)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"The observation has {features.Count} features, the policy {FeatureNames.Count}."), nameof(features));
        }

        var score = (Baseline?.Value(features) ?? 0.0) + TermsValue(terms);
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

    private double TermsValue(IReadOnlyList<float>? terms)
    {
        if (CandidateWeights is null || terms is null)
        {
            return 0.0;
        }

        if (terms.Count != CandidateWeights.Count)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"The policy weighs {CandidateWeights.Count} candidate terms and was handed {terms.Count}."), nameof(terms));
        }

        var value = 0.0;
        for (var index = 0; index < terms.Count; index++)
        {
            value += CandidateWeights[index] * terms[index];
        }

        return value;
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
