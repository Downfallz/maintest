using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Learning.ML.Models;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;
using Microsoft.ML;
namespace DA.Game.Application.Learning.ML;


public sealed class RewardPredictor
{
    private readonly PredictionEngine<GameModelInput, RewardPrediction> _engine;
    private readonly IFeatureExtractor _fx;

    public RewardPredictor(string modelPath, IFeatureExtractor fx)
    {
        var ml = new MLContext();
        var model = ml.Model.Load(modelPath, out _);
        _engine = ml.Model.CreatePredictionEngine<GameModelInput, RewardPrediction>(model);
        _fx = fx;
    }

    public float PredictReward(GameView view, string action)
    {
        var input = _fx.Extract(view, new PlayerAction(action, "test"));
        input.Action = action;
        var res = _engine.Predict(input);
        return _engine.Predict(input).Score;
    }
}
