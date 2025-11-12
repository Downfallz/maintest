using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Shared.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.ValueObjects;
public sealed record CombatActionResult(
    CharacterId ActorId,
    SpellId SpellId,
    IReadOnlyList<EffectSummary> Effects,
    bool IsSuccessful);
