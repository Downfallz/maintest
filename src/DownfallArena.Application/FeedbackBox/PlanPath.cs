using System.Text.RegularExpressions;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The path of a plan an idea went into, as the repository names its plans: <c>docs/</c>, folders of lowercase words,
/// and a file ending <c>plan.md</c> (docs/tabletop/feedback-plan.md, I1). Nothing else is kept as one.
/// </summary>
public static partial class PlanPath
{
    public const int Longest = 200;

    public static bool Is(string? path) => path is { Length: > 0 and <= Longest } && Shape().IsMatch(path);

    [GeneratedRegex(@"^docs/(?:[a-z0-9-]+/)*(?:[a-z0-9-]+-)?plan\.md$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Shape();
}
