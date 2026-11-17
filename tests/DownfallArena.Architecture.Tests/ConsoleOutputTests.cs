using System.Text.RegularExpressions;

namespace DownfallArena.Architecture.Tests;

/// <summary>
/// <see cref="Console.Out" /> is one writer for the whole process, and xUnit runs test classes in parallel: a class
/// that swaps it reads another's lines, or restores a writer another one set (#656). Such a class joins the one
/// collection that runs alone, <c>ConsoleOutput</c>; a host's own lines are better read through a sink it is given.
/// </summary>
public sealed partial class ConsoleOutputTests
{
    private static readonly string[] Swaps = ["Console.SetOut(", "Console.SetError("];

    private const string RunsAlone = "[Collection(ConsoleOutput.Name)]";

    /// <summary>This file names the swaps it looks for, and swaps nothing.</summary>
    private static readonly string Itself = Path.Combine("tests", "DownfallArena.Architecture.Tests", "ConsoleOutputTests.cs");

    [Fact]
    public void Every_test_class_that_swaps_the_console_s_writer_runs_alone()
    {
        var racing = Directory.EnumerateFiles(RepositoryFiles.PathTo("tests"), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryFiles.Root, path))
            .Where(path => path != Itself && !path.Split(Path.DirectorySeparatorChar).Contains("obj"))
            .SelectMany(path => TopLevelTypes(File.ReadAllText(RepositoryFiles.PathTo(path))).Select(type => (path, type)))
            .Where(found => Swaps.Any(swap => found.type.Body.Contains(swap, StringComparison.Ordinal))
                && !found.type.Attributes.Contains(RunsAlone, StringComparison.Ordinal))
            .Select(found => $"{found.path}: {found.type.Name}")
            .Order(StringComparer.Ordinal);

        racing.ShouldBeEmpty($"a test class that swaps Console.Out or Console.Error carries {RunsAlone} (.claude/rules/tests.md)");
    }

    /// <summary>
    /// Each type a file declares at its top level, under a file-scoped namespace: the attributes just above it, its
    /// name, and its text up to the next one, nested types and helpers included.
    /// </summary>
    private static IEnumerable<(string Attributes, string Name, string Body)> TopLevelTypes(string text)
    {
        var declarations = TopLevelDeclaration().Matches(text);
        for (var index = 0; index < declarations.Count; index++)
        {
            var declaration = declarations[index];
            var end = index + 1 < declarations.Count ? declarations[index + 1].Index : text.Length;
            yield return (declaration.Groups["attributes"].Value, declaration.Groups["name"].Value, text[declaration.Index..end]);
        }
    }

    [GeneratedRegex(@"^(?<attributes>(?:\[[^\n]*\]\r?\n)*)(?:[a-z]+ )*(?:class|record|struct) (?<name>\w+)", RegexOptions.Multiline)]
    private static partial Regex TopLevelDeclaration();
}
