using DA.Game.Shared.Contracts.Players.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Simulation.Matches.Scenarios;
public sealed record PlayerSimProfile(string Name, ActorKind Kind);