using System.Collections.Generic;

namespace ShowroomDocs;

/// <summary>
/// One block of a chapter page: either narration lifted from <c>//doc:</c> comments, or the code
/// that sits between two narration blocks.
/// </summary>
internal sealed class ChapterBlock
{
    public required BlockKind Kind { get; init; }

    /// <summary>Narration paragraphs, or code lines, depending on <see cref="Kind"/>.</summary>
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>
    /// Tests whose declaration ends inside this code block. Their output panels are rendered
    /// directly beneath it, which is what makes a chapter read as run-it-and-see rather than as a
    /// listing followed by a log.
    /// </summary>
    public IReadOnlyList<ChapterTest> ClosingTests { get; init; } = [];
}

internal enum BlockKind
{
    Narration,
    Code,
}

/// <summary>A test method inside a chapter, and what a reader needs in order to run just that one.</summary>
internal sealed class ChapterTest
{
    public required string Name { get; init; }

    /// <summary>The xunit fully qualified name, which is also the <c>--filter</c> argument.</summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>
    /// What the test needs from the machine: a plain <c>[Fact]</c> runs anywhere, <c>[DockerFact]</c>
    /// skips itself with a reason when no daemon answers, and <c>[SupportedOSPlatform("windows")]</c>
    /// narrows it to one operating system.
    /// </summary>
    /// <remarks>
    /// A platform gate is read from the attribute rather than guessed at from the code, and it outranks
    /// the test attribute: "runs anywhere" is a promise, and a reader on Linux who takes it at face value
    /// and then cannot run the chapter has been told something false by the page.
    /// </remarks>
    public required Prerequisite Prerequisite { get; init; }

    /// <summary>
    /// The reason a test skips itself unconditionally, from <c>[Fact(Skip = "…")]</c>, or
    /// <see langword="null"/> when it runs.
    /// </summary>
    /// <remarks>
    /// Distinct from a <c>[DockerFact]</c> skip, which depends on the machine. This one is a decision the
    /// chapter made, so the page states it up front instead of leaving a reader to discover it from an
    /// output panel.
    /// </remarks>
    public string? SkipReason { get; init; }

    /// <summary>xunit traits, so a reader can find the lane-wide filter a chapter belongs to.</summary>
    public IReadOnlyList<string> Traits { get; init; } = [];

    /// <summary>The last line of the method declaration, used to place the output panel.</summary>
    public required int EndLine { get; init; }
}

internal enum Prerequisite
{
    RunsAnywhere,
    NeedsDocker,
    NeedsWindows,
    Unknown,
}

/// <summary>A chapter file, parsed into the blocks its page is built from.</summary>
internal sealed class Chapter
{
    public required string Lane { get; init; }

    /// <summary>The project file the lane's tests are run through.</summary>
    public required string LaneProject { get; init; }

    /// <summary>The chapter's own number or code: <c>01</c>, <c>W5</c>, <c>A4</c>.</summary>
    public required string Number { get; init; }

    /// <summary>Chapter title, derived from the file name so the site inherits the Showroom's order.</summary>
    public required string Title { get; init; }

    public required string FileName { get; init; }

    /// <summary>Slug used for the generated page and its snippet file.</summary>
    public required string Slug { get; init; }

    public required IReadOnlyList<ChapterBlock> Blocks { get; init; }

    public required IReadOnlyList<ChapterTest> Tests { get; init; }
}
