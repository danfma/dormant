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
/// strongly-typed partial entity types in a .NET-friendly namespace (FR-046); query files (<c>.dql</c>)
/// are compiled in later stories.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DormantGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var config = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace);
                provider.GlobalOptions.TryGetValue("build_property.ProjectDir", out var projectDir);
                return new GeneratorConfig(rootNamespace, projectDir);
            })
            .WithTrackingName(TrackingNames.Config);

        var schemas = context.AdditionalTextsProvider
            .Where(static text => text.Path.EndsWith(".dqls", StringComparison.Ordinal))
            .Select(static (text, cancellationToken) =>
                new DslFile(text.Path, text.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .WithTrackingName(TrackingNames.LoadDslFiles)
            .Select(static (file, _) => BuildSchema(file))
            .WithTrackingName(TrackingNames.ParseSchema);

        context.RegisterSourceOutput(schemas.Combine(config), static (productionContext, pair) =>
        {
            var (schema, generatorConfig) = pair;

            foreach (var diagnostic in schema.Diagnostics)
            {
                productionContext.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (!schema.IsValid)
            {
                return;
            }

            var @namespace = Naming.ComputeNamespace(
                generatorConfig.RootNamespace,
                generatorConfig.ProjectDir,
                schema.FilePath,
                schema.ModuleName);

            foreach (var entity in schema.Entities)
            {
                productionContext.AddSource(
                    Naming.HintName(@namespace, entity.Name),
                    EntityEmitter.Emit(@namespace, entity));

                productionContext.AddSource(
                    Naming.HintName(@namespace, entity.Name + ".Materialization"),
                    MaterializerEmitter.Emit(@namespace, entity));
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
            file.Path,
            new EquatableArray<EntityModel>([.. parse.Entities]),
            new EquatableArray<DiagnosticInfo>([.. diagnostics]));
    }
}

/// <summary>An equatable model of one loaded DormantQL file (path + content).</summary>
/// <param name="Path">The file path.</param>
/// <param name="Text">The file contents.</param>
internal readonly record struct DslFile(string Path, string Text);

/// <summary>Equatable build configuration projected from analyzer config options (FR-046).</summary>
/// <param name="RootNamespace">The consuming project's root namespace, if known.</param>
/// <param name="ProjectDir">The consuming project's directory, if known.</param>
internal readonly record struct GeneratorConfig(string? RootNamespace, string? ProjectDir);

/// <summary>Stable pipeline step names used by cacheability tests (research §8).</summary>
internal static class TrackingNames
{
    /// <summary>The step that loads DormantQL files from additional texts.</summary>
    public const string LoadDslFiles = "LoadDslFiles";

    /// <summary>The step that parses + validates a schema file.</summary>
    public const string ParseSchema = "ParseSchema";

    /// <summary>The step that projects build configuration (root namespace, project dir).</summary>
    public const string Config = "Config";
}
