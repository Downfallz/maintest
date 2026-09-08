using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;

var dataDirectory = args.Length > 0 ? args[0] : "data";
var outputDirectory = args.Length > 1 ? args[1] : Path.Combine(dataDirectory, "dst");

try
{
    var schema = GameSchemaBuilder.Build(dataDirectory);
    GameSchemaBuilder.Write(schema, outputDirectory);

    Console.WriteLine($"Built {schema.Spells.Count} spells, {schema.Creatures.Count} creatures, {schema.TalentTrees.Count} talent trees from '{dataDirectory}'.");
    Console.WriteLine($"Content hash {schema.ContentHash} written to '{outputDirectory}'.");
    return 0;
}
catch (InvalidGameContentException exception)
{
    Console.Error.WriteLine("Invalid game content:");
    foreach (var problem in exception.Problems)
    {
        Console.Error.WriteLine(" - " + problem);
    }

    return 1;
}
catch (DirectoryNotFoundException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
