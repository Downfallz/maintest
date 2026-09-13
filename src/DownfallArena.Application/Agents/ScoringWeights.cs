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
    double Fizzle,
    double Initiative)
{
    /// <summary>
    /// The greedy agent's weights: a kill is worth five damage, a stun three, energy kept, two thirds of a
    /// point per point of damage prevented, two and a bit per point of initiative, a wasted action costs two.
    /// <para>
    /// <c>Defense</c> is not read against <c>Damage</c> point for point, whatever the shared unit suggests:
    /// an attack is paid once and a defensive effect is paid for every round it holds
    /// (<c>ActionScorer.DefensiveScore</c> multiplies what it prevents by its duration). It compounds where
    /// <c>Damage</c> does not, which is why it sits below one and why it moves the play in steps rather than
    /// smoothly: ADR 0028 has the sweep, and 0.65 sits in the middle of a step rather than on its edge.
    /// </para>
    /// <para>
    /// <c>Initiative</c> is above one for the opposite reason: a point of it is bought once and kept for the
    /// match, in a game the first mover was winning 64 % of. ADR 0018 guessed 0.5 and said so; ADR 0032 has
    /// the sweep that replaced the guess, and 2.1 sits in the middle of its step the same way.
    /// </para>
    /// <para>
    /// <c>Energy</c> was hand-set at 0.2 in phase L5 and priced one thing: the energy an actor keeps. Three
    /// ADRs since then gave it three more jobs — energy handed out and regenerated (ADR 0020), the part of an
    /// unlock's cost the actor cannot cover (ADR 0026), and energy drained (ADR 0035) — so ADR 0037 swept it
    /// and moved it to 0.3, the middle of the step 0.2..0.4, whose neighbour 0.5 breaks hard. What it buys is
    /// the mirror's first-mover share, not a stronger agent: at 0.3 the same two bots decide less of the
    /// match by going first, and a 0.3 agent against a 0.2 one is a dead heat.
    /// </para>
    /// <para>
    /// <c>Fizzle</c> was called <c>Risk</c> until ADR 0038, which renamed it because the old name promised a
    /// reading of probability the term has never had: it counts actions that came to nothing, and the three
    /// ways that happens — a spell with no legal target, a resolution that fizzled, targets gone before it
    /// resolved — are all one thing. Its value is **not measured and cannot be**: ADR 0037's sweep found every
    /// value from 0 to 100 plays the 400 benchmark seeds identically. Two of its three readers sit inside
    /// <see cref="ActionScorer.Score"/>, which a decision reaches only through <c>Expected</c>, and
    /// <c>Expected</c> resolves against the board as it stands — where nothing has fizzled and no target has
    /// dropped — so both are the same zero for every candidate and cancel. The third,
    /// <c>HeuristicAgent.DecideIntent</c>, is on the decision path but has never been observed to change an
    /// outcome. Teaching the agent to see the waste at declaration time is ADR 0039's job, and the value is
    /// worth measuring only after that.
    /// </para>
    /// </summary>
    public static ScoringWeights Default { get; } = new(Damage: 1.0, Kill: 5.0, Heal: 0.8, Stun: 3.0, Bleed: 0.8, Defense: 0.65, Energy: 0.3, Fizzle: 2.0, Initiative: 2.1);

    /// <summary>
    /// The weights under the names a weights file uses, in the order the fingerprint hashes them. One list, so
    /// a name a file may carry, a name an error may print and a name a page may show cannot drift apart.
    /// </summary>
    public IReadOnlyList<(string Name, double Value)> Named =>
    [
        ("damage", Damage), ("kill", Kill), ("heal", Heal), ("stun", Stun),
        ("bleed", Bleed), ("defense", Defense), ("energy", Energy), ("fizzle", Fizzle), ("initiative", Initiative),
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
