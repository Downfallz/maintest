using System.Text.RegularExpressions;

namespace DownfallArena.Architecture.Tests;

/// <summary>
/// A token or a key the host checks is compared in fixed time (#862): an ordinal comparison stops at the first
/// character that differs, so its time tells how much of a guess is right. The hosts compare them through
/// <c>FixedTime.Same</c>, as <c>OperatorGate</c> and the pilot's check did first.
/// </summary>
public sealed partial class SecretComparisonTests
{
    [Fact]
    public void No_host_compares_a_token_or_a_key_with_string_Equals()
    {
        var comparing = Directory.EnumerateFiles(RepositoryFiles.PathTo(Path.Combine("src", "DownfallArena.Cli")), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Contains("obj"))
            .SelectMany(path => File.ReadAllLines(path).Where(ComparesASecret).Select(line => $"{Path.GetRelativePath(RepositoryFiles.Root, path)}: {line.Trim()}"))
            .Order(StringComparer.Ordinal);

        comparing.ShouldBeEmpty("compare a token or a key with FixedTime.Same (#862)");
    }

    [Theory]
    [InlineData("if (!string.Equals(supplied, token, StringComparison.Ordinal))", true)]
    [InlineData("seats.FirstOrDefault(seat => string.Equals(seat.Token, token, StringComparison.Ordinal));", true)]
    [InlineData("record.Seats.Any(seat => seat.Person && string.Equals(seat.Token, key, StringComparison.Ordinal))", true)]
    [InlineData("if (!string.Equals(content.Version, document.Content, StringComparison.Ordinal))", false)]
    public void A_comparison_naming_a_token_or_a_key_is_found(string line, bool found) =>
        ComparesASecret(line).ShouldBe(found);

    private static bool ComparesASecret(string line) => Secret().IsMatch(line);

    [GeneratedRegex(@"string\.Equals\([^;]*\b(?:[Tt]oken|[Kk]ey|supplied)\b")]
    private static partial Regex Secret();
}
