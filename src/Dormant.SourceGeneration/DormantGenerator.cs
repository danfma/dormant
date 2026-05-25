using System;
using System.Collections.Generic;
using System.Linq;
using Dormant.SourceGeneration.Diagnostics;
using Dormant.SourceGeneration.Emit;
using Dormant.SourceGeneration.Parsing;
using Dormant.SourceGeneration.Schema;
using Microsoft.CodeAnalysis;

namespace Dormant.SourceGeneration;

/// <summary>
/// Incremental source generator for the DormantQL DSL. Rooted at <c>AdditionalTextsProvider</c> so it
/// only runs when DormantQL files change (research §5). v1 compiles schema files (<c>.dqls</c>) into
/// strongly-typed partial entity types; query files (<c>.dql</c>) are compiled in later stories.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DormantGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var schemas = context.AdditionalTextsProvider
            .Where(static text => text.Path.EndsWith(".dqls", StringComparison.Ordinal))
            .Select(static (text, cancellationToken) =>
                new DslFile(text.Path, text.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .WithTrackingName(TrackingNames.LoadDslFiles)
            .Select(static (file, _) => BuildSchema(file))
            .WithTrackingName(TrackingNames.ParseSchema);

        context.RegisterSourceOutput(schemas, static (productionContext, schema) =>
        {
            foreach (var diagnostic in schema.Diagnostics)
            {
                productionContext.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (!schema.IsValid)
            {
                return;
            }

            foreach (var entity in schema.Entities)
            {
                productionContext.AddSource(
                    Naming.HintName(schema.ModuleName, entity.Name),
                    EntityEmitter.Emit(schema.ModuleName, entity));
            }
        });
    }

    private static SchemaModel BuildSchema(DslFile file)
    {
        var parse = SchemaParser.Parse(file.Path, file.Text);
        var diagnostics = new List<DiagnosticInfo>(parse.Diagnostics);
        diagnostics.AddRange(SchemaValidator.Validate(parse.Entities));

        return new SchemaModel(
            parse.ModuleName,
            new EquatableArray<EntityModel>([.. parse.Entities]),
            new EquatableArray<DiagnosticInfo>([.. diagnostics]));
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

    /// <summary>The step that parses + validates a schema file.</summary>
    public const string ParseSchema = "ParseSchema";
}
