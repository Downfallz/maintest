using Microsoft.ML.Data;

namespace DA.Game.Application.Learning.ML.Models;

public sealed class GameModelPrediction
{
    [ColumnName("PredictedLabel")]
    public string Action { get; set; } = string.Empty;
}
