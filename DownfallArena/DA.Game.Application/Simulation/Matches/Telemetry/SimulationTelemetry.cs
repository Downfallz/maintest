using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;
using System.Text.Json;

namespace DA.Game.Application.Simulation.Matches.Telemetry;

public sealed class SimulationTelemetry
{
    private readonly List<GameTick> _ticks = new();

    public void Record(GameView view, PlayerAction action)
        => _ticks.Add(new GameTick(view, action));

    public void SaveAsJson(string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(_ticks));
}

public record GameTick(GameView View, PlayerAction Action);
