using System.Text.RegularExpressions;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The page carries no content. Not "should not": a tuning pass has to be a rebuild and a restart, and the
/// moment a spell name, a class or an effect word is written into the page, the next pass needs a diff in
/// `table/` — which is the whole thing stage 3 exists to prevent (docs/tabletop/app-roadmap.md).
/// </summary>
/// <remarks>
/// The words are read off the domain rather than listed, so a class or an effect added later is swept too. The
/// numbers are not swept here: a number in a page is an index, a poll interval or a slice bound as often as it
/// is a stat, and what holds the numbers is the renderer's own test — `card.test.js` draws every field from its
/// argument and has no default to fall back on.
/// </remarks>
public sealed partial class TablePageContentTests
{
    private static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "table");

    public static TheoryData<string> Shipped =>
    [
        .. System.IO.Directory.GetFiles(Directory, "*.js")
            .Select(Path.GetFileName)
            .Where(name => !name!.EndsWith(".test.js", StringComparison.Ordinal))
            .Concat(["index.html"])
            .Select(name => name!),
    ];

    [Theory]
    [MemberData(nameof(Shipped))]
    public void No_file_the_page_ships_names_a_piece_of_content(string file)
    {
        var text = File.ReadAllText(Path.Combine(Directory, file));

        foreach (var word in Content())
        {
            // As a word, not as a substring: `maxHealth` is a stat a board reads off its snapshot and `Heal`
            // is an effect the page may not name, and a sweep that could not tell them apart would be one
            // nobody could keep.
            Whole(word).IsMatch(text)
                .ShouldBeFalse($"{file} names {word}, so changing the content would need a change to the page");
        }
    }

    [Fact]
    public void The_sweep_reads_a_word_and_not_a_fragment_of_one()
    {
        Whole("Heal").IsMatch("Heal 4").ShouldBeTrue();
        Whole("Heal").IsMatch("creature.maxHealth").ShouldBeFalse();
        Whole("Stun").IsMatch("Stun, 1 round").ShouldBeTrue();
        Whole("Stun").IsMatch("creature.isStunned").ShouldBeFalse();
    }

    private static Regex Whole(string word) =>
        new($@"\b{Regex.Escape(word)}\b", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// An id, matched by its shape rather than by its prefix: <c>creature:</c> is also how a decision names the
    /// creature it is about, and a page is allowed to post one of those. What it may not carry is
    /// <c>spell:pummel:v1</c> — a card the page knows by name.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shipped))]
    public void No_file_the_page_ships_carries_a_versioned_id(string file)
    {
        var text = File.ReadAllText(Path.Combine(Directory, file));

        VersionedId().Matches(text).ShouldBeEmpty($"{file} carries the id of a piece of content");
    }

    [Fact]
    public void The_shape_that_is_swept_for_is_the_shape_an_id_has()
    {
        VersionedId().IsMatch("spell:pummel:v1").ShouldBeTrue();
        VersionedId().IsMatch("creature:revenant-guard:v1").ShouldBeTrue();
        VersionedId().IsMatch("talent-tree:base:v1").ShouldBeTrue();
        VersionedId().IsMatch("{ kind: 'Intent', creature: view.waitingCreature }").ShouldBeFalse();
    }

    [GeneratedRegex(@"\b(spell|creature|talent-tree):[a-z0-9][a-z0-9-]*", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex VersionedId();

    [Fact]
    public void The_sweep_reads_the_words_off_the_domain_rather_than_a_list_that_goes_stale()
    {
        var words = Content();

        words.ShouldContain("Brawler");
        words.ShouldContain("Bleed");
        words.ShouldContain("Offensive");
        words.Count.ShouldBeGreaterThan(20);
    }

    /// <summary>
    /// What "content" means here: the versioned id of anything authored, every creature class, every spell
    /// type, and the name of every effect the domain defines. <c>Creature</c> is left out of the classes: it is
    /// the game's word for a piece on the board, and the page is allowed to say it.
    /// </summary>
    private static IReadOnlyList<string> Content() =>
    [
        .. Enum.GetNames<CreatureClass>().Where(name => name != nameof(CreatureClass.Creature)),
        .. Enum.GetNames<SpellType>(),
        .. typeof(Effect).Assembly.GetTypes()
            .Where(type => type.IsSealed && typeof(Effect).IsAssignableFrom(type))
            .Select(type => type.Name),
    ];
}
