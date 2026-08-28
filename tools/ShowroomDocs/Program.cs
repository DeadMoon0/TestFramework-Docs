using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ShowroomDocs;

/// <summary>
/// Generates the site's Examples section from the Showroom chapters.
/// </summary>
/// <remarks>
/// Usage: <c>ShowroomDocs --showroom &lt;path&gt; --out &lt;directory&gt; [--captured &lt;json&gt;]</c>
/// </remarks>
internal static class Program
{
    public static int Main(string[] arguments)
    {
        Dictionary<string, string> options = ParseArguments(arguments);

        if (!options.TryGetValue("showroom", out string? showroom))
        {
            Console.Error.WriteLine("Usage: ShowroomDocs --showroom <path> --out <directory> [--captured <json>]");
            Console.Error.WriteLine("       ShowroomDocs --showroom <path> --out <dir> [--captured <json>] [--measured <json>]");
            Console.Error.WriteLine("       ShowroomDocs --showroom <path> --list-lanes");
            return 2;
        }

        if (!Directory.Exists(showroom))
        {
            Console.Error.WriteLine($"Showroom not found at {showroom}.");
            return 2;
        }

        // The pipeline captures one lane per job, and asks here which lanes exist rather than globbing
        // directories itself - so what counts as a lane or a chapter is defined in exactly one place.
        if (options.ContainsKey("list-lanes"))
        {
            Console.WriteLine(DescribeLanes(showroom));
            return 0;
        }

        if (!options.TryGetValue("out", out string? output))
        {
            Console.Error.WriteLine("Usage: ShowroomDocs --showroom <path> --out <directory> [--captured <json>]");
            return 2;
        }

        options.TryGetValue("captured", out string? capturedPath);
        options.TryGetValue("measured", out string? measuredPath);

        // Narration is required by default: a chapter without it would publish as a code dump. The
        // switch exists for the stretch while chapters are still being written, so the site can be
        // built and reviewed before all of them are narrated.
        bool allowMissingNarration = options.TryGetValue("allow-missing-narration", out string? allow)
            && bool.TryParse(allow, out bool parsed) && parsed;
        CapturedOutput captured = CapturedOutput.Load(capturedPath);

        // Timings, if this run has any. Without them the panels simply show no duration.
        if (!string.IsNullOrWhiteSpace(measuredPath) && File.Exists(measuredPath))
        {
            captured.ApplyMeasurements(CapturedOutput.Load(measuredPath));
        }

        string? commit = CurrentCommit(showroom);
        if (captured.IsStaleFor(commit))
        {
            Console.Error.WriteLine(
                $"warning: output was captured from Showroom commit {captured.CapturedFromCommit} " +
                $"but {commit} is being documented. Re-run Capture-ShowroomOutput.ps1.");
        }

        // Regenerate from scratch: a chapter that was renamed must not leave its old page behind.
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        Directory.CreateDirectory(output);

        PageWriter writer = new(captured);
        List<Chapter> chapters = [];
        List<string> failures = [];

        foreach (string laneDirectory in Directory.GetDirectories(showroom, "TestFramework.Showroom.*").OrderBy(path => path))
        {
            string lane = Path.GetFileName(laneDirectory);
            string project = $"{lane}/{lane}.csproj";

            foreach (string file in Directory.GetFiles(laneDirectory, "*.cs").OrderBy(path => path))
            {
                if (!ChapterParser.IsChapterFile(file))
                {
                    // Gates, base classes and shared fixtures are infrastructure, not chapters.
                    continue;
                }

                try
                {
                    Chapter chapter = ChapterParser.Parse(file, lane, project);
                    writer.WriteChapter(chapter, output);
                    chapters.Add(chapter);
                }
                catch (ChapterNarrationMissingException exception)
                {
                    failures.Add(exception.Message);
                }
            }
        }

        if (chapters.Count == 0 && failures.Count == 0)
        {
            Console.Error.WriteLine($"No chapter files found under {showroom}.");
            return 1;
        }

        // A lane that produces nothing is the failure this check exists for, and it is invisible without
        // it: the UI lane was discovered, skipped for carrying no narration, and left the site with no UI
        // pages at all - for months, because a missing section looks exactly like a section nobody wrote.
        // Counting discovered lanes against lanes that produced pages is what turns that into a sentence.
        foreach (string silent in SilentLanes(showroom, chapters))
        {
            failures.Add($"lane '{silent}' was discovered but produced no pages.");
        }

        WriteLandingPage(chapters, output, captured);
        WriteTableOfContents(chapters, output);

        Console.WriteLine($"{chapters.Count} chapters, {chapters.Sum(chapter => chapter.Tests.Count)} tests.");

        if (failures.Count > 0)
        {
            Console.Error.WriteLine();
            string severity = allowMissingNarration ? "warning" : "error";
            foreach (string failure in failures)
            {
                Console.Error.WriteLine($"{severity}: {failure}");
            }

            Console.Error.WriteLine(
                $"{severity}: {failures.Count} problem(s) would leave the site incomplete.");

            return allowMissingNarration ? 0 : 1;
        }

        return 0;
    }

