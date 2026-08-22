using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ShowroomDocs;

/// <summary>
/// Writes a chapter's page: narration and code interleaved in file order, with each test's captured
/// output directly beneath the code that ends it.
/// </summary>
internal sealed class PageWriter(CapturedOutput captured)
{
    public void WriteChapter(Chapter chapter, string outputDirectory)
    {
        StringBuilder page = new();

        page.AppendLine($"# {chapter.Number} - {chapter.Title}");
        page.AppendLine();

        // A chapter that declares no test is a chapter about the lane rather than a chapter that runs:
        // it gets no badges and no command, because both would describe something that does not exist.
        if (chapter.Tests.Count > 0)
        {
            page.AppendLine(Badges(chapter));
            page.AppendLine();
            page.AppendLine("Run the whole chapter:");
            page.AppendLine();
            page.AppendLine("```bash");
            page.AppendLine(ChapterCommand(chapter));
            page.AppendLine("```");
            page.AppendLine();
        }

        foreach (ChapterBlock block in chapter.Blocks)
        {
            if (block.Kind == BlockKind.Narration)
            {
                page.AppendLine(Paragraphs(block.Lines));
                page.AppendLine();
                continue;
            }

            page.AppendLine("```csharp");
            foreach (string line in block.Lines)
            {
                page.AppendLine(line);
            }

            page.AppendLine("```");
            page.AppendLine();

            foreach (ChapterTest test in block.ClosingTests)
            {
                page.AppendLine(OutputPanel(chapter, test));
                page.AppendLine();
            }
        }

        page.AppendLine("---");
        page.AppendLine();
        page.AppendLine(
            $"Source: [`{chapter.Lane}/{chapter.FileName}`]" +
            $"(https://github.com/DeadMoon0/TestFramework-Showroom/blob/main/{chapter.Lane}/{chapter.FileName})");

        string laneDirectory = Path.Combine(outputDirectory, LaneSlug(chapter.Lane));
        Directory.CreateDirectory(laneDirectory);
        File.WriteAllText(Path.Combine(laneDirectory, chapter.Slug + ".md"), page.ToString());

        WriteSnippet(chapter, outputDirectory);
    }

