namespace DownfallArena.Application.Admin;

/// <summary>A page of the operators' actions, newest first, with how many are kept and how many pages they make.</summary>
public sealed record OperatorActionPage(IReadOnlyList<OperatorAction> Actions, int Total, int Page, int Pages);