    /// <summary>
    /// Lanes the generator found on disk that contributed no page.
    /// </summary>
    /// <remarks>
    /// Discovery and output are two different things, and only the second one is visible on the site. A
    /// lane whose chapters are all unnarrated is discovered, skipped one chapter at a time, and then
    /// absent - which reads as "nobody has written that lane yet" rather than as a fault.
    /// </remarks>
    /// <param name="showroom">The Showroom checkout.</param>
    /// <param name="chapters">The chapters that will be written.</param>
    /// <returns>The lane names that produced nothing.</returns>
    private static IEnumerable<string> SilentLanes(string showroom, IReadOnlyCollection<Chapter> chapters)
    {
        HashSet<string> produced = [.. chapters.Select(chapter => chapter.Lane)];

        foreach (string laneDirectory in Directory.GetDirectories(showroom, "TestFramework.Showroom.*").OrderBy(path => path))
        {
            string lane = Path.GetFileName(laneDirectory);

            // Asked of the same predicate the walk above uses, so "has chapters" means here exactly what
            // it means there. Counting .cs files instead would report a lane holding only a gate, and a
            // check that cries wolf is one a reader learns to skip.
            if (!produced.Contains(lane)
                && Directory.GetFiles(laneDirectory, "*.cs").Any(ChapterParser.IsChapterFile))
            {
                yield return lane;
            }
        }
    }

    /// <summary>
    /// The lanes and their chapter counts, as JSON, for a pipeline matrix to consume.
    /// </summary>
    private static string DescribeLanes(string showroom)
    {
        List<string> entries = [];

        foreach (string laneDirectory in Directory.GetDirectories(showroom, "TestFramework.Showroom.*").OrderBy(path => path))
        {
            string lane = Path.GetFileName(laneDirectory);
            int chapters = Directory.GetFiles(laneDirectory, "*.cs").Count(ChapterParser.IsChapterFile);

            if (chapters == 0)
            {
                continue;
            }

            entries.Add($"{{\"lane\":\"{lane}\",\"chapters\":{chapters}}}");
        }

        return "[" + string.Join(",", entries) + "]";
    }

