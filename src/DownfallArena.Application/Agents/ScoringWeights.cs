using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DownfallArena.Application.Agents;

/// <summary>
/// What the heuristic agents value in the expected outcome of an action (<c>docs/learning/agents.md</c>).
/// The built-in values are the greedy agent; a weights file overrides them for the heuristic agent, so the
/// weights can be tuned by search without a model runtime (phase L6).
/// </summary>
public sealed record ScoringWeights(
    double Damage,
    double Kill,
    double Heal,
    double Stun,
    double Bleed,
    double Defense,
    double Energy,
    double Risk,
    double Initiative)
{
    /// <summary>
    /// The greedy agent's weights: a kill is worth five damage, a stun three, energy kept, a point of damage
    /// prevented half a point dealt, a point of initiative a little, a wasted action costs two.
    /// <para>
    /// <c>Defense</c> is not read against <c>Damage</c> point for point, whatever the shared unit suggests:
    /// an attack is paid once and a defensive effect is paid for every round it holds
    /// (<c>ActionScorer.DefensiveScore</c> multiplies what it prevents by its duration). That is why the
    /// price sits below one and why it has a cliff just above: see ADR 0028.
    /// </para>
    /// </summary>
    public static ScoringWeights Default { get; } = new(Damage: 1.0, Kill: 5.0, Heal: 0.8, Stun: 3.0, Bleed: 0.8, Defense: 0.5, Energy: 0.2, Risk: 2.0, Initiative: 0.5);

    /// <summary>
    /// The weights under the names a weights file uses, in the order the fingerprint hashes them. One list, so
    /// a name a file may carry, a name an error may print and a name a page may show cannot drift apart.
    /// </summary>
    public IReadOnlyList<(string Name, double Value)> Named =>
    [
        ("damage", Damage), ("kill", Kill), ("heal", Heal), ("stun", Stun),
        ("bleed", Bleed), ("defense", Defense), ("energy", Energy), ("risk", Risk), ("initiative", Initiative),
    ];

    /// <summary>Eight hex digits that change with any weight, the version a heuristic agent's spec carries.</summary>
    public string Fingerprint
    {
        get
        {
            var text = string.Join(",", Named.Select(weight => weight.Value.ToString("R", CultureInfo.InvariantCulture)));
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8];
        }
    }

    public ScoringWeights Validated()
    {
        foreach (var (name, value) in Named)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentException($"The '{name}' weight must be a finite number.");
            }
        }

        return this;
    }
}
