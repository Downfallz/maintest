namespace DA.Game.Infrastructure;

public static class IdHelpers
{
    // "spell:lightning_bolt:v3" -> ("spell:lightning_bolt", 3)
    public static (string baseId, int version) SplitVersionedId(string id)
    {
        var idx = id.LastIndexOf(":v", StringComparison.Ordinal);
        if (idx < 0) return (id, 1);
        var baseId = id[..idx];
        var verStr = id[(idx + 2)..];
        return (baseId, int.TryParse(verStr, out var v) ? v : 1);
    }
}
