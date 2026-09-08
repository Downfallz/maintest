using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// A position in the combat timeline at which one creature acts.
/// </summary>
public sealed record ActivationSlot(PlayerSlot Owner, CreatureId Creature, Speed Speed, Initiative Initiative);
