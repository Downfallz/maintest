using DownfallArena.Application.Leaderboard;

namespace DownfallArena.Application.Accounts.Players;

/// <summary>
/// One account as the operator's list of players shows it (ADR 0174): the account, its activity, what it played --
/// every finished game, the ranked ones, those against people and those against a bot, and the ones it won -- and
/// its place on the ladder, null until it has a ranked game.
/// </summary>
public sealed record PlayerSummary(
    Account Account,
    AccountActivity? Activity,
    int Games,
    int Ranked,
    int AgainstPlayers,
    int AgainstBots,
    int Wins,
    Standing? Standing);
