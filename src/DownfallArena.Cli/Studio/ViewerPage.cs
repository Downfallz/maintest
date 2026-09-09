using System.Text.Json;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// The viewer page (ADR 0013, decision J) carrying a studio run's artifacts, so a run opens on its own result
/// instead of asking for a file. Same shape as the run pages the Python side writes: the stylesheet inlined and
/// one JSON block the page reads at start.
/// </summary>
internal static class ViewerPage
{
    private const string StylesheetLink = "<link rel=\"stylesheet\" href=\"viewer.css\">";
    private const string ScriptStart = "  <script>\n    'use strict';";
    private const string EmbeddedId = "embedded-artifacts";

    /// <summary>
    /// The page carrying one run's artifacts, opening on the first of them.
    /// </summary>
    public static string Render(string viewerDirectory, string run, IReadOnlyList<(string Name, string Text)> artifacts) =>
        Render(viewerDirectory, run, artifacts, compare: false);

    /// <summary>
    /// With <paramref name="compare"/> the page opens on the comparison of the two artifacts it carries. It has
    /// to be said rather than guessed from the count: the Python side writes run pages that carry two
    /// evaluations and nothing else, and those must keep opening on the first artifact.
    /// </summary>
    public static string Render(string viewerDirectory, string run, IReadOnlyList<(string Name, string Text)> artifacts, bool compare)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerDirectory);
        ArgumentNullException.ThrowIfNull(artifacts);

        var index = Path.Combine(viewerDirectory, "index.html");
        var stylesheet = Path.Combine(viewerDirectory, "viewer.css");
        if (!File.Exists(index) || !File.Exists(stylesheet))
        {
            throw new FileNotFoundException($"No viewer under '{viewerDirectory}' (index.html and viewer.css).", index);
        }

        var page = Normalize(File.ReadAllText(index));
        if (!page.Contains(StylesheetLink, StringComparison.Ordinal) || !page.Contains(ScriptStart, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{index}' does not look like the viewer this page is built from.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            run,
            compare,
            artifacts = artifacts.Select(artifact => new { path = $"{run}/{artifact.Name}", name = artifact.Name, text = artifact.Text }),
        });

        // A closing tag inside the JSON would end the script element early; the escape is valid JSON.
        payload = payload.Replace("</", "<\\/", StringComparison.Ordinal);
        page = ReplaceFirst(page, StylesheetLink, $"<style>\n{Normalize(File.ReadAllText(stylesheet))}\n  </style>");
        return ReplaceFirst(page, ScriptStart, $"  <script id=\"{EmbeddedId}\" type=\"application/json\">{payload}</script>\n{ScriptStart}");
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string ReplaceFirst(string text, string search, string replacement)
    {
        var index = text.IndexOf(search, StringComparison.Ordinal);
        return text[..index] + replacement + text[(index + search.Length)..];
    }
}
