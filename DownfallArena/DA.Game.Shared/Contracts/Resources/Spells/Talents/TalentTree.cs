using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Shared.Contracts.Resources.Spells.Talents;

// Root aggregate-like resource for a talent tree
public sealed record TalentTree(
    TalentTreeId Id,
    string Name,
    TalentTreeNode Root) : ValueObject;
