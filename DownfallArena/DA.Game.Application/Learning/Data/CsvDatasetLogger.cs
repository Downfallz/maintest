using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Learning.ML.Models;
using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;

namespace DA.Game.Application.Learning.Data;


public sealed class CsvDatasetLogger : IDatasetLogger
{
    private readonly IFeatureExtractor _fx;
    private readonly List<GameModelInput> _rows = new();

    public CsvDatasetLogger(IFeatureExtractor fx) => _fx = fx;

    public void Record(GameView view, PlayerAction action, double reward)
    {
        var row = _fx.Extract(view, action);
        row.Reward = (float)reward;
        _rows.Add(row);
    }


    public void SaveCsv(string path, bool includeHeader = true)
    {
        using var w = new StreamWriter(path);
        if (includeHeader) w.WriteLine("Turn,PlayerHealth,EnemyHealth,PlayerEnergy,Action,Reward");
        foreach (var r in _rows)
            w.WriteLine($"{r.Turn},{r.PlayerHealth},{r.EnemyHealth},{r.PlayerEnergy},{r.Action},{r.Reward}");
    }
}
