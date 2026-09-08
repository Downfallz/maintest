using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Learning.ML.Models;
using Microsoft.ML;

namespace DA.Game.Application.Learning.ML;

public sealed class RewardTrainer : ITrainer
{
    public void Train(string datasetPath, string modelPath)
    {
        var ml = new MLContext(seed: 123);

        var data = ml.Data.LoadFromTextFile<GameModelInput>(
            path: datasetPath, hasHeader: true, separatorChar: ',');

        var pipeline =
            ml.Transforms.Categorical.OneHotEncoding("ActionEncoded", nameof(GameModelInput.Action))
            .Append(ml.Transforms.Concatenate("Features",
                nameof(GameModelInput.Turn),
                nameof(GameModelInput.PlayerHealth),
                nameof(GameModelInput.EnemyHealth),
                nameof(GameModelInput.PlayerEnergy),
                "ActionEncoded"))
            .Append(ml.Regression.Trainers.Sdca(
                labelColumnName: nameof(GameModelInput.Reward),
                featureColumnName: "Features"));

        var model = pipeline.Fit(data);
        ml.Model.Save(model, data.Schema, modelPath);

    }
}

