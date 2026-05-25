using System;
using System.Collections.Generic;
using System.Linq;
using Dormant.SourceGeneration.Diagnostics;
using Dormant.SourceGeneration.Emit;
using Dormant.SourceGeneration.Parsing;
using Dormant.SourceGeneration.Query;
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
                provider.GlobalOptions.TryGetValue("build_property.DormantNamingConvention", out var naming);
                return new GeneratorConfig(rootNamespace, projectDir, NamingConventions.Parse(naming));
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

            // The module maps to a database schema (FR-045); its name follows the active convention.
            var schemaName = NamingConventions.Resolve(schema.ModuleName, null, generatorConfig.Naming);

            foreach (var entity in schema.Entities)
            {
                foreach (var collision in NameResolution.FindColumnCollisions(entity, generatorConfig.Naming))
                {
                    productionContext.ReportDiagnostic(collision.ToDiagnostic());
                }

                productionContext.AddSource(
                    Naming.HintName(@namespace, entity.Name),
                    EntityEmitter.Emit(@namespace, entity));

                productionContext.AddSource(
                    Naming.HintName(@namespace, entity.Name + ".Binding"),
                    EntityBindingEmitter.Emit(@namespace, entity, schemaName, generatorConfig.Naming));
            }
        });

        // Query files (.dql) compile to ISession extension methods carrying build-time SQL (US3). They
        // resolve their selected entity against the full set of parsed schemas (combined here).
        var queries = context.AdditionalTextsProvider
            .Where(static text => text.Path.EndsWith(".dql", StringComparison.Ordinal))
            .Select(static (text, cancellationToken) =>
                new DslFile(text.Path, text.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .WithTrackingName(TrackingNames.LoadQueryFiles)
            .Select(static (file, _) => BuildQueries(file))
            .WithTrackingName(TrackingNames.ParseQueries);

        context.RegisterSourceOutput(
            queries.Combine(schemas.Collect()).Combine(config),
            static (productionContext, pair) =>
            {
                var ((queryFile, allSchemas), generatorConfig) = pair;

                foreach (var diagnostic in queryFile.Diagnostics)
                {
                    productionContext.ReportDiagnostic(diagnostic.ToDiagnostic());
                }

                if (!queryFile.IsValid)
                {
                    return;
                }

                // Map each entity to its model + the database schema (module) it belongs to, so query SQL
                // can schema-qualify the table (FR-045).
                var entities = new Dictionary<string, (EntityModel Entity, string Schema)>(StringComparer.Ordinal);
                foreach (var schema in allSchemas)
                {
                    var schemaName = NamingConventions.Resolve(schema.ModuleName, null, generatorConfig.Naming);
                    foreach (var entity in schema.Entities)
                    {
                        entities[entity.Name] = (entity, schemaName);
                    }
                }

                var @namespace = Naming.ComputeNamespace(
                    generatorConfig.RootNamespace,
                    generatorConfig.ProjectDir,
                    queryFile.FilePath,
                    queryFile.ModuleName);

                var (source, diagnostics) = QueryEmitter.Emit(@namespace, queryFile, entities, generatorConfig.Naming);

                foreach (var diagnostic in diagnostics)
                {
                    productionContext.ReportDiagnostic(diagnostic.ToDiagnostic());
                }

                if (source is not null)
                {
                    var className = Naming.ToPascalCase(queryFile.ModuleName) + "Queries";
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(queryFile.FilePath);
                    productionContext.AddSource($"{@namespace}.{className}.{fileName}.g.cs", source);
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

    private static QueryFile BuildQueries(DslFile file)
    {
        var parse = QueryParser.Parse(file.Path, file.Text);
        return new QueryFile(
            parse.ModuleName,
            file.Path,
            new EquatableArray<QueryModel>([.. parse.Queries]),
            new EquatableArray<DiagnosticInfo>([.. parse.Diagnostics]));
    }
}

/// <summary>An equatable model of one loaded DormantQL file (path + content).</summary>
/// <param name="Path">The file path.</param>
/// <param name="Text">The file contents.</param>
internal readonly record struct DslFile(string Path, string Text);

/// <summary>Equatable build configuration projected from analyzer config options (FR-046/FR-053).</summary>
/// <param name="RootNamespace">The consuming project's root namespace, if known.</param>
/// <param name="ProjectDir">The consuming project's directory, if known.</param>
/// <param name="Naming">The project-level database naming convention (default snake_case).</param>
internal readonly record struct GeneratorConfig(
    string? RootNamespace,
    string? ProjectDir,
    NamingConvention Naming);

/// <summary>Stable pipeline step names used by cacheability tests (research §8).</summary>
internal static class TrackingNames
{
    /// <summary>The step that loads DormantQL files from additional texts.</summary>
    public const string LoadDslFiles = "LoadDslFiles";

    /// <summary>The step that parses + validates a schema file.</summary>
    public const string ParseSchema = "ParseSchema";

    /// <summary>The step that loads DormantQL query files from additional texts.</summary>
    public const string LoadQueryFiles = "LoadQueryFiles";

    /// <summary>The step that parses a query file.</summary>
    public const string ParseQueries = "ParseQueries";

    /// <summary>The step that projects build configuration (root namespace, project dir).</summary>
    public const string Config = "Config";
}
