using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// A sign-in's binding as the store read it, with the version of what it read. Every write of a binding changes its
/// version, a link taking it up included, so a removal over the version a link made leaves one another link has taken
/// up since (#1261).
/// </summary>
public sealed record SubjectRead(AccountId Account, string Version);
