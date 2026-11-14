using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Hosting;

/// <summary>
/// The host's own settings, one record for the whole host (ADR 0177): whether a new player may make an account, and a
/// line every page shows under its header. Who saved them last, and when, for the panel to say.
/// </summary>
public sealed record HostSettings(bool RegistrationOpen, string? Notice, string? UpdatedBy = null, DateTimeOffset? UpdatedAt = null)
{
    /// <summary>The longest notice, in characters: one line under a header, on a phone.</summary>
    public const int LongestNotice = 200;

    /// <summary>What a host that never saved its settings reads: registration open and no notice.</summary>
    public static HostSettings Default { get; } = new(RegistrationOpen: true, Notice: null);

    /// <summary>A notice longer than a line under a header.</summary>
    public static readonly DomainError NoticeTooLong = new("Admin.NoticeTooLong", $"A notice is one line of at most {LongestNotice} characters.");

    /// <summary>
    /// The settings an operator saves: the notice made one line, its whitespace collapsed, and none when it is empty;
    /// refused when it is longer than <see cref="LongestNotice" />.
    /// </summary>
    public static Result<HostSettings> Create(bool registrationOpen, string? notice, string by, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        var line = string.Join(' ', (notice ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (line.Length > LongestNotice)
        {
            return Result.Failure<HostSettings>(NoticeTooLong);
        }

        return Result.Success(new HostSettings(registrationOpen, line.Length == 0 ? null : line, by, at));
    }
}
