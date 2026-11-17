namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The path of a plan an idea went into, as the repository names its plans: <c>docs/</c>, folders of lowercase words,
/// and a file ending <c>plan.md</c> (docs/tabletop/feedback-plan.md, I1). Nothing else is kept as one.
/// </summary>
/// <remarks>
/// Checked part by part, without a regex: a match timeout trips on a starved CPU, not on the pattern, and a live write
/// would then throw (#733). The check is linear in the path, which is at most <see cref="Longest" /> characters.
/// </remarks>
public static class PlanPath
{
    public const int Longest = 200;

    private const string Root = "docs";
    private const string File = "plan.md";
    private const string Suffix = "-" + File;

    public static bool Is(string? path)
    {
        if (path is not { Length: > 0 and <= Longest })
        {
            return false;
        }

        var parts = path.Split('/');
        return parts.Length >= 2 && parts[0] == Root && parts[1..^1].All(IsWord) && IsPlanFile(parts[^1]);
    }

    private static bool IsPlanFile(string name) =>
        name == File || (name.EndsWith(Suffix, StringComparison.Ordinal) && IsWord(name[..^Suffix.Length]));

    private static bool IsWord(string part) => part.Length > 0 && part.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');
}
