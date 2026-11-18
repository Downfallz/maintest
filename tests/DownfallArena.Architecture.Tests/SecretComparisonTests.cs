using System.Text.RegularExpressions;

namespace DownfallArena.Architecture.Tests;

/// <summary>
/// A token, a key, an invitation or a join code the host checks is compared in fixed time (#862): an ordinal
/// comparison stops at the first character that differs, so its time tells how much of a guess is right. The hosts
/// compare them through <c>FixedTime.Same</c>, never <c>string.Equals</c>, <c>==</c> or <c>!=</c>, as
/// <c>OperatorGate</c> and the pilot's check did first.
/// </summary>
public sealed partial class SecretComparisonTests
{
    [Fact]
    public void No_host_compares_a_token_a_key_an_invitation_or_a_code_ordinally()
    {
        var comparing = Directory.EnumerateFiles(RepositoryFiles.PathTo(Path.Combine("src", "DownfallArena.Cli")), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Contains("obj"))
            .SelectMany(path => File.ReadAllLines(path).Where(ComparesASecret).Select(line => $"{Path.GetRelativePath(RepositoryFiles.Root, path)}: {line.Trim()}"))
            .Order(StringComparer.Ordinal);

        comparing.ShouldBeEmpty("compare a token, a key, an invitation or a code with FixedTime.Same (#862)");
    }

    [Theory]
    [InlineData("if (!string.Equals(supplied, token, StringComparison.Ordinal))", true)]
    [InlineData("seats.FirstOrDefault(seat => string.Equals(seat.Token, token, StringComparison.Ordinal));", true)]
    [InlineData("record.Seats.Any(seat => seat.Person && string.Equals(seat.Token, key, StringComparison.Ordinal))", true)]
    [InlineData("&& (table.Pilot.Token == token || table.Seats.Any(seat => seat.Token == token))", true)]
    [InlineData("table.Seats.FirstOrDefault(seat => seat.Invitation == invitation)", true)]
    [InlineData("seats.Single(seat => seat.Token == one.Token)", true)]
    [InlineData("seat.Name == indexed.Seat && seat.Code == code", true)]
    [InlineData("if (!string.Equals(content.Version, document.Content, StringComparison.Ordinal))", false)]
    [InlineData("if (seat.Invitation == null || held.Token != null)", false)]
    [InlineData("error.Code == \"Lobby.Gone\"", false)]
    public void A_comparison_naming_a_token_or_a_key_is_found(string line, bool found) =>
        ComparesASecret(line).ShouldBe(found);

    private static bool ComparesASecret(string line) => Secret().IsMatch(line);

    [GeneratedRegex(@"string\.Equals\([^;]*\b(?:[Tt]oken|[Kk]ey|supplied)\b|\b(?:Token|Invitation|token|invitation)\s*[!=]=(?!\s*null\b)|[!=]=\s*[\w.]*(?:Token|Invitation)\b|\.Code\s*[!=]=\s*code\b")]
    private static partial Regex Secret();
}
