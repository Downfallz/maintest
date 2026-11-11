using System.Collections.Concurrent;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A relay that keeps what it was told and hands out addresses that name what they were asked for.</summary>
internal sealed class RecordedRelay : IChangeRelay
{
    public ConcurrentQueue<string> Said { get; } = new();

    public string Origin { get; init; } = "this-process";

    public void Table(string table) => Said.Enqueue($"table {table}");

    public void Lobby() => Said.Enqueue("lobby");

    public void Account(AccountId account) => Said.Enqueue($"account {account.Value}");

    public Task<Uri> SeatAsync(string table, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Uri($"wss://relay.example/client/hubs/downfall?group=table-{table}"));

    public Task<Uri> LobbyAsync(AccountId? account, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Uri($"wss://relay.example/client/hubs/downfall?user={account?.Value ?? "nobody"}"));

    public Task<Uri> HostsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new Uri("wss://relay.example/client/hubs/downfall?group=hosts"));

    public Task SentAsync() => Task.CompletedTask;
}
