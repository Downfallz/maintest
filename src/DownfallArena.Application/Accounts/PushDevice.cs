namespace DownfallArena.Application.Accounts;

/// <summary>
/// One browser an account asked to be told on (friends plan, stage 1): the push service address its
/// <c>PushManager.subscribe</c> answered with, the two keys that encrypt a notice to that browser alone, and when
/// it was registered, which is what decides which device goes when the account has too many. The account's, not
/// a seat's: a seat's subscription belongs to the browser holding its token and goes with it (ADR 0118); a device
/// stays until the account says otherwise, signs out everywhere, or is deleted.
/// </summary>
public sealed record PushDevice(string Endpoint, string P256dh, string Auth, DateTimeOffset AddedAt);
