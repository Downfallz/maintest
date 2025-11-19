using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Learning.ML.Models;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Learning.ML;

public sealed class MLPolicy : IPolicy
{
    private readonly PredictionEngine<GameModelInput, GameModelPrediction> _engine;
    private readonly IFeatureExtractor _extractor;

    public MLPolicy(string modelPath, IFeatureExtractor extractor)
    {
        var ml = new MLContext();
        var model = ml.Model.Load(modelPath, out _);
        _engine = ml.Model.CreatePredictionEngine<GameModelInput, GameModelPrediction>(model);
        _extractor = extractor;
    }

    public Task<PlayerAction> DecideAsync(GameView view, CancellationToken ct = default)
    {
        var features = _extractor.Extract(view);
        var result = _engine.Predict(features);
        return Task.FromResult(new PlayerAction(result.Action, "ml-policy"));
    }
}
