using DA.Game.Application.Matches.Ports;
using DA.Game.Domain2.Matches.Resources;
using System.Text.Json;

namespace DA.Game.Infrastructure.Bootstrap;

public class GameResourcesProvider() : IGameResourcesProvider
{
    public GameResources Get()
    {
        var json = File.ReadAllText("dist/game.schema.json");
        var schema = JsonSerializer.Deserialize<GameSchema>(json)!;

        // Accès rapide
        var spellsById = schema.Spells.ToDictionary(s => s.Id);
        var activeBoltId = schema.Aliases?["spell:basic_attack"] ?? "spell:lightning_bolt:v1";
        var lightningBolt = spellsById[activeBoltId];




        var activeId = schema.Aliases["spell:frost_nova"];
        var nova = schema.Spells.First(s => s.Id == activeId);
    }
}
