namespace DownfallArena.Cli.Table;

/// <summary>What <see cref="SignInAttempts" /> holds for one address or network: its refusals, the people vouched for, and the sign-ins being checked.</summary>
internal sealed class SignInTally
{
    public List<DateTimeOffset> Refused { get; } = [];

    public List<(DateTimeOffset At, string Who)> Vouched { get; } = [];

    public int Checking { get; set; }

    public bool Idle => Refused.Count == 0 && Vouched.Count == 0 && Checking == 0;

    /// <summary>Lets go of what happened at or before <paramref name="before" />.</summary>
    public void Forget(DateTimeOffset before)
    {
        Refused.RemoveAll(at => at <= before);
        Vouched.RemoveAll(one => one.At <= before);
    }
}
