using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli;

/// <summary>
/// A combat declaration as this host writes it (ADR 0110): on a command line, in a rule set file, in a table
/// request and in a table's record. One spelling everywhere, so a record can be read back by the flag's parser.
/// </summary>
internal static class CombatDeclarationText
{
    public const string HiddenIntent = "hidden-intent";

    public const string AtActivation = "at-activation";

    public static string Of(CombatDeclaration declaration) => declaration switch
    {
        CombatDeclaration.HiddenIntent => HiddenIntent,
        CombatDeclaration.AtActivation => AtActivation,
        _ => throw new ArgumentOutOfRangeException(nameof(declaration), declaration, "Not a combat declaration."),
    };

    public static bool TryParse(string? text, out CombatDeclaration declaration)
    {
        switch (text?.Trim())
        {
            case HiddenIntent:
                declaration = CombatDeclaration.HiddenIntent;
                return true;
            case AtActivation:
                declaration = CombatDeclaration.AtActivation;
                return true;
            default:
                declaration = default;
                return false;
        }
    }

    /// <summary>The refusal every reader gives for a spelling it does not know.</summary>
    public static string Unknown(string? text) => $"'{text}' is not a combat declaration: {HiddenIntent} or {AtActivation}.";
}
