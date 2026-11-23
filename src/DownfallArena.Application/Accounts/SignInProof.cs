namespace DownfallArena.Application.Accounts;

/// <summary>
/// What a page posts to sign in with a provider, as the string fields of the body it sent: Google's
/// <c>credential</c>, or a later provider's <c>code</c> and <c>verifier</c>. Each provider reads the fields it knows;
/// a field that is missing, or was not a string, reads as null.
/// </summary>
public sealed class SignInProof
{
    private readonly IReadOnlyDictionary<string, string> _fields;

    public SignInProof(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _fields = fields;
    }

    public static SignInProof Of(params (string Name, string Value)[] fields) =>
        new(fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal));

    public string? this[string name] => _fields.GetValueOrDefault(name);
}
