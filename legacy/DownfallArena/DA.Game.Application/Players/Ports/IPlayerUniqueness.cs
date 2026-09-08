namespace DA.Game.Application.Players.Ports;

public interface IPlayerUniqueness
{
    Task<bool> ExistsNameAsync(string name, CancellationToken ct = default);
}
