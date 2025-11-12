using Microsoft.ML.Data;

namespace DA.Game.Application.Learning.ML.Models;

public sealed class GameModelInput
{
    [LoadColumn(0)] public float Turn { get; set; }
    [LoadColumn(1)] public float PlayerHealth { get; set; }
    [LoadColumn(2)] public float EnemyHealth { get; set; }
    [LoadColumn(3)] public float PlayerEnergy { get; set; }
    [LoadColumn(4)] public string Action { get; set; } = string.Empty;
    [LoadColumn(5)] public float Reward { get; set; }
}
