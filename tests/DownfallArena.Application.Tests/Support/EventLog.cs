using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using NSubstitute;
using Match = DownfallArena.Domain.Matches.Match;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// Every event the matches of a store raised, in the order they raised them, and the same as text: what two
/// ways of playing one match are compared on.
/// </summary>
internal sealed partial class EventLog
{
    private static readonly JsonSerializerOptions EventJson = new() { Converters = { new CreatureKey() } };

    private readonly List<IDomainEvent> _all = [];

    /// <summary>A log of everything the store's dispatcher is handed from now on.</summary>
    public static EventLog Of(MatchStore store)
    {
        var log = new EventLog();
        store.Dispatcher.DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>()).Returns(call => log.RecordAsync(call.Arg<Match>()));
        return log;
    }

    public IReadOnlyList<IDomainEvent> All => _all;

    /// <summary>
    /// The events as text, one a line, with the ids a match draws for itself blanked: two matches from the same
    /// seed differ in their own id and their rounds', and in nothing a player could see.
    /// </summary>
    public IReadOnlyList<string> Trace() =>
        [.. _all.Select(raised => $"{raised.GetType().Name} {DrawnId().Replace(JsonSerializer.Serialize(raised, raised.GetType(), EventJson), "id")}")];

    private Task RecordAsync(Match match)
    {
        _all.AddRange(match.DomainEvents);
        match.ClearDomainEvents();
        return Task.CompletedTask;
    }

    [GeneratedRegex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}")]
    private static partial Regex DrawnId();

    /// <summary>Writes a creature id where JSON wants text: some events key what they carry by creature.</summary>
    private sealed class CreatureKey : JsonConverter<CreatureId>
    {
        public override CreatureId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, CreatureId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

        public override void WriteAsPropertyName(Utf8JsonWriter writer, CreatureId value, JsonSerializerOptions options) => writer.WritePropertyName(value.ToString());
    }
}
