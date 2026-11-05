using System.Text.Json;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The shape a friend store writes, the same in a directory and in a blob container: one document an account at
/// <c>friends/&lt;accountId&gt;.json</c>, holding its friends, who asked it and whom it asked, each by id and date.
/// </summary>
internal static class FriendDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string PathOf(AccountId account) => $"friends/{account.Value}.json";

    public static bool IsEmpty(FriendList list) => list.Friends.Count == 0 && list.Incoming.Count == 0 && list.Outgoing.Count == 0;

    public static string Write(FriendList list) =>
        JsonSerializer.Serialize(new ListRecord(Links(list.Friends), Links(list.Incoming), Links(list.Outgoing)), Web);

    public static FriendList Read(string json)
    {
        var record = JsonSerializer.Deserialize<ListRecord>(json, Web) ?? throw new InvalidDataException("A friends record is empty.");
        return new FriendList(Links(record.Friends), Links(record.Incoming), Links(record.Outgoing));
    }

    private static IReadOnlyList<LinkRecord> Links(IReadOnlyList<FriendLink> links) => [.. links.Select(link => new LinkRecord(link.Other.Value, link.Since))];

    private static IReadOnlyList<FriendLink> Links(IReadOnlyList<LinkRecord>? links) => [.. (links ?? []).Select(link => new FriendLink(AccountId.Parse(link.Account), link.Since))];

    private sealed record ListRecord(IReadOnlyList<LinkRecord>? Friends, IReadOnlyList<LinkRecord>? Incoming, IReadOnlyList<LinkRecord>? Outgoing);

    private sealed record LinkRecord(string Account, DateTimeOffset Since);
}
