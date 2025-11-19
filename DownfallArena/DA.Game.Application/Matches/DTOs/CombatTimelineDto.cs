using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.DTOs;

public sealed record CombatTimelineDto(
    IReadOnlyList<ActivationSlotDto> Slots
);
