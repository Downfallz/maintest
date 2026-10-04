using System.Globalization;

namespace DownfallArena.Domain.Matches;

/// <summary>
/// How many Creatures a side brings, named the way players name it: <c>3v3</c>, <c>2v2</c>, <c>1v1</c>.
/// </summary>
/// <remarks>
/// <para>
/// It is a label over one <see cref="RuleSet" /> value, <see cref="RuleSet.TeamSize" />, and it exists so that
/// nothing else has to spell that label itself. A command line, an admin panel, a workflow input, a run stamp
/// and the viewer all name the same games, and a page that built <c>"3v3"</c> out of a team size on its own
/// would be a second naming to keep in agreement (the reasoning of ADR 0056, applied to a word instead of a
/// schedule).
/// </para>
/// <para>
/// It carries no rule of its own. A format does not change what a Round does, only how many Creatures walk
/// through it, so playing 1v1 costs no fidelity: it is the same engine on another value, and the Evolution
/// picks follow from the rules already written down -- a Creature buys at most one package an opportunity
/// (ADR 0066), so a side of one has one pick whatever the Rule set allows.
/// </para>
/// </remarks>
public readonly record struct MatchFormat
{
    /// <summary>
    /// The largest side the engine encodes. The learning observation lays a Creature block out per slot and
    /// numbers both sides from it (<c>BoardSlots</c>, <c>FeatureSchema</c>), so this is the one place the
    /// ceiling is written and the encoding reads it from here.
    /// </summary>
    public const int MaxTeamSize = 16;

    private MatchFormat(int teamSize) => TeamSize = teamSize;

    /// <summary>The Creatures each side brings.</summary>
    public int TeamSize { get; }

    /// <summary>Three against three: the prototype's game, and the engine's default.</summary>
    public static MatchFormat ThreeOnThree { get; } = new(3);

    /// <summary>
    /// The formats a chooser offers, smallest first. Parsing is wider than this on purpose: the engine plays
    /// any side up to <see cref="MaxTeamSize" />, and this is the shortlist a person is shown rather than a
    /// rule about what exists.
    /// </summary>
    public static IReadOnlyList<MatchFormat> Offered { get; } = [new(1), new(2), new(3)];

    public static MatchFormat OfTeamSize(int teamSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(teamSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(teamSize, MaxTeamSize);
        return new MatchFormat(teamSize);
    }

    /// <summary>
    /// A format from its name: <c>3v3</c>, or the bare team size <c>3</c>, which is what a shell argument
    /// tends to be typed as. Case and surrounding space are ignored; anything else is refused by name.
    /// </summary>
    public static MatchFormat Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return TryParse(text, out var format)
            ? format
            : throw new ArgumentException($"'{text.Trim()}' is not a match format. It is a side against itself ('3v3', '2v2', '1v1') or the team size alone ('3'), from 1 to {MaxTeamSize}.", nameof(text));
    }

    public static bool TryParse(string? text, out MatchFormat format)
    {
        format = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var separator = trimmed.IndexOf('v', StringComparison.OrdinalIgnoreCase);
        var left = separator < 0 ? trimmed : trimmed[..separator];
        if (!int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var teamSize) || teamSize is < 1 or > MaxTeamSize)
        {
            return false;
        }

        // A side against itself, and nothing else: an asymmetric '3v2' is a different game, not a label, and
        // would need a second team size on the rule set to mean anything.
        if (separator >= 0
            && (!int.TryParse(trimmed[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var other) || other != teamSize))
        {
            return false;
        }

        format = new MatchFormat(teamSize);
        return true;
    }

    /// <summary>The format a rule set plays.</summary>
    public static MatchFormat Of(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return OfTeamSize(rules.TeamSize);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{TeamSize}v{TeamSize}");
}
