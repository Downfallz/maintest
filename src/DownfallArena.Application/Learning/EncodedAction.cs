namespace DownfallArena.Application.Learning;

/// <summary>
/// One action as a dataset sees it: the stable key and the numeric code.
/// </summary>
public sealed record EncodedAction(string Key, ActionCode Code);
