using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Learning.ML.Models;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Learning.ML;
// ML/GameFeatureExtractor.cs
public sealed class GameFeatureExtractor : IFeatureExtractor
{
    public GameModelInput Extract(GameView view, PlayerAction? action = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var player = view.CurrentTurn == PlayerSlot.Player1 ? view.Player1Id : view.Player2Id;
        var enemy = view.CurrentTurn == PlayerSlot.Player1 ? view.Player2Id : view.Player1Id;

        return new GameModelInput
        {
            Turn = view.TurnNumber,
            PlayerHealth = 0,
            EnemyHealth = 0,
            PlayerEnergy = 0,
            Action = "chouchou",
            Reward = 0

        };
    }
}
