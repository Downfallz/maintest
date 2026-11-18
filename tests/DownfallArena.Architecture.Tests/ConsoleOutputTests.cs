namespace DownfallArena.Architecture.Tests;

/// <summary>
/// <see cref="Console.Out" /> is one writer for the whole process, and xUnit runs test classes in parallel: a test that
/// swaps it reads another's lines, or restores a writer another one set (#656). A collection that runs alone did not
/// close that: a helper in one class could swap the console for callers in another, which a scan of source text cannot
/// follow (#801). Every host a test reads takes a sink instead (#792, #799, #800), so no test swaps the console at all.
/// </summary>
public sealed class ConsoleOutputTests
{
    private static readonly string[] Swaps = ["Console.SetOut(", "Console.SetError("];

    /// <summary>This file names the swaps it looks for, and swaps nothing.</summary>
    private static readonly string Itself = Path.Combine("tests", "DownfallArena.Architecture.Tests", "ConsoleOutputTests.cs");

    [Fact]
    public void No_test_swaps_the_console_s_writer()
    {
        var swapping = Directory.EnumerateFiles(RepositoryFiles.PathTo("tests"), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryFiles.Root, path))
            .Where(path => path != Itself && !path.Split(Path.DirectorySeparatorChar).Contains("obj"))
            .Where(path => SwapsTheConsole(File.ReadAllText(RepositoryFiles.PathTo(path))))
            .Order(StringComparer.Ordinal);

        swapping.ShouldBeEmpty("read what a host says through a sink it is given, never by swapping Console.Out (.claude/rules/tests.md)");
    }

    /// <summary>
    /// The case the per-class check let through (#801): a helper that swaps the console, in a class that ran alone,
    /// called from a class that did not.
    /// </summary>
    [Fact]
    public void A_helper_that_swaps_the_console_for_another_class_is_found()
    {
        const string text = """
            [Collection(ConsoleOutput.Name)]
            internal static class Captured
            {
                public static string Of(Action act) { var writer = new StringWriter(); Console.SetOut(writer); act(); return writer.ToString(); }
            }

            public sealed class ParallelTests
            {
                [Fact]
                public void Reads() => Captured.Of(() => { }).ShouldBeEmpty();
            }
            """;

        SwapsTheConsole(text).ShouldBeTrue();
    }

    private static bool SwapsTheConsole(string text) => Swaps.Any(swap => text.Contains(swap, StringComparison.Ordinal));
}
