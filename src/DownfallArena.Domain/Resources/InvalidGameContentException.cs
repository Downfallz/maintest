namespace DownfallArena.Domain.Resources;

/// <summary>
/// The game content is inconsistent (missing references, duplicate ids, malformed values). Carries every problem
/// found so content authors can fix them in one pass.
/// </summary>
public sealed class InvalidGameContentException : Exception
{
    public InvalidGameContentException()
        : this([])
    {
    }

    public InvalidGameContentException(string message)
        : base(message)
    {
        Problems = [message];
    }

    public InvalidGameContentException(string message, Exception innerException)
        : base(message, innerException)
    {
        Problems = [message];
    }

    public InvalidGameContentException(IReadOnlyList<string> problems)
        : base(BuildMessage(problems))
    {
        Problems = problems;
    }

    public IReadOnlyList<string> Problems { get; }

    private static string BuildMessage(IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        return problems.Count == 0
            ? "The game content is invalid."
            : "The game content is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(problem => " - " + problem));
    }
}
