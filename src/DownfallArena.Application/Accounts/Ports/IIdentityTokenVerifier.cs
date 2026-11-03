namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Reads who a sign-in provider says sent a token (ADR 0122): Google's ID token, checked against the provider's
/// own keys, issuer, audience and expiry. Null for a token that is not one, so a forged, expired or foreign token
/// is a refusal and never an exception. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IIdentityTokenVerifier
{
    Task<ExternalIdentity?> VerifyAsync(string token, CancellationToken cancellationToken = default);
}
