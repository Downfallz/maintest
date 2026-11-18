namespace DownfallArena.Cli.Table;

/// <summary>
/// A practice or a scenario that cannot be played for a reason the host wrote for the person: the content lacks a
/// spell the recipe needs, the recipe stops short of its question. Such a refusal is said on the page; any other
/// failure is said as a fixed sentence, as its message can carry a path or a store's words (#786). It stays an
/// <see cref="InvalidDataException" />, so what catches one still does.
/// </summary>
internal static class PracticeRefusal
{
    private const string Key = "DownfallArena.PracticeRefusal";

    /// <summary>A refusal whose message is written for the person, and is said to them.</summary>
    public static InvalidDataException Of(string message)
    {
        var refusal = new InvalidDataException(message);
        refusal.Data[Key] = true;
        return refusal;
    }

    /// <summary>The message to say, when the failure is a refusal written for the person; null for any other.</summary>
    public static string? Said(Exception exception) =>
        exception.Data[Key] is true ? exception.Message : null;
}
