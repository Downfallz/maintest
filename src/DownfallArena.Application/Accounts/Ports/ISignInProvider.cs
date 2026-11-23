namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// One way to sign in (ADR 0122, ADR 0204): a provider that names itself, as its identities and its route do
/// (<c>google</c>, <c>/api/account/google</c>), and turns what the page sent into who the provider says sent it.
/// Null for anything that is not a proof the provider gave, so a forged, expired or foreign proof is a refusal and
/// never an exception. Owned by Application, implemented by Infrastructure, one per provider.
/// </summary>
public interface ISignInProvider
{
    /// <summary>The provider's name: lower-case letters, the <see cref="ExternalIdentity.Provider" /> of what it signs in.</summary>
    string Name { get; }

    /// <summary>The public id the page's button is drawn for (Google's client id); null for a provider that needs none.</summary>
    string? ClientId { get; }

    Task<ExternalIdentity?> SignInAsync(SignInProof proof, CancellationToken cancellationToken = default);
}
