using DownfallArena.Application.Learning.Recording;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a note button posts: the kind and what the player said. The three kinds a person may write are the
/// only ones accepted, because the other two are the host's own account of what happened and a client that
/// could post them could rewrite the record of a session.
/// </summary>
internal sealed record TableNoteBody
{
    public string? Kind { get; init; }

    public string? Text { get; init; }

    /// <summary>The kinds a body may name, so a refusal can say what was expected.</summary>
    public static string Kinds => string.Join(", ", Writable);

    private static readonly NoteKind[] Writable = [NoteKind.Lookup, NoteKind.Misplay, NoteKind.Comment];

    /// <summary>
    /// The kind this body names, or the reason it names none. A lookup and a misplay are one tap and carry no
    /// text, so an empty one is a note and not a refusal; a comment with nothing in it is nothing to record.
    /// </summary>
    public NoteKind? ToKind(out string problem)
    {
        problem = string.Empty;
        if (!Enum.TryParse<NoteKind>(Kind, out var kind) || !Writable.Contains(kind))
        {
            problem = $"'{Kind}' is not a note a player writes. One of: {Kinds}.";
            return null;
        }

        if (kind == NoteKind.Comment && string.IsNullOrWhiteSpace(Text))
        {
            problem = "A comment with no text is nothing to record.";
            return null;
        }

        return kind;
    }

    /// <summary>What the note carries, trimmed, and capped so one paste cannot become the session's record.</summary>
    public string Trimmed() => (Text ?? string.Empty).Trim() is { Length: > 0 } text
        ? text[..Math.Min(text.Length, 2000)]
        : string.Empty;
}
