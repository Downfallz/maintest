using DA.Game.Application.Learning.ML.Models;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;

namespace DA.Game.Application.Learning.Abstractions;

// Abstractions/IFeatureExtractor.cs
public interface IFeatureExtractor
{
    GameModelInput Extract(GameView view, PlayerAction? action = null);
}
