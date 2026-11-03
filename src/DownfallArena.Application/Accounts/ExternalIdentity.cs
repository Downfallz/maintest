namespace DownfallArena.Application.Accounts;

/// <summary>
/// Who a sign-in provider says a person is: the provider's name and its subject, the id it never reassigns
/// (Google's <c>sub</c>). An email is not an identity: an address can change owner.
/// </summary>
public readonly record struct ExternalIdentity
{
    public const string Google = "google";
    public const string Development = "dev";

    public ExternalIdentity(string provider, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (!provider.All(char.IsAsciiLetterLower))
        {
            throw new ArgumentException($"'{provider}' is not a provider name: lower-case letters only.", nameof(provider));
        }

        Provider = provider;
        Subject = subject;
    }

    public string Provider { get; }

    public string Subject { get; }

    public override string ToString() => $"{Provider}:{Subject}";
}
