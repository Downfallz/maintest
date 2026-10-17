using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Agents;
using DownfallArena.Infrastructure.Tests.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Agents;

public sealed class JsonLinesSlotChoiceLogTests
{
    /// <summary>One slot a line, in the artifacts' dialect, so `scripts/slot-choices.py` reads it as written.</summary>
    [Fact]
    public void Each_slot_is_one_line_of_json_with_its_spells()
    {
        using var directory = new ContentDirectory();
        var path = Path.Combine(directory.Path, "slots", "slots.jsonl");
        var strike = SpellId.Parse("spell:heavy_strike:v1");
        var wait = SpellId.Parse("spell:wait:v1");

        using (var log = new JsonLinesSlotChoiceLog(path))
        {
            log.Record(new SlotChoice(MatchId.New(), 3, PlayerSlot.Player2, CreatureId.From(5), strike, [new SpellValue(strike, 0, 0, 3), new SpellValue(wait, 0, 0, 0.5)]));
            log.Record(new SlotChoice(MatchId.New(), 4, PlayerSlot.Player1, CreatureId.From(1), wait, [new SpellValue(wait, 1, 0, 2)]));
        }

        var lines = File.ReadAllLines(path);
        lines.Length.ShouldBe(2);
        using var first = JsonDocument.Parse(lines[0]);
        first.RootElement.GetProperty("chosen").GetString().ShouldBe("spell:heavy_strike:v1");
        first.RootElement.GetProperty("seat").GetString().ShouldBe("Player2");
        first.RootElement.GetProperty("spells")[1].GetProperty("score").GetDouble().ShouldBe(0.5);
    }
}
