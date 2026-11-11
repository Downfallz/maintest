namespace DownfallArena.Cli;

/// <summary>
/// The game schema file a host was built over, as the command line named it: what a table host keeps a copy of
/// for the tables it opens (ADR 0163).
/// </summary>
internal sealed record GameContentFile(string SchemaPath);