    private static void WriteLandingPage(List<Chapter> chapters, string output, CapturedOutput captured)
    {
        StringBuilder page = new();
        page.AppendLine("# Examples");
        page.AppendLine();
        page.AppendLine("Each chapter below is a real test you can run. The pages are generated from the");
        page.AppendLine("[Showroom](https://github.com/DeadMoon0/TestFramework-Showroom) sources, so the code on a page is");
        page.AppendLine("the code that runs, and the output panels are what it actually printed.");
        page.AppendLine();
        page.AppendLine("## Running them yourself");
        page.AppendLine();
        page.AppendLine("```bash");
        page.AppendLine("git clone https://github.com/DeadMoon0/TestFramework-Showroom.git");
        page.AppendLine("cd TestFramework-Showroom");
        page.AppendLine("dotnet test TestFramework.Showroom.Basic/TestFramework.Showroom.Basic.csproj -c Release");
        page.AppendLine("```");
        page.AppendLine();
        page.AppendLine("The `--filter` arguments on each page are VSTest syntax, which is what the Showroom's xunit 2.x");
        page.AppendLine("runner uses. Chapters marked as needing Docker skip themselves, with a reason, when no daemon");
        page.AppendLine("answers - so running a lane on a machine without Docker is safe, not a wall of red.");
        page.AppendLine();

        if (!string.IsNullOrWhiteSpace(captured.CapturedAt))
        {
            page.AppendLine("## About the numbers");
            page.AppendLine();
            page.AppendLine($"Output panels were captured on {captured.CapturedAt} from Showroom commit");
            page.AppendLine($"`{captured.CapturedFromCommit}`" +
                (captured.CapturedIn is null ? "." : $", on {captured.CapturedIn.Describe()}."));
            page.AppendLine();
            page.AppendLine("Each panel carries the exact duration its test took, because a real number is more useful than");
            page.AppendLine("a vague one - but read it for what it is. It was measured once, on that machine, with whatever");
            page.AppendLine("else was running on it. A container-backed chapter in particular spends most of its time waiting");
            page.AppendLine("for infrastructure that your machine will start at a different speed. The figures tell you which");
            page.AppendLine("chapters are cheap and which are not; they are not a performance guarantee, and they are not a");
            page.AppendLine("benchmark of the framework.");
            page.AppendLine();
        }

        foreach (IGrouping<string, Chapter> lane in chapters.GroupBy(chapter => chapter.Lane))
        {
            page.AppendLine($"## {LaneTitle(lane.Key)}");
            page.AppendLine();
            page.AppendLine("| Chapter | Tests | Needs |");
            page.AppendLine("|---|---|---|");

            foreach (Chapter chapter in lane)
            {
                string needs = Needs(chapter);

                page.AppendLine(
                    $"| [{chapter.Number} - {chapter.Title}]({PageWriter.LaneSlug(chapter.Lane)}/{chapter.Slug}.md) " +
                    $"| {chapter.Tests.Count} | {needs} |");
            }

            page.AppendLine();
        }

        File.WriteAllText(Path.Combine(output, "index.md"), page.ToString());
    }

    /// <summary>
    /// The overview table's "Needs" cell. It has to agree with the badge on the chapter's own page, so a
    /// reader scanning the table and a reader opening the chapter are told the same thing.
    /// </summary>
    private static string Needs(Chapter chapter)
    {
        List<string> needs = [];

        if (chapter.Tests.Any(test => test.Prerequisite == Prerequisite.NeedsDocker))
        {
            needs.Add("Docker");
        }

        if (chapter.Tests.Any(test => test.Prerequisite == Prerequisite.NeedsWindows))
        {
            needs.Add("Windows");
        }

        if (chapter.Tests.Count > 0 && chapter.Tests.All(test => test.SkipReason is not null))
        {
            needs.Add("an edit to run");
        }

        return needs.Count == 0 ? "nothing" : string.Join(", ", needs);
    }

    private static void WriteTableOfContents(List<Chapter> chapters, string output)
    {
        StringBuilder toc = new();
        toc.AppendLine("- name: Overview");
        toc.AppendLine("  href: index.md");

        foreach (IGrouping<string, Chapter> lane in chapters.GroupBy(chapter => chapter.Lane))
        {
            toc.AppendLine($"- name: {LaneTitle(lane.Key)}");
            toc.AppendLine("  items:");

            foreach (Chapter chapter in lane)
            {
                toc.AppendLine($"  - name: {chapter.Number} {chapter.Title}");
                toc.AppendLine($"    href: {PageWriter.LaneSlug(chapter.Lane)}/{chapter.Slug}.md");
            }
        }

        File.WriteAllText(Path.Combine(output, "toc.yml"), toc.ToString());
    }

    private static string LaneTitle(string lane) => lane switch
    {
        "TestFramework.Showroom.Basic" => "Basics",
        "TestFramework.Showroom.Web" => "Web",
        "TestFramework.Showroom.Azure" => "Azure",
        "TestFramework.Showroom.UI" => "UI",
        _ => lane,
    };

    /// <summary>The Showroom commit being documented, so a stale capture can be reported.</summary>
    private static string? CurrentCommit(string showroom)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
            {
                WorkingDirectory = showroom,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process is null)
            {
                return null;
            }

            string commit = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 ? commit : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] arguments)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < arguments.Length; index++)
        {
            if (!arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string name = arguments[index][2..];
            bool hasValue = index + 1 < arguments.Length
                && !arguments[index + 1].StartsWith("--", StringComparison.Ordinal);

            options[name] = hasValue ? arguments[index + 1] : "true";
            if (hasValue) { index++; }
        }

        return options;
    }
}
