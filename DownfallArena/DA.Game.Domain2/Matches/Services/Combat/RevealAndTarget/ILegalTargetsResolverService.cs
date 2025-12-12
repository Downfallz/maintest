using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.RevealAndTarget;

public interface ILegalTargetsResolverService
{
    Result<LegalTargetsResult> Resolve(
       CreaturePerspective ctx,
       Spell spellRef);
}
