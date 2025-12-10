using DA.Game.Shared.Contracts.Matches.Enums;

namespace DA.Game.Domain2.Matches.Services.Queries;

public sealed record PlayerUnlockableSpells(
    PlayerSlot Slot,
    IReadOnlyList<CreatureUnlockableSpells> Creatures);
