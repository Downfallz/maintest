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
    double Buff,
    double Energy,
    double Risk)
{
    /// <summary>The greedy agent's weights: a kill is worth five damage, a stun three, energy kept and buffs a little, a wasted action costs two.</summary>
    public static ScoringWeights Default { get; } = new(Damage: 1.0, Kill: 5.0, Heal: 0.8, Stun: 3.0, Bleed: 0.8, Buff: 0.5, Energy: 0.2, Risk: 2.0);

    /// <summary>Eight hex digits that change with any weight, the version a heuristic agent's spec carries.</summary>
    public string Fingerprint
    {
        get
        {
            var text = string.Join(",", new[] { Damage, Kill, Heal, Stun, Bleed, Buff, Energy, Risk }.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8];
        }
    }

    public ScoringWeights Validated()
    {
        foreach (var (name, value) in new[] { ("damage", Damage), ("kill", Kill), ("heal", Heal), ("stun", Stun), ("bleed", Bleed), ("buff", Buff), ("energy", Energy), ("risk", Risk) })
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentException($"The '{name}' weight must be a finite number.");
            }
        }

        return this;
    }
}
