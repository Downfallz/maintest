namespace DownfallArena.Application.Evaluation;

/// <summary>
/// A mean and the bounds of its 95% confidence interval.
/// </summary>
public sealed record ConfidenceInterval(double Mean, double Low, double High);
