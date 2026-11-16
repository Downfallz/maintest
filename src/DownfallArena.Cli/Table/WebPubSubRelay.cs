using Azure.Core;
using Azure.Messaging.WebPubSub;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The relay on Azure Web PubSub (ADR 0165), reached by the process's own identity: the service takes no key. A seat's
/// page joins its table's group, a lobby the lobby's group under its account as the user, and a process the hosts'
/// group; each message is a word, and the hosts' one also names the table and who sent it.
/// </summary>
internal sealed class WebPubSubRelay : IChangeRelay
{
    /// <summary>The one hub every message goes through.</summary>
    public const string Hub = "downfall";

    /// <summary>The variable that names the service's endpoint, <c>https://&lt;name&gt;.webpubsub.azure.com</c>. Unset, no relay.</summary>
    public const string Environment = "DOWNFALL_RELAY";

    /// <summary>How long a page's or a process's connection address is good for; a connection made with it outlives it.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public const string HostsGroup = "hosts";

    public const string LobbyGroup = "lobby";

    private readonly WebPubSubServiceClient _client;
    private readonly CoalescedSends _sends;

    public WebPubSubRelay(Uri endpoint, TokenCredential credential)
    {
        _client = new WebPubSubServiceClient(endpoint, Hub, credential);
        _sends = new CoalescedSends(SendAsync);
    }

    public string Origin { get; } = Guid.CreateVersion7().ToString("N");

    /// <summary>The relay the environment names, or none.</summary>
    public static WebPubSubRelay? FromEnvironment()
    {
        var endpoint = System.Environment.GetEnvironmentVariable(Environment);
        return string.IsNullOrWhiteSpace(endpoint) ? null : new WebPubSubRelay(new Uri(endpoint), new Azure.Identity.DefaultAzureCredential());
    }

    public static string TableGroup(string table) => $"table-{table}";

    /// <summary>What a process reads off a hosts message: who sent it, and the table it moved.</summary>
    public static (string Origin, string Table)? Moved(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var bar = message.IndexOf('|', StringComparison.Ordinal);
        return bar <= 0 || bar == message.Length - 1 ? null : (message[..bar], message[(bar + 1)..]);
    }

    public Task SentAsync() => _sends.Idle;

    public void Table(string table) => _sends.Post($"t:{table}");

    public void Lobby() => _sends.Post("l:");

    public void Account(AccountId account) => _sends.Post($"a:{account.Value}");

    public async Task<Uri> SeatAsync(string table, CancellationToken cancellationToken = default) =>
        await _client.GetClientAccessUriAsync(Lifetime, userId: null, roles: null, groups: [TableGroup(table)], cancellationToken);

    public async Task<Uri> LobbyAsync(AccountId? account, CancellationToken cancellationToken = default) =>
        await _client.GetClientAccessUriAsync(Lifetime, userId: account?.Value, roles: null, groups: [LobbyGroup], cancellationToken);

    public async Task<Uri> HostsAsync(CancellationToken cancellationToken = default) =>
        await _client.GetClientAccessUriAsync(Lifetime, userId: $"host-{Origin}", roles: null, groups: [HostsGroup], cancellationToken);

    private async Task SendAsync(string key)
    {
        var value = key[2..];
        switch (key[0])
        {
            case 't':
                await _client.SendToGroupAsync(TableGroup(value), "changed", ContentType.TextPlain);
                await _client.SendToGroupAsync(HostsGroup, $"{Origin}|{value}", ContentType.TextPlain);
                break;
            case 'l':
                await _client.SendToGroupAsync(LobbyGroup, "lobby", ContentType.TextPlain);
                break;
            default:
                await _client.SendToUserAsync(value, "account", ContentType.TextPlain);
                break;
        }
    }
}
