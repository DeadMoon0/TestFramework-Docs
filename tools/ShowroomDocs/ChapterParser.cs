using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ShowroomDocs;

/// <summary>
/// Reads one chapter file and splits it into the alternating narration and code blocks its page is
/// made of.
/// </summary>
/// <remarks>
/// <para>
/// The page order is never configured anywhere: it is the order things appear in the file. A run of
/// <c>//doc:</c> lines is prose, everything between two such runs is code, and a bare <c>//doc:</c>
/// ends a paragraph. Ordinary <c>//</c> comments stay in the code, because they are commentary on
/// the code rather than commentary on the chapter.
/// </para>
/// <para>
/// Only the *syntax* is parsed - no compilation, no restore. That keeps documenting the Showroom
/// independent of being able to build it.
/// </para>
/// </remarks>
internal static class ChapterParser
{
    private const string NarrationPrefix = "//doc:";
    private const string HideStart = "//doc:hide-start";
    private const string HideEnd = "//doc:hide-end";

    /// <summary>Chapter files carry the Showroom's own numbering; anything else is infrastructure.</summary>
    private static readonly Regex ChapterFileName = new(@"^(?<number>[0-9]{1,2}|[WA][0-9]{1,2})_(?<name>[A-Za-z0-9]+)\.cs$", RegexOptions.Compiled);

    public static bool IsChapterFile(string path) => ChapterFileName.IsMatch(Path.GetFileName(path));

    public static Chapter Parse(string path, string lane, string laneProject)
    {
        string fileName = Path.GetFileName(path);
        Match name = ChapterFileName.Match(fileName);
        if (!name.Success)
        {
            throw new ArgumentException($"{fileName} is not a chapter file.", nameof(path));
        }

        string[] lines = File.ReadAllLines(path);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(string.Join('\n', lines), path: path);
        CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();

        List<ChapterTest> tests = FindTests(root);
        List<ChapterBlock> blocks = SplitIntoBlocks(lines, tests, DroppedLines(root, lines.Length));

        if (!blocks.Any(block => block.Kind == BlockKind.Narration))
        {
            // The forcing function: a chapter with no narration is a code dump, and the build says so
            // rather than publishing one.
            throw new ChapterNarrationMissingException(path);
        }

        string number = name.Groups["number"].Value;
        return new Chapter
        {
            Lane = lane,
            LaneProject = laneProject,
            Number = number,
            Title = Humanise(name.Groups["name"].Value),
            FileName = fileName,
            Slug = $"{number}-{Slugify(name.Groups["name"].Value)}".ToLowerInvariant(),
            Blocks = blocks,
            Tests = tests,
        };
    }

    /// <summary>
    /// Every test method, with what it needs from the machine. The attribute is the source of that:
    /// a plain <c>[Fact]</c> runs anywhere, and the Showroom's own <c>[DockerFact]</c> skips itself
    /// with a reason when no daemon answers.
    /// </summary>
    private static List<ChapterTest> FindTests(CompilationUnitSyntax root)
    {
        List<ChapterTest> tests = [];

        foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            List<AttributeSyntax> attributes = method.AttributeLists
                .SelectMany(list => list.Attributes)
                .ToList();

            AttributeSyntax? testAttribute = attributes.FirstOrDefault(attribute =>
            {
                string attributeName = AttributeName(attribute);
                return attributeName.EndsWith("Fact", StringComparison.Ordinal)
                    || attributeName.EndsWith("Theory", StringComparison.Ordinal);
            });

            if (testAttribute is null)
            {
                continue;
            }

            tests.Add(new ChapterTest
            {
                Name = method.Identifier.Text,
                FullyQualifiedName = FullyQualifiedName(method),
                Prerequisite = PrerequisiteOf(AttributeName(testAttribute)),
                Traits = attributes
                    .Where(attribute => AttributeName(attribute) == "Trait")
                    .Select(TraitText)
                    .Where(trait => trait is not null)
                    .Select(trait => trait!)
                    .ToList(),

                // Line numbers are 1-based here, matching how the file is read below.
                EndLine = method.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
            });
        }

