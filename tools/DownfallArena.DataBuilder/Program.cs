using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;

var dataDirectory = args.Length > 0 ? args[0] : "data";
var outputDirectory = args.Length > 1 ? args[1] : Path.Combine(dataDirectory, "dst");

try
{
    var schema = GameSchemaBuilder.Build(dataDirectory);
    GameSchemaBuilder.Write(schema, outputDirectory);

    await Console.Out.WriteLineAsync($"Built {schema.Spells.Count} spells, {schema.Creatures.Count} creatures, {schema.TalentTrees.Count} talent trees from '{dataDirectory}'.");
    await Console.Out.WriteLineAsync($"Content hash {schema.ContentHash} written to '{outputDirectory}'.");
    return 0;
}
catch (InvalidGameContentException exception)
{
    await Console.Error.WriteLineAsync("Invalid game content:");
    foreach (var problem in exception.Problems)
    {
        await Console.Error.WriteLineAsync(" - " + problem);
    }

    return 1;
}
catch (DirectoryNotFoundException exception)
{
    await Console.Error.WriteLineAsync(exception.Message);
    return 1;
}
