using Microsoft.ML.Data;
namespace DA.Game.Application.Learning.ML;

public sealed class RewardPrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
}