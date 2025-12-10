using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Shared.Contracts.Resources.Json;

// Prerequisites for unlocking a node or a spell
// allOf: all listed spells must be known
// anyOf: at least one of the listed spells must be known
public sealed record TalentPrerequisitesDto(
    IReadOnlyList<string> AllOf,
    IReadOnlyList<string> AnyOf
);
