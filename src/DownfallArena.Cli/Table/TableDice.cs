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
/// A die kept for the life of a process would throw differently in a process that picked the table up, and one
/// made again for each turn from the table's seed alone would throw the same numbers at every turn.
/// </remarks>
internal sealed class TableDice(IRandomSourceFactory factory, int seed)
{
    private readonly IRandomSourceFactory _factory = factory;
    private readonly int _seed = seed;
    private long _lines;

    /// <summary>Where the journal ends as a turn of the bots begins: what the dice of that turn are seeded from.</summary>
    public void At(JournalPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        Interlocked.Exchange(ref _lines, position.Lines);
    }

    /// <summary>The die a seat's bot throws, whoever sits there.</summary>
    public IRandomSource For(PlayerSlot slot) => new Die(this, slot);

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

    private sealed class Die(TableDice dice, PlayerSlot slot) : IRandomSource
    {
        private long _at = -1;
        private IRandomSource? _thrown;

        public int NextInt32(int minInclusive, int maxExclusive) => Thrown().NextInt32(minInclusive, maxExclusive);

        public double NextDouble() => Thrown().NextDouble();

        /// <summary>The source of the turn the table is at, made again when the table has moved to another.</summary>
        private IRandomSource Thrown()
        {
            var lines = Interlocked.Read(ref dice._lines);
            if (_thrown is null || _at != lines)
            {
                _thrown = dice._factory.Create(Seed(dice._seed, slot, lines));
                _at = lines;
            }

            return _thrown;
        }
    }
}
