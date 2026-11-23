namespace DownfallArena.Cli.Table;

/// <summary>
/// A push service that refused a notice, or did not answer in time (N6): the failure of the send itself, which the
/// notices worker counts as refused, and not the store or the queue around it, which are not the push service's.
/// </summary>
internal sealed class PushRefusedException : Exception
{
    public PushRefusedException()
    {
    }

    public PushRefusedException(string message)
        : base(message)
    {
    }

    public PushRefusedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