        return tests;
    }

    private static string AttributeName(AttributeSyntax attribute)
    {
        string text = attribute.Name.ToString();
        int lastDot = text.LastIndexOf('.');
        if (lastDot >= 0)
        {
            text = text[(lastDot + 1)..];
        }

        return text.EndsWith("Attribute", StringComparison.Ordinal)
            ? text[..^"Attribute".Length]
            : text;
    }

    private static string? TraitText(AttributeSyntax attribute)
    {
        List<string> arguments = attribute.ArgumentList?.Arguments
            .Select(argument => argument.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => literal.Token.ValueText)
            .ToList() ?? [];

        return arguments.Count == 2 ? $"{arguments[0]}={arguments[1]}" : null;
    }

    private static Prerequisite PrerequisiteOf(string attributeName) => attributeName switch
    {
        "Fact" or "Theory" => Prerequisite.RunsAnywhere,
        _ when attributeName.Contains("Docker", StringComparison.OrdinalIgnoreCase) => Prerequisite.NeedsDocker,
        _ => Prerequisite.Unknown,
    };

    private static string FullyQualifiedName(MethodDeclarationSyntax method)
    {
        List<string> typeNames = [];
        for (SyntaxNode? node = method.Parent; node is not null; node = node.Parent)
        {
            if (node is TypeDeclarationSyntax type)
            {
                typeNames.Insert(0, type.Identifier.Text);
            }
            else if (node is BaseNamespaceDeclarationSyntax @namespace)
            {
                typeNames.Insert(0, @namespace.Name.ToString());
            }
        }

        // xunit separates a nested type from its container with '+'.
        string container = typeNames.Count switch
        {
            0 => string.Empty,
            1 => typeNames[0],
            _ => typeNames[0] + "." + string.Join('+', typeNames.Skip(1)),
        };

        return container.Length == 0 ? method.Identifier.Text : $"{container}.{method.Identifier.Text}";
    }

    /// <summary>
    /// Lines the page never shows: usings, the namespace, and the chapter type's own declaration and
    /// closing brace. They are C# ceremony, not part of what the chapter teaches.
    /// </summary>
    private static HashSet<int> DroppedLines(CompilationUnitSyntax root, int lineCount)
    {
        HashSet<int> dropped = [];

        void DropSpan(SyntaxNode node)
        {
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();
            for (int line = span.StartLinePosition.Line + 1; line <= span.EndLinePosition.Line + 1; line++)
            {
                dropped.Add(line);
            }
        }

        foreach (UsingDirectiveSyntax @using in root.Usings)
        {
            DropSpan(@using);
        }

        foreach (BaseNamespaceDeclarationSyntax @namespace in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        {
            if (@namespace is FileScopedNamespaceDeclarationSyntax fileScoped)
            {
                DropSpan(fileScoped.Name);
                dropped.Add(fileScoped.Name.GetLocation().GetLineSpan().EndLinePosition.Line + 1);
            }
        }

        // The outermost type declaration: its header lines, and its closing brace.
        foreach (TypeDeclarationSyntax type in root.Members.OfType<TypeDeclarationSyntax>()
            .Concat(root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().SelectMany(n => n.Members).OfType<TypeDeclarationSyntax>()))
        {
            // A primary constructor is not ceremony: it is where a chapter's dependencies come from,
            // and dropping it would leave the reader wondering what `output` is. Only a bare
            // declaration is dropped.
            FileLinePositionSpan brace = type.OpenBraceToken.GetLocation().GetLineSpan();

            if (type.ParameterList is null)
            {
                FileLinePositionSpan start = type.Identifier.GetLocation().GetLineSpan();
                for (int line = start.StartLinePosition.Line + 1; line <= brace.EndLinePosition.Line + 1; line++)
                {
                    dropped.Add(line);
                }
            }
            else
            {
                // The declaration is kept, but its opening brace would read as a dangling line once
                // the matching closing brace is gone.
                dropped.Add(brace.StartLinePosition.Line + 1);
            }

            dropped.Add(type.CloseBraceToken.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
        }

        dropped.RemoveWhere(line => line < 1 || line > lineCount);
        return dropped;
    }

    private static List<ChapterBlock> SplitIntoBlocks(string[] lines, List<ChapterTest> tests, HashSet<int> dropped)
    {
        List<ChapterBlock> blocks = [];
        List<string> narration = [];
        List<string> code = [];
        List<ChapterTest> closing = [];
        bool hidden = false;

        void FlushNarration()
        {
            if (narration.Count == 0)
            {
                return;
            }

            blocks.Add(new ChapterBlock { Kind = BlockKind.Narration, Lines = [.. narration] });
            narration.Clear();
        }

        void FlushCode()
        {
            List<string> trimmed = Dedent(code);
            code.Clear();

            if (trimmed.Count == 0)
            {
                // No code, but a test may still have closed here; hand its panel to the previous block.
                if (closing.Count > 0 && blocks.Count > 0 && blocks[^1].Kind == BlockKind.Code)
                {
                    ChapterBlock previous = blocks[^1];
                    blocks[^1] = new ChapterBlock
                    {
                        Kind = BlockKind.Code,
                        Lines = previous.Lines,
                        ClosingTests = [.. previous.ClosingTests, .. closing],
                    };
                }

                closing.Clear();
                return;
            }

            blocks.Add(new ChapterBlock { Kind = BlockKind.Code, Lines = trimmed, ClosingTests = [.. closing] });
            closing.Clear();
        }

        for (int index = 0; index < lines.Length; index++)
        {
            int lineNumber = index + 1;
            string line = lines[index];
            string trimmedLine = line.TrimStart();

            if (trimmedLine.StartsWith(HideStart, StringComparison.Ordinal))
            {
                hidden = true;
                continue;
            }

            if (trimmedLine.StartsWith(HideEnd, StringComparison.Ordinal))
            {
                hidden = false;
                continue;
            }

            if (hidden || dropped.Contains(lineNumber))
            {
                continue;
            }

            if (trimmedLine.StartsWith(NarrationPrefix, StringComparison.Ordinal))
            {
                FlushCode();
                string text = trimmedLine[NarrationPrefix.Length..].Trim();

                // A bare '//doc:' is a paragraph break.
                narration.Add(text);
                continue;
            }

            FlushNarration();
            code.Add(line);

            foreach (ChapterTest test in tests.Where(test => test.EndLine == lineNumber))
            {
                closing.Add(test);
            }
        }

        FlushNarration();
        FlushCode();

        return blocks;
    }

    /// <summary>Removes the shared indent, so a member declared inside a class reads as top level.</summary>
    private static List<string> Dedent(IReadOnlyList<string> lines)
    {
        List<string> kept = [.. lines];

        while (kept.Count > 0 && kept[0].Trim().Length == 0)
        {
            kept.RemoveAt(0);
        }

        while (kept.Count > 0 && kept[^1].Trim().Length == 0)
        {
            kept.RemoveAt(kept.Count - 1);
        }

        if (kept.Count == 0)
        {
            return kept;
        }

        int indent = kept
            .Where(line => line.Trim().Length > 0)
            .Select(line => line.Length - line.TrimStart().Length)
            .DefaultIfEmpty(0)
            .Min();

        return kept
            .Select(line => line.Length >= indent ? line[indent..] : line.TrimStart())
            .ToList();
    }

    private static string Humanise(string name)
    {
        string spaced = Regex.Replace(name, "(?<!^)([A-Z])", " $1");
        return char.ToUpperInvariant(spaced[0]) + spaced[1..].ToLowerInvariant();
    }

    private static string Slugify(string name) => Regex.Replace(name, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
}

/// <summary>Thrown when a chapter carries no <c>//doc:</c> narration at all.</summary>
internal sealed class ChapterNarrationMissingException(string path)
    : Exception($"{System.IO.Path.GetFileName(path)} has no //doc: narration, so its page would be a code dump.")
{
    public string ChapterPath { get; } = path;
}
