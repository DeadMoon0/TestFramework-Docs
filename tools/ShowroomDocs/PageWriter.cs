using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
        page.AppendLine(Badges(chapter));
        page.AppendLine();
        page.AppendLine("Run the whole chapter:");
        page.AppendLine();
        page.AppendLine("```bash");
        page.AppendLine(ChapterCommand(chapter));
        page.AppendLine("```");
        page.AppendLine();

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

    private static string Badges(Chapter chapter)
    {
        List<string> badges = [];

        bool needsDocker = chapter.Tests.Any(test => test.Prerequisite == Prerequisite.NeedsDocker);
        badges.Add(needsDocker
            ? "<span class=\"tf-badge tf-badge-warn\">needs a Docker daemon</span>"
            : "<span class=\"tf-badge tf-badge-ok\">runs anywhere</span>");

        badges.Add($"<span class=\"tf-badge\">{chapter.Tests.Count} test{(chapter.Tests.Count == 1 ? string.Empty : "s")}</span>");

        foreach (string trait in chapter.Tests.SelectMany(test => test.Traits).Distinct())
        {
            badges.Add($"<span class=\"tf-badge\">{trait}</span>");
        }

        return $"<div class=\"tf-badges\">{string.Join(string.Empty, badges)}</div>";
    }

    private static string ChapterCommand(Chapter chapter) =>
        $"dotnet test {chapter.LaneProject} -c Release --filter \"FullyQualifiedName~{TypeName(chapter)}\"";

    private static string TestCommand(Chapter chapter, ChapterTest test) =>
        $"dotnet test {chapter.LaneProject} -c Release --filter \"FullyQualifiedName={test.FullyQualifiedName}\"";

    private static string TypeName(Chapter chapter)
    {
        string? name = chapter.Tests.Select(test => test.FullyQualifiedName).FirstOrDefault();
        if (name is null)
        {
            return chapter.Title.Replace(" ", string.Empty);
        }

        int lastDot = name.LastIndexOf('.');
        return lastDot < 0 ? name : name[..lastDot];
    }

    private static string Paragraphs(IReadOnlyList<string> lines)
    {
        List<string> paragraphs = [];
        List<string> current = [];

        foreach (string line in lines)
        {
            if (line.Length == 0)
            {
                if (current.Count > 0)
                {
                    paragraphs.Add(string.Join(' ', current));
                    current.Clear();
                }

                continue;
            }

            current.Add(line);
        }

        if (current.Count > 0)
        {
            paragraphs.Add(string.Join(' ', current));
        }

        return string.Join("\n\n", paragraphs);
    }

    public static string LaneSlug(string lane) =>
        lane.Replace("TestFramework.Showroom.", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
