using System;
using Microsoft.CodeAnalysis;

namespace Dormant.SourceGeneration;

/// <summary>
/// Incremental source generator for the DormantQL DSL. Rooted at <c>AdditionalTextsProvider</c> so it
/// only runs when DormantQL files change (research §5); parsing and emission (partial entities,
/// projections, snapshots, typed query methods + build-time SQL) land in the user-story phases.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DormantGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var dslFiles = context.AdditionalTextsProvider
            .Where(static text =>
                text.Path.EndsWith(".dqls", StringComparison.Ordinal) ||
                text.Path.EndsWith(".dql", StringComparison.Ordinal))
            .Select(static (text, cancellationToken) =>
                new DslFile(text.Path, text.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .WithTrackingName(TrackingNames.LoadDslFiles);

        // Emission is wired in US1+ (schema -> partial entities, queries -> typed methods + SQL).
        context.RegisterSourceOutput(dslFiles, static (_, _) => { });
    }
}

/// <summary>An equatable model of one loaded DormantQL file (path + content).</summary>
/// <param name="Path">The file path.</param>
/// <param name="Text">The file contents.</param>
internal readonly record struct DslFile(string Path, string Text);

/// <summary>Stable pipeline step names used by cacheability tests (research §8).</summary>
internal static class TrackingNames
{
    /// <summary>The step that loads DormantQL files from additional texts.</summary>
    public const string LoadDslFiles = "LoadDslFiles";
}
