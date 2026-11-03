using System.Security.Cryptography;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The public keys a provider signs its ID tokens with, by key id. <paramref name="refresh" /> asks for the keys
/// again rather than the ones held: a token signed by a key not held yet is a provider that rotated its keys.
/// </summary>
public interface ISigningKeySource
{
    Task<RSA?> KeyAsync(string keyId, bool refresh, CancellationToken cancellationToken = default);
}
