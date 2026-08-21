using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShowroomDocs;

/// <summary>What one test printed when it last ran.</summary>
internal sealed class TestOutcome
{
    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = "Unknown";

    /// <summary>
    /// The exact measured duration. Volatile by design - see <see cref="CaptureEnvironment"/> for why a
    /// figure is only publishable together with the machine that produced it.
    /// </summary>
    [JsonPropertyName("durationMs")]
    public double? DurationMs { get; set; }

    [JsonPropertyName("skipReason")]
    public string? SkipReason { get; set; }

    [JsonPropertyName("stdout")]
    public string? StandardOutput { get; set; }
}

/// <summary>
/// Where a capture was taken. A duration means one thing on a CI runner and another on a workstation,
/// so the pages state which.
/// </summary>
internal sealed class CaptureEnvironment
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("os")]
    public string? Os { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("processors")]
    public int? Processors { get; set; }

    /// <summary>The machine, named as briefly as it can be while still being identifiable.</summary>
    public string Describe()
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(Image)) { parts.Add(Image); }
        else if (!string.IsNullOrWhiteSpace(Os)) { parts.Add(Os); }
        if (Processors is > 0) { parts.Add($"{Processors} logical cores"); }

        return parts.Count == 0 ? "an unrecorded machine" : string.Join(", ", parts);
    }

    /// <summary>
    /// The caption printed under a timing. A CI figure and a workstation figure answer different
    /// questions, so they are not allowed to read the same.
    /// </summary>
    public string Caption()
    {
        bool isPipeline = Kind is not null
            && Kind.Contains("Actions", StringComparison.OrdinalIgnoreCase);

        return isPipeline
            ? $"Measured in the documentation pipeline on {Describe()}. That answers \"does this still run\", "
              + "not \"how fast is this on your machine\"."
            : $"Measured on one workstation ({Describe()}), not in the pipeline. Treat it as an order of "
              + "magnitude rather than a benchmark.";
    }
}

/// <summary>
/// The captured test output the chapter pages show, as written by <c>Capture-ShowroomOutput.ps1</c>.
/// </summary>
/// <remarks>
/// This file is committed on purpose. Capturing needs a Docker daemon and a restorable package feed;
/// building the site should need neither, so the capture is a separate deliberate step and its result
/// travels with the repository.
/// </remarks>
internal sealed class CapturedOutput
{
    [JsonPropertyName("capturedFromCommit")]
    public string? CapturedFromCommit { get; set; }

    [JsonPropertyName("capturedAt")]
    public string? CapturedAt { get; set; }

    [JsonPropertyName("capturedIn")]
    public CaptureEnvironment? CapturedIn { get; set; }

    [JsonPropertyName("tests")]
    public Dictionary<string, TestOutcome> Tests { get; set; } = [];

    public static CapturedOutput Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new CapturedOutput();
        }

        CapturedOutput? loaded = JsonSerializer.Deserialize<CapturedOutput>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return loaded ?? new CapturedOutput();
    }

    public TestOutcome? For(string fullyQualifiedName) =>
        Tests.TryGetValue(fullyQualifiedName, out TestOutcome? outcome) ? outcome : null;

    /// <summary>
    /// Overlays the timings a pipeline run measured onto the committed content.
    /// </summary>
    /// <remarks>
    /// The two are stored apart on purpose: content is committed so any clone can render a panel, while
    /// a duration is true of one run on one machine and is never committed. A build with no measurements
    /// file therefore shows no timings, which is the honest outcome - it measured none.
    /// </remarks>
    public void ApplyMeasurements(CapturedOutput measurements)
    {
        ArgumentNullException.ThrowIfNull(measurements);

        CapturedAt = measurements.CapturedAt;
        CapturedIn = measurements.CapturedIn;

        foreach ((string name, TestOutcome measured) in measurements.Tests)
        {
            if (Tests.TryGetValue(name, out TestOutcome? outcome))
            {
                outcome.DurationMs = measured.DurationMs;
            }
        }
    }

    /// <summary>
    /// True when the capture was taken from a different Showroom commit than the one being
    /// documented - which means at least one panel may be showing output from code that has changed.
    /// </summary>
    public bool IsStaleFor(string? currentCommit) =>
        !string.IsNullOrWhiteSpace(CapturedFromCommit)
        && !string.IsNullOrWhiteSpace(currentCommit)
        && !string.Equals(CapturedFromCommit, currentCommit, StringComparison.OrdinalIgnoreCase);
}
