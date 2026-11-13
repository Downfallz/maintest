namespace DownfallArena.Cli.Table;

/// <summary>What a reaction button posts: the reaction's name, one of <see cref="TableReactions.Catalogue" />.</summary>
internal sealed record TableReactionBody
{
    public string? Reaction { get; init; }
}
