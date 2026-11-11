using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The dice a table's bots throw, seeded for each of their turns from the table's seed, the seat and where the
/// journal ends as the turn begins (ADR 0159, phase 5): any process that takes the same turn of the same table
/// throws the same numbers, whether it has held the table since it opened or picked it up a moment ago.
/// </summary>
/// <remarks>
/// <para>
/// A die kept for the life of a process would throw differently in a process that picked the table up, and one
/// made again for each turn from the table's seed alone would throw the same numbers at every turn.
/// </para>
/// <para>
/// Every turn starts its dice from the start, a turn taken again at the same place in the journal included: a
/// turn whose line could not be written is taken again by the same process on the same numbers, as a process that
/// picked the table up would take it. And what a bot throws as it is made -- a searching agent draws the seed of
/// its rollouts then -- is thrown from the table's seed and the seat alone, until the table is next told where a
/// turn begins: a bot seated mid-match is made again, before any turn, by a process that picks the table up.
/// </para>
/// </remarks>
internal sealed class TableDice(IRandomSourceFactory factory, int seed)
{
    private readonly IRandomSourceFactory _factory = factory;
    private readonly int _seed = seed;
    private long _lines;

    // How many times a turn has begun: a die throws a turn's numbers from that turn's start.
    private long _turns;

    /// <summary>What a die throws until the table is next told where a turn begins: what a bot throws as it is made.</summary>
    private const long Made = -1;

    /// <summary>Where the journal ends as a turn of the bots begins: what the dice of that turn are seeded from.</summary>
    public void At(JournalPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        Interlocked.Exchange(ref _lines, position.Lines);
        Interlocked.Increment(ref _turns);
    }

    /// <summary>The die a seat's bot throws, whoever sits there.</summary>
    public IRandomSource For(PlayerSlot slot) => new Die(this, slot, Interlocked.Read(ref _turns));

    /// <summary>
    /// A turn's seed: the table's seed, the seat and the journal's length, mixed so that turns next to each other
    /// are not seeded next to each other. Fixed arithmetic, never a process's hash, which is salted per run.
    /// </summary>
    internal static int Seed(int seed, PlayerSlot slot, long lines)
    {
        unchecked
        {
            var mixed = ((ulong)(uint)seed * 0x9E3779B97F4A7C15UL) ^ ((ulong)(slot == PlayerSlot.Player1 ? 1 : 2) << 56) ^ (ulong)lines;
            mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
            mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
            mixed ^= mixed >> 31;
            return (int)(mixed & 0x7FFFFFFF);
        }
    }

    private sealed class Die(TableDice dice, PlayerSlot slot, long made) : IRandomSource
    {
        private long _at = long.MinValue;
        private IRandomSource? _thrown;

        public int NextInt32(int minInclusive, int maxExclusive) => Thrown().NextInt32(minInclusive, maxExclusive);

        public double NextDouble() => Thrown().NextDouble();

        /// <summary>
        /// The source of the turn the table is at, made again at every turn; the one a bot throws as it is made,
        /// until the table is next told where a turn begins.
        /// </summary>
        private IRandomSource Thrown()
        {
            var turn = Interlocked.Read(ref dice._turns);
            if (_thrown is null || _at != turn)
            {
                _thrown = dice._factory.Create(Seed(dice._seed, slot, turn == made ? Made : Interlocked.Read(ref dice._lines)));
                _at = turn;
            }

            return _thrown;
        }
    }
}
