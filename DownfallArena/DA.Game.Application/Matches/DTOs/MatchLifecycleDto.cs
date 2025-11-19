using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.DTOs;

using DA.Game.Domain2.Match.Enums;

public sealed record MatchLifecycleDto(
    MatchState State,
    bool IsStarted,
    bool IsWaitingForPlayers,
    bool HasEnded
);
