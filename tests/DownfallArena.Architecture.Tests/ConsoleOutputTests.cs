namespace DownfallArena.Architecture.Tests;

/// <summary>
/// <see cref="Console.Out" /> is one writer for the whole process, and xUnit runs test classes side by side: a class
/// that swaps it reads another's lines, or restores a writer another one set (#656). Such a class joins the one
/// collection that runs alone, <c>ConsoleOutput</c>; a host's own lines are better read through a sink it is given.
/// </summary>
public sealed class ConsoleOutputTests
{
    private static readonly string[] Swaps = ["Console.SetOut(", "Console.SetError("];

    private const string RunsAlone = "[Collection(ConsoleOutput.Name)]";

    [Fact]
    public void Every_test_class_that_swaps_the_console_s_writer_runs_alone()
    {
        var racing = Directory.EnumerateFiles(RepositoryFiles.PathTo("tests"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path) is var text
                && Swaps.Any(swap => text.Contains(swap, StringComparison.Ordinal))
                && !text.Contains(RunsAlone, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryFiles.Root, path))
            .Order(StringComparer.Ordinal);

        racing.ShouldBeEmpty($"a test class that swaps Console.Out or Console.Error carries {RunsAlone} (.claude/rules/tests.md)");
    }
}
