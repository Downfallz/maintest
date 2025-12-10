using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;

namespace DA.Game.Domain2.Matches.Services.Queries;

public sealed record CreatureUnlockableSpells(
    CreatureId CreatureId,
    IReadOnlyList<SpellId> UnlockableSpellIds);
