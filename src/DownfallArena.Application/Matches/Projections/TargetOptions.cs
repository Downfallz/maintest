using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// The intent being revealed belongs to the player: the actor, the declared spell, and its legal targets on the
/// board as it stands. The match never asks for a slot whose creature cannot act (ADR 0083), so the targets are
/// castable whenever a player sees them; an agent handed uncastable ones still answers with no target.
/// </summary>
public sealed record TargetOptions(CreatureId Actor, SpellId Spell, LegalTargets LegalTargets);