    /// <summary>
    /// The same code without the narration, for Learn pages to include. A Learn page must never show
    /// the <c>//doc:</c> markers, and must never hold a second copy of the code either.
    /// </summary>
    private static void WriteSnippet(Chapter chapter, string outputDirectory)
    {
        IEnumerable<string> code = chapter.Blocks
            .Where(block => block.Kind == BlockKind.Code)
            .SelectMany(block => block.Lines.Append(string.Empty));

        string directory = Path.Combine(outputDirectory, "snippets", LaneSlug(chapter.Lane));
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, chapter.Slug + ".cs"), code);
    }

    private string OutputPanel(Chapter chapter, ChapterTest test)
    {
        TestOutcome? outcome = captured.For(test.FullyQualifiedName);
        string command = TestCommand(chapter, test);

        StringBuilder panel = new();
        string heading = outcome is null
            ? $"Output - {test.Name} (not captured yet)"
            : $"Output - {test.Name} ({outcome.Outcome.ToLowerInvariant()}{Duration(outcome)})";

        panel.AppendLine("<details class=\"tf-output\">");
        panel.AppendLine($"<summary>{heading}</summary>");
        panel.AppendLine();
        panel.AppendLine("```bash");
        panel.AppendLine(command);
        panel.AppendLine("```");
        panel.AppendLine();

        if (outcome is null)
        {
            panel.AppendLine(
                "> [!NOTE]\n" +
                "> No output has been captured for this test yet. Run `Capture-ShowroomOutput.ps1`.");
        }
        else if (!string.IsNullOrWhiteSpace(outcome.SkipReason))
        {
            // A skip is not a hole in the documentation: it states the chapter's prerequisite in the
            // words the test itself uses.
            panel.AppendLine("```console");
            panel.AppendLine($"Skipped: {outcome.SkipReason.Trim()}");
            panel.AppendLine("```");
        }
        else if (string.IsNullOrWhiteSpace(outcome.StandardOutput))
        {
            panel.AppendLine("```console");
            panel.AppendLine("(the test printed nothing)");
            panel.AppendLine("```");
        }
        else
        {
            panel.AppendLine("```console");
            panel.AppendLine(outcome.StandardOutput.TrimEnd());
            panel.AppendLine("```");
        }

        if (outcome?.DurationMs is not null && captured.CapturedIn is not null)
        {
            panel.AppendLine();
            panel.AppendLine($"<p class=\"tf-measured\">{captured.CapturedIn.Caption()}</p>");
        }

        panel.AppendLine();
        panel.AppendLine("</details>");
        return panel.ToString().TrimEnd();
    }

    /// <summary>
    /// The exact measured figure. Formatted by magnitude so a 40 ms step and a 90 second container start
    /// are both readable, but never rounded into a range - the number is the point.
    /// </summary>
    private static string Duration(TestOutcome outcome)
    {
        if (outcome.DurationMs is not double milliseconds)
        {
            return string.Empty;
        }

        return milliseconds >= 10_000
            ? $", {milliseconds / 1000:F1} s"
            : $", {milliseconds:F0} ms";
    }

    /// <summary>
    /// What the chapter needs, and whether it runs at all. Both are stated before the code, because a
    /// reader decides whether to follow along from this line.
    /// </summary>
    /// <remarks>
    /// "Runs anywhere" is only emitted when it is true of every test in the chapter. A platform gate or a
    /// standing skip replaces it rather than joining it: a reader on Linux who is told a chapter runs
    /// anywhere, and then cannot run it, has been misled by the page and not by the framework.
    /// </remarks>
    private static string Badges(Chapter chapter)
    {
        List<string> badges = [];

        if (chapter.Tests.Any(test => test.Prerequisite == Prerequisite.NeedsDocker))
        {
            badges.Add("<span class=\"tf-badge tf-badge-warn\">needs a Docker daemon</span>");
        }

        if (chapter.Tests.Any(test => test.Prerequisite == Prerequisite.NeedsWindows))
        {
            badges.Add("<span class=\"tf-badge tf-badge-warn\">Windows only</span>");
        }

        if (chapter.Tests.All(test => test.Prerequisite == Prerequisite.RunsAnywhere))
        {
            badges.Add("<span class=\"tf-badge tf-badge-ok\">runs anywhere</span>");
        }

        // A standing skip is not a machine problem, so it is said separately from the prerequisites.
        if (chapter.Tests.Count > 0 && chapter.Tests.All(test => test.SkipReason is not null))
        {
            badges.Add("<span class=\"tf-badge tf-badge-warn\">skipped unless you edit it</span>");
        }

        badges.Add($"<span class=\"tf-badge\">{chapter.Tests.Count} test{(chapter.Tests.Count == 1 ? string.Empty : "s")}</span>");

        foreach (string trait in chapter.Tests.SelectMany(test => test.Traits).Distinct())
        {
            badges.Add($"<span class=\"tf-badge\">{trait}</span>");
        }

        return $"<div class=\"tf-badges\">{string.Join(string.Empty, badges)}</div>";
    }

    private static string ChapterCommand(Chapter chapter) =>
        $"dotnet test {chapter.LaneProject} -c Release --filter \"{ChapterFilter(chapter)}\"";

    private static string TestCommand(Chapter chapter, ChapterTest test) =>
        $"dotnet test {chapter.LaneProject} -c Release --filter \"FullyQualifiedName={test.FullyQualifiedName}\"";

    /// <summary>
    /// A filter that runs every test the chapter declares. Most chapters hold several classes sharing
    /// one name - <c>Sql_SeededRow…</c>, <c>Sql_Finder…</c> - so the shared part is the filter, and the
    /// command means what it says. When the names share nothing usable, each one is named outright:
    /// a chapter command that silently runs half the chapter is worse than a long one.
    /// </summary>
    private static string ChapterFilter(Chapter chapter)
    {
        List<string> containers = chapter.Tests
            .Select(ContainerOf)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (containers.Count == 1)
        {
            return $"FullyQualifiedName~{containers[0]}";
        }

        string? shared = SharedPrefix(containers);
        return shared is not null
            ? $"FullyQualifiedName~{shared}"
            : string.Join('|', containers.Select(name => $"FullyQualifiedName~{name}"));
    }

    private static string ContainerOf(ChapterTest test)
    {
        int lastDot = test.FullyQualifiedName.LastIndexOf('.');
        return lastDot < 0 ? test.FullyQualifiedName : test.FullyQualifiedName[..lastDot];
    }

    /// <summary>
    /// The part every class name starts with, cut at a boundary a name can actually be cut at, or
    /// <c>null</c> when that would leave nothing but the namespace - which would run the whole lane.
    /// </summary>
    private static string? SharedPrefix(List<string> names)
    {
        int length = names[0].Length;
        foreach (string name in names.Skip(1))
        {
            int index = 0;
            while (index < length && index < name.Length && names[0][index] == name[index])
            {
                index++;
            }

            length = index;
        }

        // Cutting mid-word would match chapters nobody asked for, so only a separator will do.
        int boundary = names[0][..length].LastIndexOfAny(['.', '_']);
        if (boundary < 0)
        {
            return null;
        }

        string prefix = names[0][..boundary];
        string @namespace = names[0][..(names[0].LastIndexOf('.') + 1)];
        return prefix.Length > @namespace.Length ? prefix : null;
    }

    /// <summary>
    /// Narration lines rewrapped into paragraphs: a bare <c>//doc:</c> separates them, and everything
    /// between two of those becomes one paragraph regardless of where the comment happened to wrap.
    /// </summary>
    /// <remarks>
    /// A line that opens a markdown block - a list item, a table row, a quote, a heading - keeps its own
    /// line instead, because joining it to the line above would turn a list into a sentence with dashes
    /// in it. One such line is one such item: narration has no way to express a continuation, and does
    /// not need one.
    /// </remarks>
    private static string Paragraphs(IReadOnlyList<string> lines)
    {
        List<string> paragraphs = [];
        StringBuilder current = new();

        void Flush()
        {
            if (current.Length > 0)
            {
                paragraphs.Add(current.ToString());
                current.Clear();
            }
        }

        foreach (string line in lines)
        {
            if (line.Length == 0)
            {
                Flush();
                continue;
            }

            if (current.Length > 0)
            {
                current.Append(StartsMarkdownBlock(line) ? '\n' : ' ');
            }

            current.Append(line);
        }

        Flush();

        return string.Join("\n\n", paragraphs);
    }

    private static bool StartsMarkdownBlock(string line) =>
        line.StartsWith("- ", StringComparison.Ordinal)
        || line.StartsWith("* ", StringComparison.Ordinal)
        || line.StartsWith("> ", StringComparison.Ordinal)
        || line.StartsWith("| ", StringComparison.Ordinal)
        || line.StartsWith("#", StringComparison.Ordinal)
        || OrderedListItem.IsMatch(line);

    private static readonly Regex OrderedListItem = new(@"^[0-9]{1,2}\. ", RegexOptions.Compiled);

    public static string LaneSlug(string lane) =>
        lane.Replace("TestFramework.Showroom.", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
