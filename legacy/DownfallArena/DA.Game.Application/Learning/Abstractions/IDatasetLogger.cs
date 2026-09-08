using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;

namespace DA.Game.Application.Learning.Abstractions;

public interface IDatasetLogger
{
    void Record(GameView view, PlayerAction action, double reward);
    void SaveCsv(string path, bool includeHeader = true);
}