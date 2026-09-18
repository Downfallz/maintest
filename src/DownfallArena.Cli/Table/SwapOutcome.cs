namespace DownfallArena.Cli.Table;

/// <summary>
/// What a seat did with a swap, and everything about the seat that the answer depends on, read in the same
/// locked step that decided it.
/// </summary>
/// <remarks>
/// The three travel together because asking the seat again afterwards is asking a different question: a
/// pending swap can land between the two reads, and the caller would then name the wrong occupant as the one
/// being replaced and a round the match has already left. Every way this has gone wrong on this branch has
/// been a check and an action separated by something.
/// </remarks>
/// <param name="Held">Who held the seat at that moment, which is who a swap replaces.</param>
/// <param name="Reached">The newest round the seat had been asked about, or null when it had been asked nothing.</param>
/// <param name="Taken">Whether the swap was installed. When it was not, <see cref="Reached" /> says why.</param>
internal sealed record SwapOutcome(Occupant Held, int? Reached, bool Taken);
