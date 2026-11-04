using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Cli.Studio;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A player's friends (ADR 0129): the list with who asked and who was asked, asking by name, accepting, and the
/// silent ways out. Every route is the signed-in account's own; a request from nobody is 401. A request and an
/// acceptance each reach the other player's devices as a notice, when the host sends any.
/// </summary>
internal sealed class FriendsApi(Friends friends, AccountApi accounts, AccountNotices? notices = null)
{
    public const string Prefix = "/api/friends";

    private const string RequestsPrefix = Prefix + "/requests";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static bool Names(string path) => path == Prefix || path.StartsWith(Prefix + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, AccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(request);
        if (await accounts.WhoAsync(request) is not { } who)
        {
            return Problem(401, "Account.SignInRequired", "Sign in to see your friends.");
        }

        try
        {
            var answer = (method, path) switch
            {
                ("GET", Prefix) => await ListAsync(who.Account.Id),
                ("POST", RequestsPrefix) => await AskAsync(who.Account, body),
                ("POST", _) when Requested(path, "/accept") is { } other => await AcceptAsync(who.Account, other),
                ("DELETE", _) when Requested(path, "") is { } other => await UnlinkAsync(who.Account.Id, other),
                ("DELETE", _) when path.StartsWith(Prefix + "/", StringComparison.Ordinal) && Id(path[(Prefix.Length + 1)..]) is { } other => await UnlinkAsync(who.Account.Id, other),
                _ => Problem(404, "Friend.NoRoute", "No such friends route."),
            };
            return who.Renewed is { } renewed ? answer with { Headers = [new KeyValuePair<string, string>("Set-Cookie", renewed)] } : answer;
        }
        catch (JsonException)
        {
            return Problem(400, "Friend.BadRequest", "The request is not valid JSON.");
        }
    }

    /// <summary>The list as the panel draws it: each link named by its account's current name, or "Player" for one that is gone.</summary>
    private async Task<StudioResponse> ListAsync(AccountId me)
    {
        var list = await friends.ListAsync(me);
        return Answer(new
        {
            friends = await Task.WhenAll(list.Friends.Select(DescribeAsync)),
            incoming = await Task.WhenAll(list.Incoming.Select(DescribeAsync)),
            outgoing = await Task.WhenAll(list.Outgoing.Select(DescribeAsync)),
        });
    }

    private async Task<StudioResponse> AskAsync(Account me, string body)
    {
        var asked = await friends.AskAsync(me.Id, Read<AskBody>(body)?.Username);
        if (asked.IsFailure)
        {
            return Refused(asked.Error);
        }

        if (notices is { } send)
        {
            // Told once either way: of the request, or, when asking back is accepting, of the friendship.
            var payload = asked.Value.Accepted
                ? AccountNotices.Payload("A new friend", $"{me.Username.Value} accepted your friend request.", "/lobby#friends", $"friend:{me.Id.Value}")
                : AccountNotices.Payload("A friend request", $"{me.Username.Value} wants to be your friend.", "/lobby#friends", $"friend:{me.Id.Value}");
            await send.SendAsync(asked.Value.Other, payload, "friend");
        }

        return Answer(new { accepted = asked.Value.Accepted, other = await DescribeAsync(new FriendLink(asked.Value.Other, default)) });
    }

    private async Task<StudioResponse> AcceptAsync(Account me, AccountId other)
    {
        var accepted = await friends.AcceptAsync(me.Id, other);
        if (accepted.IsFailure)
        {
            return Refused(accepted.Error);
        }

        if (notices is { } send)
        {
            await send.SendAsync(other, AccountNotices.Payload("A new friend", $"{me.Username.Value} accepted your friend request.", "/lobby#friends", $"friend:{me.Id.Value}"), "friend");
        }

        return Answer(new { accepted = true });
    }

    private async Task<StudioResponse> UnlinkAsync(AccountId me, AccountId other)
    {
        await friends.UnlinkAsync(me, other);
        return Answer(new { removed = true });
    }

    private async Task<object> DescribeAsync(FriendLink link)
    {
        var account = await accounts.AccountAsync(link.Other);
        return new { id = link.Other.Value, username = account?.Username.Value ?? "Player", since = link.Since == default ? (DateTimeOffset?)null : link.Since };
    }

    /// <summary>The account of <c>/api/friends/requests/&lt;id&gt;&lt;suffix&gt;</c>, or null for any other path.</summary>
    private static AccountId? Requested(string path, string suffix)
    {
        if (!path.StartsWith(RequestsPrefix + "/", StringComparison.Ordinal) || !path.EndsWith(suffix, StringComparison.Ordinal))
        {
            return null;
        }

        return Id(path[(RequestsPrefix.Length + 1)..^suffix.Length]);
    }

    private static AccountId? Id(string text) => AccountId.TryParse(text, out var id) ? id : null;

    private static StudioResponse Refused(DomainError error) => Problem(
        error == FriendErrors.NoSuchPlayer || error == FriendErrors.NoSuchRequest ? 404 : error == FriendErrors.Self ? 400 : 409,
        error.Code,
        error.Message);

    private static T? Read<T>(string body)
        where T : class => string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<T>(body, Web);

    private static StudioResponse Answer(object payload) => StudioResponse.OfJson(payload, Web);

    private static StudioResponse Problem(int status, string code, string message) =>
        StudioResponse.OfJson(new { error = code, message }, Web, status);

    private sealed record AskBody(string? Username);
}
