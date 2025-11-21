namespace DA.Game.Shared.Tests;

internal static class TestIdFactory
{
    public static string CreateSpellIdString(string name = "fire_bolt", int version = 1)
        => $"spell:{name}:v{version}";

    public static string CreateCharacterDefIdString(string name = "warrior", int version = 1)
        => $"creature:{name}:v{version}";
}
